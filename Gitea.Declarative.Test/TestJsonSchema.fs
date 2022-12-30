namespace Gitea.Declarative.Test

open System.IO
open System.Reflection
open Gitea.Declarative
open NJsonSchema.Generation
open NJsonSchema.Validation
open NUnit.Framework
open FsUnitTyped
open NJsonSchema
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

[<TestFixture>]
module TestSchema =

    [<Test>]
    let ``Example conforms to schema`` () =
        let executing = Assembly.GetExecutingAssembly().Location |> FileInfo

        let schemaFile =
            Utils.findFileAbove "Gitea.Declarative.Lib/GiteaConfig.schema.json" executing.Directory

        let schema = JsonSchema.FromJsonAsync(File.ReadAllText schemaFile.FullName).Result

        let jsonFile = Utils.findFileAbove "GiteaConfig.json" executing.Directory
        let json = File.ReadAllText jsonFile.FullName

        let validator = JsonSchemaValidator ()
        let errors = validator.Validate (json, schema)

        errors |> shouldBeEmpty

    [<Test>]
    let ``Example can be loaded`` () =
        let executing = Assembly.GetExecutingAssembly().Location |> FileInfo
        let jsonFile = Utils.findFileAbove "GiteaConfig.json" executing.Directory
        GiteaConfig.get jsonFile |> ignore

    [<Test>]
    [<Explicit "Run this to regenerate the schema file">]
    let ``Update schema file`` () =
        let schemaFile =
            Assembly.GetExecutingAssembly().Location
            |> FileInfo
            |> fun fi -> fi.Directory
            |> Utils.findFileAbove "Gitea.Declarative.Lib/GiteaConfig.schema.json"

        let settings = JsonSchemaGeneratorSettings ()

        settings.SerializerSettings <-
            JsonSerializerSettings (ContractResolver = CamelCasePropertyNamesContractResolver ())

        let schema = JsonSchema.FromType (typeof<SerialisedGiteaConfig>, settings)

        // Hack around the lack of discriminated unions in C#
        let serialisedRepoSchema = schema.Definitions.[typeof<SerialisedRepo>.Name]
        serialisedRepoSchema.RequiredProperties.Clear ()

        do
            let schema = JsonSchema ()
            schema.RequiredProperties.Add "description"
            schema.RequiredProperties.Add "gitHub"
            serialisedRepoSchema.OneOf.Add schema

        do
            let schema = JsonSchema ()
            schema.RequiredProperties.Add "description"
            schema.RequiredProperties.Add "native"
            serialisedRepoSchema.OneOf.Add schema

        File.WriteAllText (schemaFile.FullName, schema.ToJson ())
