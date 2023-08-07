namespace Gitea.Declarative

open System
open Argu

type RefreshAuthArgsFragment =
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced>] Gitea_Host of string
    | [<ExactlyOnce ; EqualsAssignmentOrSpaced ; CustomAppSettings "GITEA_ADMIN_API_TOKEN">] Gitea_Admin_Api_Token of
        string
    | [<Unique ; EqualsAssignmentOrSpaced ; CustomAppSettings "GITHUB_API_TOKEN">] GitHub_Api_Token of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Gitea_Host _ -> "the Gitea host, e.g. https://gitea.mydomain.com"
            | Gitea_Admin_Api_Token _ ->
                "a Gitea admin user's API token; can be read from the environment variable GITEA_ADMIN_API_TOKEN"
            | GitHub_Api_Token _ ->
                "a GitHub API token with read access to every desired sync-from-GitHub repo; can be read from the environment variable GITHUB_API_TOKEN"

type RefreshAuthArgs =
    {
        GiteaHost : Uri
        GiteaAdminApiToken : string
        GitHubApiToken : string
    }

    static member OfParse
        (parsed : ParseResults<RefreshAuthArgsFragment>)
        : Result<RefreshAuthArgs, ArguParseException>
        =
        try
            {
                GiteaHost = parsed.GetResult RefreshAuthArgsFragment.Gitea_Host |> Uri
                GiteaAdminApiToken = parsed.GetResult RefreshAuthArgsFragment.Gitea_Admin_Api_Token
                GitHubApiToken = parsed.GetResult RefreshAuthArgsFragment.GitHub_Api_Token
            }
            |> Ok
        with :? ArguParseException as e ->
            Error e

[<RequireQualifiedAccess>]
module RefreshAuth =

    let run (args : RefreshAuthArgs) : Async<int> =
        async {
            use httpClient = Utils.createHttpClient args.GiteaHost args.GiteaAdminApiToken
            let client = Gitea.Client httpClient |> IGiteaClient.fromReal
            do! Gitea.refreshAuth client args.GitHubApiToken

            return 0
        }
