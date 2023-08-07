namespace Gitea.Declarative

open System
open System.Net.Http
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Options

[<RequireQualifiedAccess>]
module internal Utils =

    let createLoggerProvider () =
        let options =
            let options = ConsoleLoggerOptions ()

            { new IOptionsMonitor<ConsoleLoggerOptions> with
                member _.Get _ = options
                member _.CurrentValue = options

                member _.OnChange _ =
                    { new IDisposable with
                        member _.Dispose () = ()
                    }
            }

        new ConsoleLoggerProvider (options)

    let createHttpClient (host : Uri) (apiKey : string) =
        let client = new HttpClient ()
        client.BaseAddress <- host
        client.DefaultRequestHeaders.Add ("Authorization", $"token {apiKey}")

        client
