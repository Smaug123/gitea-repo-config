namespace Gitea.Declarative.Test

open System
open System.IO
open FsCheck

type CustomArb () =
    static member UriGen = Gen.constant (Uri "http://example.com") |> Arb.fromGen

[<RequireQualifiedAccess>]
module Utils =

    let rec findFileAbove (fileName : string) (di : DirectoryInfo) =
        if isNull di then
            failwith "hit the root without finding anything"

        let candidate = Path.Combine (di.FullName, fileName) |> FileInfo

        if candidate.Exists then
            candidate
        else
            findFileAbove fileName di.Parent
