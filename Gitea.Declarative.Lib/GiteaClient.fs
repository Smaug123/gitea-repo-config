namespace Gitea.Declarative

open SwaggerProvider

[<AutoOpen>]
module GiteaClient =

    [<Literal>]
    let Host = "file://" + __SOURCE_DIRECTORY__ + "/swagger.v1.json"

    type Gitea = SwaggerClientProvider<Host>

    let getAllPushMirrors (client : Gitea.Client) (owner : string) (repoName : string) : Gitea.PushMirror array Async =
        let rec go (page : int64) (soFar : Gitea.PushMirror array) =
            async {
                let! newPage =
                    client.RepoListPushMirrors (owner, repoName, Some page, Some 100L)
                    |> Async.AwaitTask

                let soFar = Array.append soFar newPage

                if newPage.Length < 100 then
                    return soFar
                else
                    return! go (page + 1L) soFar
            }

        go 0L [||]
