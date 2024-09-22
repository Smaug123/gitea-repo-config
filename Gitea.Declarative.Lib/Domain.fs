namespace Gitea.Declarative

open System
open System.ComponentModel
open System.Text.Json.Nodes
open WoofWare.Myriad.Plugins

[<TypeConverter(typeof<UserTypeConverter>)>]
type User =
    | User of string

    override this.ToString () =
        match this with
        | User u -> u

    static member jsonParse (s : JsonNode) : User = s.GetValue<string> () |> User

    static member toJsonNode (User u) : JsonNode = JsonValue.op_Implicit u

and UserTypeConverter () =
    inherit TypeConverter ()
    override _.CanConvertFrom (_, t : Type) : bool = t = typeof<string>
    override _.ConvertFrom (_, _, v : obj) : obj = v |> unbox<string> |> User |> box

[<TypeConverter(typeof<RepoNameTypeConverter>)>]
type RepoName =
    | RepoName of string

    override this.ToString () =
        match this with
        | RepoName r -> r

    static member jsonParse (s : JsonNode) : RepoName = s.GetValue<string> () |> RepoName

    static member toJsonNode (RepoName r) : JsonNode = JsonValue.op_Implicit r

and RepoNameTypeConverter () =
    inherit TypeConverter ()
    override _.CanConvertFrom (_, t : Type) : bool = t = typeof<string>
    override _.ConvertFrom (_, _, v : obj) : obj = v |> unbox<string> |> RepoName |> box
