namespace Gitea.Declarative

open System
open System.IO
open Newtonsoft.Json

type MergeStyle =
    | Merge
    | Rebase
    | RebaseMerge
    | Squash

    static member Parse (s : string) : MergeStyle =
        if s = "merge" then MergeStyle.Merge
        elif s = "squash" then MergeStyle.Squash
        elif s = "rebase" then MergeStyle.Rebase
        elif s = "rebase-merge" then MergeStyle.RebaseMerge
        else failwithf "Unrecognised merge style '%s'" s

    static member toString (s : MergeStyle) : string =
        match s with
        | Merge -> "merge"
        | RebaseMerge -> "rebase-merge"
        | Rebase -> "rebase"
        | Squash -> "squash"

type NativeRepo =
    {
        DefaultBranch : string
        Private : bool option
        IgnoreWhitespaceConflicts : bool option
        HasPullRequests : bool option
        HasProjects : bool option
        HasIssues : bool option
        HasWiki : bool option
        DefaultMergeStyle : MergeStyle option
        DeleteBranchAfterMerge : bool option
        AllowSquashMerge : bool option
        AllowRebaseUpdate : bool option
        AllowRebase : bool option
        AllowRebaseExplicit : bool option
        AllowMergeCommits : bool option
    }

    static member internal OfSerialised (s : SerialisedNativeRepo) =
        {
            NativeRepo.DefaultBranch = s.DefaultBranch
            Private = s.Private |> Option.ofNullable
            IgnoreWhitespaceConflicts = s.IgnoreWhitespaceConflicts |> Option.ofNullable
            HasPullRequests = s.HasPullRequests |> Option.ofNullable
            HasProjects = s.HasProjects |> Option.ofNullable
            HasIssues = s.HasIssues |> Option.ofNullable
            HasWiki = s.HasWiki |> Option.ofNullable
            DefaultMergeStyle = s.DefaultMergeStyle |> Option.ofObj |> Option.map MergeStyle.Parse
            DeleteBranchAfterMerge = s.DeleteBranchAfterMerge |> Option.ofNullable
            AllowSquashMerge = s.AllowSquashMerge |> Option.ofNullable
            AllowRebaseUpdate = s.AllowRebaseUpdate |> Option.ofNullable
            AllowRebase = s.AllowRebase |> Option.ofNullable
            AllowRebaseExplicit = s.AllowRebaseExplicit |> Option.ofNullable
            AllowMergeCommits = s.AllowMergeCommits |> Option.ofNullable
        }

type GitHubRepo =
    {
        Uri : Uri
        /// This is a Golang string.
        MirrorInterval : string
    }

    static member internal OfSerialised (s : SerialisedGitHubRepo) : GitHubRepo =
        {
            Uri = Uri s.Uri
            MirrorInterval =
                // Rather odd behaviour of the API here!
                if s.MirrorInterval = null then
                    "8h0m0s"
                else
                    s.MirrorInterval
        }

type Repo =
    {
        Description : string
        GitHub : GitHubRepo option
        Native : NativeRepo option
    }

    static member Render (u : Gitea.Repository) : Repo =
        {
            Description = u.Description
            GitHub =
                if String.IsNullOrEmpty u.OriginalUrl then
                    None
                else
                    {
                        Uri = Uri u.OriginalUrl
                        MirrorInterval = u.MirrorInterval
                    }
                    |> Some
            Native =
                if String.IsNullOrEmpty u.OriginalUrl then
                    {
                        Private = u.Private
                        DefaultBranch = u.DefaultBranch
                        IgnoreWhitespaceConflicts = u.IgnoreWhitespaceConflicts
                        HasPullRequests = u.HasProjects
                        HasProjects = u.HasProjects
                        HasIssues = u.HasIssues
                        HasWiki = u.HasWiki
                        DefaultMergeStyle = u.DefaultMergeStyle |> Option.ofObj |> Option.map MergeStyle.Parse
                        DeleteBranchAfterMerge = u.DefaultDeleteBranchAfterMerge
                        AllowSquashMerge = u.AllowSquashMerge
                        AllowRebaseUpdate = u.AllowRebaseUpdate
                        AllowRebase = u.AllowRebase
                        AllowRebaseExplicit = u.AllowRebaseExplicit
                        AllowMergeCommits = u.AllowMergeCommits
                    }
                    |> Some
                else
                    None
        }

    static member internal OfSerialised (s : SerialisedRepo) =
        {
            Repo.Description = s.Description
            GitHub = Option.ofNullable s.GitHub |> Option.map GitHubRepo.OfSerialised
            Native = s.Native |> Option.ofNullable |> Option.map NativeRepo.OfSerialised
        }

type UserInfoUpdate =
    | Admin of desired : bool option * actual : bool option
    | Email of desired : string * actual : string
    | Website of desired : Uri * actual : Uri option
    | Visibility of desired : string * actual : string option

type UserInfo =
    {
        IsAdmin : bool option
        Email : string
        Website : Uri option
        Visibility : string option
    }

    static member Render (u : Gitea.User) : UserInfo =
        {
            IsAdmin = u.IsAdmin
            Email = u.Email
            Website =
                if String.IsNullOrEmpty u.Website then
                    None
                else
                    Some (Uri u.Website)
            Visibility =
                if String.IsNullOrEmpty u.Visibility then
                    None
                else
                    Some u.Visibility
        }

    static member internal OfSerialised (s : SerialisedUserInfo) =
        {
            UserInfo.IsAdmin = s.IsAdmin |> Option.ofNullable
            Email = s.Email
            Website = Option.ofObj s.Website
            Visibility = Option.ofObj s.Visibility
        }

    static member Resolve (desired : UserInfo) (actual : UserInfo) : UserInfoUpdate list =
        [
            if desired.IsAdmin <> actual.IsAdmin then
                yield UserInfoUpdate.Admin (desired.IsAdmin, actual.IsAdmin)
            if desired.Email <> actual.Email then
                yield UserInfoUpdate.Email (desired.Email, actual.Email)
            if desired.Website <> actual.Website then
                match desired.Website with
                | Some w -> yield UserInfoUpdate.Website (w, actual.Website)
                | None -> ()
            if desired.Visibility <> actual.Visibility then
                match desired.Visibility with
                | Some v -> yield UserInfoUpdate.Visibility (v, actual.Visibility)
                | None -> ()
        ]

type GiteaConfig =
    {
        Users : Map<User, UserInfo>
        Repos : Map<User, Map<RepoName, Repo>>
    }

    static member internal OfSerialised (s : SerialisedGiteaConfig) =
        {
            GiteaConfig.Users =
                s.Users
                |> Seq.map (fun (KeyValue (user, info)) -> user, UserInfo.OfSerialised info)
                |> Map.ofSeq
            Repos =
                s.Repos
                |> Seq.map (fun (KeyValue (user, repos)) ->
                    let repos =
                        repos
                        |> Seq.map (fun (KeyValue (repoName, repo)) -> repoName, Repo.OfSerialised repo)
                        |> Map.ofSeq

                    user, repos
                )
                |> Map.ofSeq
        }

[<RequireQualifiedAccess>]
module GiteaConfig =
    let get (file : FileInfo) : GiteaConfig =
        let s =
            use reader = new StreamReader (file.OpenRead ())
            reader.ReadToEnd ()

        JsonConvert.DeserializeObject<SerialisedGiteaConfig> s
        |> GiteaConfig.OfSerialised
