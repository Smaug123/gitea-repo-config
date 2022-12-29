namespace Gitea.Test

open System.IO
open System.Reflection
open Gitea
open NUnit.Framework
open FsUnitTyped
open Newtonsoft.Json
open Newtonsoft.Json.Schema
open Newtonsoft.Json.Schema.Generation
open Newtonsoft.Json.Serialization

[<TestFixture>]
module TestSchema =
    let schemaGen = JSchemaGenerator ()
    schemaGen.ContractResolver <- CamelCasePropertyNamesContractResolver ()

    let rec findFileAbove (fileName : string) (di : DirectoryInfo) =
        if isNull di then
            failwith "hit the root without finding anything"

        let candidate = Path.Combine (di.FullName, fileName) |> FileInfo

        if candidate.Exists then
            candidate
        else
            findFileAbove fileName di.Parent

    let rec findExampleFile (di : DirectoryInfo) =
        if isNull di then
            failwith "hit the root without finding anything"

        let candidate = Path.Combine (di.FullName, "GiteaConfig.json") |> FileInfo

        if candidate.Exists then
            candidate
        else
            findExampleFile di.Parent

    [<Test>]
    let ``Schema is consistent`` () =
        let schemaFile =
            Assembly.GetExecutingAssembly().Location
            |> FileInfo
            |> fun fi -> fi.Directory
            |> findFileAbove "Gitea/GiteaConfig.schema.json"

        let existing = JSchema.Parse (File.ReadAllText schemaFile.FullName)
        let derived = schemaGen.Generate typeof<SerialisedGiteaConfig>

        existing.ToString () |> shouldEqual (derived.ToString ())

    [<Test>]
    let ``Example conforms to schema`` () =
        let executing = Assembly.GetExecutingAssembly().Location |> FileInfo
        let schemaFile = findFileAbove "GiteaConfig.json" executing.Directory

        let existing = JSchema.Parse (File.ReadAllText schemaFile.FullName)

        let jsonFile = findExampleFile executing.Directory
        let json = File.ReadAllText jsonFile.FullName

        use reader = new JsonTextReader (new StringReader (json))
        use validatingReader = new JSchemaValidatingReader (reader)
        validatingReader.Schema <- existing

        let messages = ResizeArray ()
        validatingReader.ValidationEventHandler.Add (fun args -> messages.Add args.Message)

        let ser = JsonSerializer ()
        ser.ContractResolver <- CamelCasePropertyNamesContractResolver ()
        let _config = ser.Deserialize<SerialisedGiteaConfig> validatingReader

        messages |> shouldBeEmpty

    [<Test>]
    [<Explicit "Run this to regenerate the schema file">]
    let ``Update schema file`` () =
        let schemaFile =
            Assembly.GetExecutingAssembly().Location
            |> FileInfo
            |> fun fi -> fi.Directory
            |> findFileAbove "Gitea/GiteaConfig.schema.json"

        let schema = schemaGen.Generate typeof<SerialisedGiteaConfig>

        File.WriteAllText (schemaFile.FullName, schema.ToString ())
