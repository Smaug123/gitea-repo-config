namespace Gitea.Declarative

open System.IO
open Argu

type OutputSchemaArgsFragment =
    | Output of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Output _ -> "path to the file to be written (or overwritten, if it already exists), instead of stdout"

type OutputSchemaArgs =
    {
        Output : FileInfo option
    }

    static member OfParse
        (parsed : ParseResults<OutputSchemaArgsFragment>)
        : Result<OutputSchemaArgs, ArguParseException>
        =
        try
            {
                Output = parsed.TryGetResult OutputSchemaArgsFragment.Output |> Option.map FileInfo
            }
            |> Ok
        with :? ArguParseException as e ->
            Error e

[<RequireQualifiedAccess>]
module OutputSchema =

    let run (args : OutputSchemaArgs) : Async<int> =
        async {
            use stream = GiteaConfig.getSchema ()

            match args.Output with
            | None ->
                let reader = new StreamReader (stream)
                System.Console.WriteLine (reader.ReadToEnd ())
            | Some output ->
                use output = output.OpenWrite ()
                stream.CopyTo output

            return 0
        }
