namespace Gitea.Declarative.Test

open Gitea.Declarative
open System
open System.IO
open FsCheck
open Microsoft.FSharp.Reflection

type CustomArb () =
    static member UriGen = Gen.constant (Uri "http://example.com") |> Arb.fromGen

    static member User : Arbitrary<Gitea.User> =
        gen {
            let user = Gitea.User ()
            let! a = Arb.generate<_>
            user.Active <- a
            let! a = Arb.generate<_>
            user.Created <- a
            let! a = Arb.generate<_>
            user.Description <- a
            let! a = Arb.generate<_>
            user.Email <- a
            let! a = Arb.generate<_>
            user.Id <- a
            let! a = Arb.generate<_>
            user.Language <- a
            let! a = Arb.generate<_>
            user.Location <- a
            let! a = Arb.generate<_>
            user.Login <- a
            let! a = Arb.generate<_>
            user.Restricted <- a
            let! a = Arb.generate<_>
            user.Visibility <- a
            let! a = Arb.generate<_>
            user.Website <- a
            let! a = Arb.generate<_>
            user.FullName <- a
            let! a = Arb.generate<_>
            user.IsAdmin <- a
            let! a = Arb.generate<_>
            user.LoginName <- a
            let! a = Arb.generate<_>
            user.ProhibitLogin <- a
            return user
        }
        |> Arb.fromGen

    static member RepositoryGen : Arbitrary<Gitea.Repository> =
        gen {
            let repo = Gitea.Repository ()
            let! a = Arb.generate<_>
            repo.Archived <- a
            let! a = Arb.generate<_>
            repo.Description <- a
            let! a = Arb.generate<_>
            repo.Empty <- a
            let! a = Arb.generate<_>
            repo.Fork <- a
            let! a = Arb.generate<_>
            repo.Id <- a
            let! a = Arb.generate<_>
            repo.Internal <- a
            let! a = Arb.generate<_>
            repo.Language <- a
            let! a = Arb.generate<_>
            repo.Link <- a
            let! a = Arb.generate<_>
            repo.Mirror <- a
            let! a = Arb.generate<_>
            repo.Name <- a
            let! a = Arb.generate<_>
            repo.Owner <- a
            let! a = Arb.generate<_>
            repo.Private <- a
            let! a = Arb.generate<_>
            repo.Website <- a
            let! a = Arb.generate<_>
            repo.AllowRebase <- a
            let! a = Arb.generate<_>
            repo.AllowMergeCommits <- a
            let! a = Arb.generate<_>
            repo.AllowRebaseExplicit <- a
            let! a = Arb.generate<_>
            repo.AllowRebaseUpdate <- a
            let! a = Arb.generate<_>
            repo.AllowSquashMerge <- a
            let! a = Arb.generate<_>
            repo.DefaultBranch <- a
            let! a = Arb.generate<_>
            repo.HasIssues <- a
            let! a = Arb.generate<_>
            repo.HasProjects <- a
            let! a = Arb.generate<_>
            repo.HasWiki <- a
            let! a = Arb.generate<_>
            repo.HasPullRequests <- a

            let! a =
                FSharpType.GetUnionCases typeof<MergeStyle>
                |> Array.map (fun uci -> FSharpValue.MakeUnion (uci, [||]) |> unbox<MergeStyle>)
                |> Gen.elements

            repo.DefaultMergeStyle <- MergeStyle.toString a
            return repo
        }
        |> Arb.fromGen

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
