namespace Gitea

open System
open System.ComponentModel

[<TypeConverter(typeof<UserTypeConverter>)>]
type User =
    | User of string

    override this.ToString () =
        match this with
        | User u -> u

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

and RepoNameTypeConverter () =
    inherit TypeConverter ()
    override _.CanConvertFrom (_, t : Type) : bool = t = typeof<string>
    override _.ConvertFrom (_, _, v : obj) : obj = v |> unbox<string> |> RepoName |> box
