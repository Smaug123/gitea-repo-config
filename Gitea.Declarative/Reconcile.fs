namespace Gitea.Declarative

open System
open System.IO
open Argu
open Microsoft.Extensions.Logging

type RunArgsFragment =
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Config_File of string
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Gitea_Host of string
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced ; CustomAppSettings "GITEA_ADMIN_API_TOKEN">] Gitea_Admin_Api_Token of
        string
    | [<Unique ; EqualsAssignmentOrSpaced ; CustomAppSettings "GITHUB_API_TOKEN">] GitHub_Api_Token of string
    | Dry_Run

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config_File _ ->
                "a config file, JSON, conforming to GiteaConfig.schema.json, specifying the desired Gitea configuration"
            | Gitea_Host _ -> "the Gitea host, e.g. https://gitea.mydomain.com"
            | Gitea_Admin_Api_Token _ ->
                "a Gitea admin user's API token; can be read from the environment variable GITEA_ADMIN_API_TOKEN"
            | GitHub_Api_Token _ ->
                "a GitHub API token with read access to every desired sync-from-GitHub repo; can be read from the environment variable GITHUB_API_TOKEN"
            | Dry_Run -> "don't actually perform the reconciliation"

type RunArgs =
    {
        ConfigFile : FileInfo
        GiteaHost : Uri
        GiteaAdminApiToken : string
        GitHubApiToken : string option
        DryRun : bool
    }

    static member OfParse (parsed : ParseResults<RunArgsFragment>) : Result<RunArgs, ArguParseException> =
        try
            {
                ConfigFile = parsed.GetResult RunArgsFragment.Config_File |> FileInfo
                GiteaHost = parsed.GetResult RunArgsFragment.Gitea_Host |> Uri
                GiteaAdminApiToken = parsed.GetResult RunArgsFragment.Gitea_Admin_Api_Token
                GitHubApiToken = parsed.TryGetResult RunArgsFragment.GitHub_Api_Token
                DryRun = parsed.TryGetResult RunArgsFragment.Dry_Run |> Option.isSome
            }
            |> Ok
        with :? ArguParseException as e ->
            Error e

[<RequireQualifiedAccess>]
module Reconcile =

    let private getUserInput (s : string) : string =
        Console.Write s
        Console.ReadLine ()

    let run (args : RunArgs) : Async<int> =

        let config = GiteaConfig.get args.ConfigFile

        async {
            use loggerProvider = Utils.createLoggerProvider ()
            let logger = loggerProvider.CreateLogger "Gitea.Declarative"

            use httpClient = Utils.createHttpClient args.GiteaHost args.GiteaAdminApiToken
            let client = Gitea.Client httpClient |> IGiteaClient.fromReal

            logger.LogInformation "Checking users..."
            let! userErrors = Gitea.checkUsers config client

            match userErrors, args.DryRun with
            | Ok (), _ -> ()
            | Error errors, false -> do! Gitea.reconcileUserErrors logger getUserInput client errors
            | Error errors, true ->
                logger.LogError (
                    "Differences encountered in user configuration, but not reconciling them due to --dry-run. Errors may occur while checking repo configuration. {UserErrors}",
                    errors
                )

            logger.LogInformation "Checking repos..."
            let! repoErrors = Gitea.checkRepos logger config client

            match repoErrors, args.DryRun with
            | Ok (), _ -> ()
            | Error errors, false -> do! Gitea.reconcileRepoErrors logger client args.GitHubApiToken errors
            | Error errors, true ->
                logger.LogError (
                    "Differences encountered in repo configuration, but not reconciling them due to --dry-run. {RepoErrors}",
                    errors
                )

            match userErrors, repoErrors with
            | Ok (), Ok () -> return 0
            | Ok (), Error _ -> return 1
            | Error _, Ok () -> return 2
            | Error _, Error _ -> return 3
        }
