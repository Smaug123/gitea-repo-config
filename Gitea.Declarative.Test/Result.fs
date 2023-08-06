namespace Gitea.Declarative.Test

[<RequireQualifiedAccess>]
module Result =

    let get r =
        match r with
        | Ok o -> o
        | Error e -> failwithf "Expected Ok, got: %+A" e

    let getError r =
        match r with
        | Ok o -> failwithf "Expected Error, got: %+A" o
        | Error e -> e
