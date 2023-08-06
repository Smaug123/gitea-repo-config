namespace Gitea.Declarative.Test

open System
open Microsoft.Extensions.Logging

[<RequireQualifiedAccess>]
module LoggerFactory =

    /// Creates a test ILoggerFactory, a sink whose provided inputs you can access through the `unit -> string list`.
    let makeTest () : ILoggerFactory * (unit -> string list) =
        let outputs = ResizeArray<_> ()

        let lf =
            { new ILoggerFactory with
                member _.Dispose () = ()

                member _.CreateLogger (name : string) =
                    { new ILogger with
                        member _.IsEnabled _ = true

                        member _.BeginScope _ =
                            { new IDisposable with
                                member _.Dispose () = ()
                            }

                        member _.Log (_, _, state, exc : exn, formatter) =
                            let toWrite = formatter.Invoke (state, exc)
                            lock outputs (fun () -> outputs.Add toWrite)
                    }

                member _.AddProvider provider = failwith "unsupported"
            }

        lf, (fun () -> lock outputs (fun () -> Seq.toList outputs))
