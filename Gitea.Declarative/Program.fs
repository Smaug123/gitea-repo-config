namespace Gitea.Declarative

open System
open System.IO
open System.Net.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Options
open Argu

type ArgsFragments =
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Config_File of string
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Gitea_Host of string
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Gitea_Admin_Api_Token of string
    | [<Unique ; EqualsAssignmentOrSpaced>] GitHub_Api_Token of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config_File _ ->
                "a config file, JSON, conforming to GiteaConfig.schema.json, specifying the desired Gitea configuration"
            | Gitea_Host _ -> "the Gitea host, e.g. https://gitea.mydomain.com"
            | Gitea_Admin_Api_Token _ -> "a Gitea admin user's API token"
            | GitHub_Api_Token _ -> "a GitHub API token with read access to every desired sync-from-GitHub repo"

type Args =
    {
        ConfigFile : FileInfo
        GiteaHost : Uri
        GiteaAdminApiToken : string
        GitHubApiToken : string option
    }

module Program =

    let printUserErrors (m : Map<User, AlignmentError<UserInfo>>) =
        m |> Map.iter (fun (User u) err -> printfn $"%s{u}: {err}")

    let printRepoErrors (m : Map<User, Map<RepoName, AlignmentError<Repo>>>) =
        m
        |> Map.iter (fun (User u) errMap -> errMap |> Map.iter (fun (RepoName r) err -> printfn $"%s{u}: %s{r}: {err}"))

    let getUserInput (s : string) : string =
        Console.Write s
        Console.ReadLine ()

    [<EntryPoint>]
    let main argv =
        let parser = ArgumentParser.Create<ArgsFragments> ()
        let parsed = parser.Parse argv

        let args =
            {
                ConfigFile = parsed.GetResult ArgsFragments.Config_File |> FileInfo
                GiteaHost = parsed.GetResult ArgsFragments.Gitea_Host |> Uri
                GiteaAdminApiToken = parsed.GetResult ArgsFragments.Gitea_Admin_Api_Token
                GitHubApiToken = parsed.TryGetResult ArgsFragments.GitHub_Api_Token
            }

        let config = GiteaConfig.get args.ConfigFile

        let options =
            let options = ConsoleLoggerOptions ()

            { new IOptionsMonitor<ConsoleLoggerOptions> with
                member _.Get _ = options
                member _.CurrentValue = options

                member _.OnChange _ =
                    { new IDisposable with
                        member _.Dispose () = ()
                    }
            }

        use loggerProvider = new ConsoleLoggerProvider (options)
        let logger = loggerProvider.CreateLogger "Gitea.Declarative"

        use client = new HttpClient ()
        client.BaseAddress <- args.GiteaHost
        client.DefaultRequestHeaders.Add ("Authorization", $"token {args.GiteaAdminApiToken}")

        let client = Gitea.Client client

        task {
            logger.LogInformation "Checking users..."
            let! userErrors = Gitea.checkUsers config client

            match userErrors with
            | Ok () -> ()
            | Error errors -> do! Gitea.reconcileUserErrors logger getUserInput client errors

            logger.LogInformation "Checking repos..."
            let! repoErrors = Gitea.checkRepos config client

            match repoErrors with
            | Ok () -> ()
            | Error errors -> do! Gitea.reconcileRepoErrors logger client args.GitHubApiToken errors

            match userErrors, repoErrors with
            | Ok (), Ok () -> return 0
            | Ok (), Error _ -> return 1
            | Error _, Ok () -> return 2
            | Error _, Error _ -> return 3
        }
        |> fun t -> t.Result
