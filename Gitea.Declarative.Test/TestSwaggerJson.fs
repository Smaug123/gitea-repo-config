namespace Gitea.Declarative.Test

open System.IO
open System.Net.Http
open System.Reflection
open NUnit.Framework

[<TestFixture>]
module TestSwaggerJson =

    [<Literal>]
    let GITEA_URL = "https://gitea.patrickstevens.co.uk"

    [<Test>]
    [<Explicit>]
    let ``Update swagger file`` () : unit =
        let swaggerFile =
            Assembly.GetExecutingAssembly().Location
            |> FileInfo
            |> fun fi -> fi.Directory
            |> Utils.findFileAbove "Gitea/swagger.v1.json"

        task {
            use client = new HttpClient ()
            let! stream = client.GetStreamAsync $"{GITEA_URL}/swagger.v1.json"
            use file = swaggerFile.OpenWrite ()
            do! stream.CopyToAsync file
            return ()
        }
        |> fun t -> t.Result
