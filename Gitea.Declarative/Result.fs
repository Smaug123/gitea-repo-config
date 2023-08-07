namespace Gitea.Declarative

[<RequireQualifiedAccess>]
module Result =

    let cata<'ok, 'err, 'result> onOk onError (r : Result<'ok, 'err>) : 'result =
        match r with
        | Ok ok -> onOk ok
        | Error e -> onError e
