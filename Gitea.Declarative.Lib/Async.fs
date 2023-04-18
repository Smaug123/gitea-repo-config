namespace Gitea.Declarative

[<RequireQualifiedAccess>]
module Async =

    let map f a =
        async {
            let! a = a
            return f a
        }
