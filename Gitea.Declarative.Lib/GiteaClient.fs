namespace Gitea.Declarative

open System.Threading.Tasks
open SwaggerProvider

[<AutoOpen>]
module GiteaClient =

    [<Literal>]
    let Host = "file://" + __SOURCE_DIRECTORY__ + "/swagger.v1.json"

    type Gitea = SwaggerClientProvider<Host>

    /// The input function takes page first, then count.
    /// Repeatedly calls `f` with increasing page numbers until all results are returned.
    let getAllPaginated (f : int64 -> int64 -> 'ret array Task) : 'ret array Async =
        let rec go (page : int64) (soFar : 'ret array) =
            async {
                let! newPage = f page 100L |> Async.AwaitTask

                let soFar = Array.append soFar newPage

                if newPage.Length < 100 then
                    return soFar
                else
                    return! go (page + 1L) soFar
            }

        go 0L [||]
