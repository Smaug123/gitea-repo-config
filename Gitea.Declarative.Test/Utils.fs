namespace Gitea.Declarative.Test

open System.Collections.Generic
open Gitea.Declarative
open System
open System.IO
open FsCheck
open Microsoft.FSharp.Reflection

[<RequireQualifiedAccess>]
module Types =
    let emptyUser (loginName : string) : GiteaClient.User =
        {
            Active = None
            Created = None
            Description = None
            Email = None
            Id = None
            Language = None
            Location = None
            Login = None
            Restricted = None
            Visibility = None
            Website = None
            FullName = None
            IsAdmin = None
            LoginName = Some loginName
            ProhibitLogin = None
            AdditionalProperties = Dictionary ()
            AvatarUrl = None
            FollowersCount = None
            FollowingCount = None
            StarredReposCount = None
            LastLogin = None
        }

    let emptyRepo (fullName : string) (defaultBranch : string) : GiteaClient.Repository =
        {
            Archived = None
            Description = Some "a description here"
            Empty = None
            Fork = None
            Id = None
            Internal = None
            Language = None
            Link = None
            Mirror = None
            Name = None
            Owner = Some (emptyUser "some-username")
            Private = None
            Website = None
            AllowRebase = None
            AllowMergeCommits = None
            AllowRebaseExplicit = None
            AllowRebaseUpdate = None
            AllowSquashMerge = None
            DefaultBranch = Some defaultBranch
            HasIssues = None
            HasProjects = None
            HasWiki = None
            HasPullRequests = None
            DefaultMergeStyle = None
            AdditionalProperties = Dictionary ()
            AvatarUrl = None
            CloneUrl = None
            CreatedAt = None
            DefaultAllowMaintainerEdit = None
            DefaultDeleteBranchAfterMerge = None
            ExternalTracker = None
            ExternalWiki = None
            ForksCount = None
            FullName = Some fullName
            HtmlUrl = None
            IgnoreWhitespaceConflicts = None
            InternalTracker = None
            LanguagesUrl = None
            MirrorInterval = None
            MirrorUpdated = None
            OpenIssuesCount = None
            OpenPrCounter = None
            OriginalUrl = None
            Parent = None
            Permissions = None
            ReleaseCounter = None
            RepoTransfer = None
            Size = None
            SshUrl = None
            StarsCount = None
            Template = None
            UpdatedAt = None
            WatchersCount = None
        }

type CustomArb () =
    static member UriGen = Gen.constant (Uri "http://example.com") |> Arb.fromGen

    static member User : Arbitrary<GiteaClient.User> =
        gen {
            let! active = Arb.generate<_>
            let! created = Arb.generate<_>
            let! description = Arb.generate<_>
            let! email = Arb.generate<_>
            let! id = Arb.generate<_>
            let! language = Arb.generate<_>
            let! location = Arb.generate<_>
            let! login = Arb.generate<_>
            let! restricted = Arb.generate<_>
            let! visibility = Arb.generate<_>
            let! website = Arb.generate<_>
            let! fullname = Arb.generate<_>
            let! isAdmin = Arb.generate<_>
            let! loginName = Arb.generate<_>
            let! prohibitLogin = Arb.generate<_>

            return
                ({ Types.emptyUser loginName with
                    Active = active
                    Created = created
                    Description = description
                    Email = email
                    Id = id
                    Language = language
                    Location = location
                    Login = login
                    Restricted = restricted
                    Visibility = visibility
                    Website = website
                    FullName = fullname
                    IsAdmin = isAdmin
                    ProhibitLogin = prohibitLogin
                }
                : GiteaClient.User)
        }
        |> Arb.fromGen

    static member RepositoryGen : Arbitrary<GiteaClient.Repository> =
        gen {
            let! archived = Arb.generate<_>
            let! description = Arb.generate<_>
            let! empty = Arb.generate<_>
            let! fork = Arb.generate<_>
            let! id = Arb.generate<_>
            let! isInternal = Arb.generate<_>
            let! language = Arb.generate<_>
            let! link = Arb.generate<_>
            let! mirror = Arb.generate<_>
            let! name = Arb.generate<_>
            let! fullName = Arb.generate<_>
            let! owner = Arb.generate<_>
            let! isPrivate = Arb.generate<_>
            let! website = Arb.generate<_>
            let! allowRebase = Arb.generate<_>
            let! allowMergeCommits = Arb.generate<_>
            let! allowRebaseExplicit = Arb.generate<_>
            let! allowRebaseUpdate = Arb.generate<_>
            let! allowSquashMerge = Arb.generate<_>
            let! defaultBranch = Arb.generate<_>
            let! hasIssues = Arb.generate<_>
            let! hasProjects = Arb.generate<_>
            let! hasWiki = Arb.generate<_>
            let! hasPullRequests = Arb.generate<_>

            let! mergeStyle =
                FSharpType.GetUnionCases typeof<MergeStyle>
                |> Array.map (fun uci -> FSharpValue.MakeUnion (uci, [||]) |> unbox<MergeStyle>)
                |> Gen.elements

            let mergeStyle = (mergeStyle : MergeStyle).ToString ()

            return
                ({ Types.emptyRepo fullName defaultBranch with
                    Archived = archived
                    Description = Some description
                    Empty = empty
                    Fork = fork
                    Id = id
                    Internal = isInternal
                    Language = language
                    Link = link
                    Mirror = mirror
                    Name = Some name
                    Owner = Some owner
                    Private = isPrivate
                    Website = website
                    AllowRebase = allowRebase
                    AllowMergeCommits = allowMergeCommits
                    AllowRebaseExplicit = allowRebaseExplicit
                    AllowRebaseUpdate = allowRebaseUpdate
                    AllowSquashMerge = allowSquashMerge
                    HasIssues = hasIssues
                    HasProjects = hasProjects
                    HasWiki = hasWiki
                    HasPullRequests = hasPullRequests
                    DefaultMergeStyle = Some mergeStyle
                    AdditionalProperties = Dictionary ()
                }
                : GiteaClient.Repository)
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
