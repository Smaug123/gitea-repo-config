namespace Gitea.Declarative

open System
open System.Collections.Generic
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

    override this.ToString () : string =
        match this with
        | Merge -> "merge"
        | RebaseMerge -> "rebase-merge"
        | Rebase -> "rebase"
        | Squash -> "squash"

type PushMirror =
    {
        GitHubAddress : Uri
    }

    static member OfSerialised (s : SerialisedPushMirror) : PushMirror =
        {
            GitHubAddress = Uri s.GitHubAddress
        }

    member this.ToSerialised () : SerialisedPushMirror =
        {
            GitHubAddress = (this.GitHubAddress : Uri).ToString ()
        }

type ProtectedBranch =
    {
        BranchName : string
        BlockOnOutdatedBranch : bool option
        RequiredStatusChecks : string list option
    }

    static member OfSerialised (s : SerialisedProtectedBranch) : ProtectedBranch =
        {
            BranchName = s.BranchName
            BlockOnOutdatedBranch = Option.ofNullable s.BlockOnOutdatedBranch
            RequiredStatusChecks = Option.ofObj s.RequiredStatusChecks |> Option.map List.ofArray
        }

    member this.ToSerialised () : SerialisedProtectedBranch =
        {
            BranchName = this.BranchName
            BlockOnOutdatedBranch = Option.toNullable this.BlockOnOutdatedBranch
            RequiredStatusChecks =
                match this.RequiredStatusChecks with
                | None -> null
                | Some l -> List.toArray l
        }

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
        Mirror : PushMirror option
        ProtectedBranches : ProtectedBranch Set
        Collaborators : string Set
    }

    static member Default : NativeRepo =
        {
            DefaultBranch = "main"
            Private = Some false
            IgnoreWhitespaceConflicts = Some true
            HasPullRequests = Some true
            HasProjects = Some false
            HasIssues = Some true
            HasWiki = Some false
            DefaultMergeStyle = Some MergeStyle.Rebase
            DeleteBranchAfterMerge = Some true
            AllowSquashMerge = Some true
            AllowRebaseUpdate = Some false
            AllowRebase = Some false
            AllowRebaseExplicit = Some false
            AllowMergeCommits = Some false
            Mirror = None
            ProtectedBranches = Set.empty
            Collaborators = Set.empty
        }

    member this.OverrideDefaults () =
        {
            DefaultBranch = this.DefaultBranch
            Private = this.Private |> Option.orElse NativeRepo.Default.Private
            IgnoreWhitespaceConflicts =
                this.IgnoreWhitespaceConflicts
                |> Option.orElse NativeRepo.Default.IgnoreWhitespaceConflicts
            HasPullRequests = this.HasPullRequests |> Option.orElse NativeRepo.Default.HasPullRequests
            HasProjects = this.HasProjects |> Option.orElse NativeRepo.Default.HasProjects
            HasIssues = this.HasIssues |> Option.orElse NativeRepo.Default.HasIssues
            HasWiki = this.HasWiki |> Option.orElse NativeRepo.Default.HasWiki
            DefaultMergeStyle = this.DefaultMergeStyle |> Option.orElse NativeRepo.Default.DefaultMergeStyle
            DeleteBranchAfterMerge =
                this.DeleteBranchAfterMerge
                |> Option.orElse NativeRepo.Default.DeleteBranchAfterMerge
            AllowSquashMerge = this.AllowSquashMerge |> Option.orElse NativeRepo.Default.AllowSquashMerge
            AllowRebaseUpdate = this.AllowRebaseUpdate |> Option.orElse NativeRepo.Default.AllowRebaseUpdate
            AllowRebase = this.AllowRebase |> Option.orElse NativeRepo.Default.AllowRebase
            AllowRebaseExplicit = this.AllowRebaseExplicit |> Option.orElse NativeRepo.Default.AllowRebaseExplicit
            AllowMergeCommits = this.AllowMergeCommits |> Option.orElse NativeRepo.Default.AllowMergeCommits
            Mirror = this.Mirror
            ProtectedBranches = this.ProtectedBranches // TODO should this replace null with empty?
            Collaborators = this.Collaborators
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
            Mirror = s.Mirror |> Option.ofNullable |> Option.map PushMirror.OfSerialised
            ProtectedBranches =
                match s.ProtectedBranches with
                | null -> Set.empty
                | a -> a |> Seq.map ProtectedBranch.OfSerialised |> Set.ofSeq
            Collaborators =
                match s.Collaborators with
                | null -> Set.empty
                | l -> Set.ofArray l
        }

    member internal this.ToSerialised () : SerialisedNativeRepo =
        {
            DefaultBranch = this.DefaultBranch
            Private = this.Private |> Option.toNullable
            IgnoreWhitespaceConflicts = this.IgnoreWhitespaceConflicts |> Option.toNullable
            HasPullRequests = this.HasPullRequests |> Option.toNullable
            HasProjects = this.HasProjects |> Option.toNullable
            HasIssues = this.HasIssues |> Option.toNullable
            HasWiki = this.HasWiki |> Option.toNullable
            DefaultMergeStyle =
                match this.DefaultMergeStyle with
                | None -> null
                | Some mergeStyle -> MergeStyle.toString mergeStyle
            DeleteBranchAfterMerge = this.DeleteBranchAfterMerge |> Option.toNullable
            AllowSquashMerge = this.AllowSquashMerge |> Option.toNullable
            AllowRebaseUpdate = this.AllowRebaseUpdate |> Option.toNullable
            AllowRebase = this.AllowRebase |> Option.toNullable
            AllowRebaseExplicit = this.AllowRebaseExplicit |> Option.toNullable
            AllowMergeCommits = this.AllowMergeCommits |> Option.toNullable
            Mirror =
                match this.Mirror with
                | None -> Nullable ()
                | Some mirror -> Nullable (mirror.ToSerialised ())
            ProtectedBranches = this.ProtectedBranches |> Seq.map (fun b -> b.ToSerialised ()) |> Array.ofSeq
            Collaborators = Set.toArray this.Collaborators
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

    member internal this.ToSerialised () : SerialisedGitHubRepo =
        {
            Uri = (this.Uri : Uri).ToString ()
            MirrorInterval = this.MirrorInterval
        }

type Repo =
    {
        Description : string
        Deleted : bool option
        GitHub : GitHubRepo option
        Native : NativeRepo option
    }

    member this.OverrideDefaults () =
        { this with
            Native = this.Native |> Option.map (fun s -> s.OverrideDefaults ())
        }

    static member Render (client : IGiteaClient) (u : Gitea.Repository) : Repo Async =
        if u.Mirror = Some true && not (String.IsNullOrEmpty u.OriginalUrl) then
            {
                Description = u.Description
                GitHub =
                    {
                        Uri = Uri u.OriginalUrl
                        MirrorInterval = u.MirrorInterval
                    }
                    |> Some
                Native = None
                Deleted = None
            }
            |> async.Return
        else
            async {
                let! mirror =
                    getAllPaginated (fun page count ->
                        client.RepoListPushMirrors (u.Owner.LoginName, u.FullName, Some page, Some count)
                    )

                let mirror =
                    if mirror.Length = 0 then None
                    elif mirror.Length = 1 then Some mirror.[0]
                    else failwith "Multiple mirrors not supported yet"

                let! (branchProtections : Gitea.BranchProtection[]) =
                    client.RepoListBranchProtection (u.Owner.LoginName, u.FullName)
                    |> Async.AwaitTask

                let! (collaborators : Gitea.User[]) =
                    getAllPaginated (fun page count ->
                        client.RepoListCollaborators (u.Owner.LoginName, u.FullName, Some page, Some count)
                    )

                return

                    {
                        Description = u.Description
                        Deleted = None
                        GitHub = None
                        Native =
                            {
                                Private = u.Private
                                DefaultBranch = u.DefaultBranch
                                IgnoreWhitespaceConflicts = u.IgnoreWhitespaceConflicts
                                HasPullRequests = u.HasPullRequests
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
                                Mirror =
                                    mirror
                                    |> Option.map (fun m ->
                                        {
                                            GitHubAddress = Uri m.RemoteAddress
                                        }
                                    )
                                ProtectedBranches =
                                    branchProtections
                                    |> Seq.map (fun bp ->
                                        {
                                            BranchName = bp.BranchName
                                            BlockOnOutdatedBranch = bp.BlockOnOutdatedBranch
                                            RequiredStatusChecks =
                                                if bp.EnableStatusCheck = Some true then
                                                    bp.StatusCheckContexts |> List.ofArray |> Some
                                                else
                                                    None
                                        }
                                    )
                                    |> Set.ofSeq
                                Collaborators = collaborators |> Seq.map (fun user -> user.LoginName) |> Set.ofSeq
                            }
                            |> Some
                    }
            }

    static member internal OfSerialised (s : SerialisedRepo) =
        {
            Repo.Description = s.Description
            GitHub = Option.ofNullable s.GitHub |> Option.map GitHubRepo.OfSerialised
            Native = s.Native |> Option.ofNullable |> Option.map NativeRepo.OfSerialised
            Deleted = s.Deleted |> Option.ofNullable
        }

    member internal this.ToSerialised () : SerialisedRepo =
        {
            Description = this.Description
            GitHub =
                match this.GitHub with
                | None -> Nullable ()
                | Some gitHub -> Nullable (gitHub.ToSerialised ())
            Native =
                match this.Native with
                | None -> Nullable ()
                | Some native -> Nullable (native.ToSerialised ())
            Deleted =
                match this.Deleted with
                | None -> Nullable ()
                | Some v -> Nullable v
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

    member internal this.ToSerialised () : SerialisedUserInfo =
        {
            IsAdmin = this.IsAdmin |> Option.toNullable
            Email = this.Email
            Website = this.Website |> Option.toObj
            Visibility = this.Visibility |> Option.toObj
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

    member internal this.ToSerialised () : SerialisedGiteaConfig =
        {
            Users =
                this.Users
                |> Map.toSeq
                |> Seq.map (fun (user, userInfo) -> KeyValuePair (user, userInfo.ToSerialised ()))
                |> Dictionary
            Repos =
                this.Repos
                |> Map.toSeq
                |> Seq.map (fun (user, repos) ->
                    repos
                    |> Map.toSeq
                    |> Seq.map (fun (repoName, repo) -> KeyValuePair (repoName, repo.ToSerialised ()))
                    |> Dictionary
                    |> fun repos -> KeyValuePair (user, repos)
                )
                |> Dictionary
        }

[<RequireQualifiedAccess>]
module GiteaConfig =
    let get (file : FileInfo) : GiteaConfig =
        let s =
            use reader = new StreamReader (file.OpenRead ())
            reader.ReadToEnd ()

        JsonConvert.DeserializeObject<SerialisedGiteaConfig> s
        |> GiteaConfig.OfSerialised

    let getSchema () : Stream =
        let resource = "Gitea.Declarative.Lib.GiteaConfig.schema.json"
        let assembly = System.Reflection.Assembly.GetExecutingAssembly ()
        let stream = assembly.GetManifestResourceStream resource

        match stream with
        | null -> failwithf "The resource %s was not found. This is a bug in the tool." resource
        | stream -> stream
