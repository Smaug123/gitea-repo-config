namespace Gitea.Declarative

open System.Runtime.ExceptionServices

[<RequireQualifiedAccess>]
module internal Exception =
    let reraiseWithOriginalStackTrace<'a> (e : exn) : 'a =
        let edi = ExceptionDispatchInfo.Capture e
        edi.Throw ()
        failwith "unreachable"
