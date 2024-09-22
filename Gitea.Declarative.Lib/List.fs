namespace Gitea.Declarative

[<RequireQualifiedAccess>]
module internal List =

    /// f takes a page number and a limit (i.e. a desired page size).
    let getPaginated (f : int -> int -> 'a list Async) : 'a list Async =
        let count = 30

        let rec go (page : int) (acc : 'a list list) =
            async {
                let! result = f page count

                if result.Length >= count then
                    return! go (page + 1) (result :: acc)
                else
                    return (result :: acc) |> List.concat
            }

        go 1 []
