namespace Gitea.Declarative

open System
open System.IO
open Argu
open NJsonSchema
open NJsonSchema.Validation

type VerifyArgsFragment =
    | [<MainCommand>] Input of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Input _ -> "path to the file to be verified, or the literal '-' to read from stdin"

type VerifyArgs =
    | File of FileInfo
    | Stdin

    static member OfParse (parsed : ParseResults<VerifyArgsFragment>) : Result<VerifyArgs, ArguParseException> =
        let input =
            try
                parsed.GetResult VerifyArgsFragment.Input |> Ok
            with :? ArguParseException as e ->
                Error e

        input
        |> Result.map (fun input ->
            if input = "-" then
                VerifyArgs.Stdin
            else
                VerifyArgs.File (FileInfo input)
        )

[<RequireQualifiedAccess>]
module Verify =
    let run (args : VerifyArgs) : Async<int> =
        async {
            let validator = JsonSchemaValidator ()
            use schema = GiteaConfig.getSchema ()
            let! ct = Async.CancellationToken
            let! schema = JsonSchema.FromJsonAsync (schema, ct) |> Async.AwaitTask

            use jsonStream =
                match args with
                | VerifyArgs.Stdin -> Console.OpenStandardInput ()
                | VerifyArgs.File f -> f.OpenRead ()

            let reader = new StreamReader (jsonStream)
            let! json = reader.ReadToEndAsync ct |> Async.AwaitTask

            let errors = validator.Validate (json, schema)

            if errors.Count = 0 then
                return 0
            else
                for error in errors do
                    Console.Error.WriteLine (sprintf "Error: %+A" error)

                return 1
        }
