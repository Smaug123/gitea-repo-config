namespace Gitea

[<RequireQualifiedAccess>]
module internal Map =

    let inline union<'k, 'v when 'k : comparison>
        ([<InlineIfLambda>] f : 'k -> 'v -> 'v -> 'v)
        (m1 : Map<'k, 'v>)
        (m2 : Map<'k, 'v>)
        : Map<'k, 'v>
        =
        (m1, m2)
        ||> Map.fold (fun acc k v2 ->
            acc
            |> Map.change
                k
                (function
                | None -> Some v2
                | Some v1 -> Some (f k v1 v2)
                )
        )
