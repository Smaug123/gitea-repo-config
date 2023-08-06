namespace Gitea.Declarative

[<RequireQualifiedAccess>]
module internal Array =

    /// f takes a page number and a limit (i.e. a desired page size).
    let getPaginated (f : int64 -> int64 -> 'a array Async) : 'a list Async =
        let count = 30

        let rec go (page : int) (acc : 'a array list) =
            async {
                let! result = f page count

                if result.Length >= count then
                    return! go (page + 1) (result :: acc)
                else
                    return (result :: acc) |> Seq.concat |> Seq.toList
            }

        go 1 []
