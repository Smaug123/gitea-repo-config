namespace Gitea.Declarative

open System
open System.Collections.Generic
open System.ComponentModel
open Newtonsoft.Json

type SerialisedMergeStyle = string

[<RequireQualifiedAccess>]
[<Struct>]
[<CLIMutable>]
[<Description "Information about a repo that is to be created on Gitea without syncing from GitHub.">]
type SerialisedPushMirror =
    {
        [<JsonProperty(Required = Required.Always)>]
        GitHubAddress : string
    }

[<RequireQualifiedAccess>]
[<Struct>]
[<CLIMutable>]
[<Description "Information about a repo that is to be created on Gitea without syncing from GitHub.">]
type SerialisedProtectedBranch =
    {
        [<JsonProperty(Required = Required.Always)>]
        BranchName : string
        [<JsonProperty(Required = Required.DisallowNull)>]
        BlockOnOutdatedBranch : Nullable<bool>
        [<JsonProperty(Required = Required.Default)>]
        [<Description "A list of status check patterns; merge into this branch will be blocked unless all these checks have run and passed. (Probably go with alphanumeric strings, I can't find any docs.)">]
        RequiredStatusChecks : string array
        [<JsonProperty(Required = Required.DisallowNull)>]
        IgnoreStaleApprovals : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        EnableForcePush : Nullable<bool>
    }

[<RequireQualifiedAccess>]
[<Struct>]
[<CLIMutable>]
[<Description "Information about a repo that is to be created on Gitea without syncing from GitHub.">]
type internal SerialisedNativeRepo =
    {
        [<Description "The default branch name for this repository, e.g. 'main'">]
        [<JsonProperty(Required = Required.Always)>]
        DefaultBranch : string
        [<Description "Whether this repository is a Gitea private repo">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        Private : Nullable<bool>
        [<Description "either `true` to ignore whitespace for conflicts, or `false` to not ignore whitespace.">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        IgnoreWhitespaceConflicts : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow pull requests, or `false` to prevent pull request.">]
        HasPullRequests : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to enable project unit, or `false` to disable them.">]
        HasProjects : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to enable issues for this repository or `false` to disable them.">]
        HasIssues : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to enable the wiki for this repository or `false` to disable it.">]
        HasWiki : Nullable<bool>
        [<Description "set to a merge style to be used by this repository: \"merge\", \"rebase\", \"rebase-merge\", or \"squash\".">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        DefaultMergeStyle : SerialisedMergeStyle
        [<Description "set to `true` to delete pr branch after merge by default.">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        DeleteBranchAfterMerge : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow squash-merging pull requests, or `false` to prevent squash-merging.">]
        AllowSquashMerge : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow updating pull request branch by rebase, or `false` to prevent it.">]
        AllowRebaseUpdate : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow rebase-merging pull requests, or `false` to prevent rebase-merging.">]
        AllowRebase : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow rebase with explicit merge commits (--no-ff), or `false` to prevent rebase with explicit merge commits.">]
        AllowRebaseExplicit : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "either `true` to allow merging pull requests with a merge commit, or `false` to prevent merging pull requests with merge commits.">]
        AllowMergeCommits : Nullable<bool>
        [<JsonProperty(Required = Required.DisallowNull)>]
        [<Description "Configure GitHub push mirrors to sync this repo to">]
        Mirrors : SerialisedPushMirror[]
        [<JsonProperty(Required = Required.Default)>]
        [<Description "Protected branch configuration">]
        ProtectedBranches : SerialisedProtectedBranch array
        [<JsonProperty(Required = Required.Default)>]
        [<Description "Usernames on this Gitea instance who are collaborators on this repo">]
        Collaborators : string array
    }

[<Struct>]
[<CLIMutable>]
[<Description "Information about a repo that is being mirrored from GitHub.">]
type internal SerialisedGitHubRepo =
    {
        [<Description "e.g. https://github.com/Smaug123/nix-maui">]
        [<JsonProperty(Required = Required.Always)>]
        Uri : string
        [<Description "A Golang string, e.g. \"8h30m0s\"">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        MirrorInterval : string
    }

[<RequireQualifiedAccess>]
[<CLIMutable>]
type internal SerialisedRepo =
    {
        [<JsonProperty(Required = Required.Always)>]
        [<Description "The text that will accompany this repository in the Gitea UI">]
        Description : string
        [<Description "If this repo is to sync from GitHub, information about the repo.">]
        [<JsonProperty(Required = Required.Default)>]
        GitHub : Nullable<SerialisedGitHubRepo>
        [<Description "If this repo is to be created natively on Gitea, the information about the repo.">]
        [<JsonProperty(Required = Required.Default)>]
        Native : Nullable<SerialisedNativeRepo>
        [<Description "Set this to true to delete the repo. We will refuse to delete any repo that is absent from
        configuration; the workflow is to first set its `deleted` to `true` (whereupon we will delete it from Gitea) and then to delete it from configuration.">]
        [<JsonProperty(Required = Required.Default)>]
        Deleted : Nullable<bool>
    }

[<RequireQualifiedAccess>]
[<CLIMutable>]
type internal SerialisedUserInfo =
    {
        [<JsonProperty(Required = Required.DisallowNull)>]
        IsAdmin : Nullable<bool>
        [<JsonProperty(Required = Required.Always)>]
        Email : string
        [<JsonProperty(Required = Required.Default)>]
        Website : Uri
        [<JsonProperty(Required = Required.Default)>]
        Visibility : string
    }

[<RequireQualifiedAccess>]
[<CLIMutable>]
type internal SerialisedGiteaConfig =
    {
        [<JsonProperty(Required = Required.Always)>]
        Users : Dictionary<User, SerialisedUserInfo>
        [<JsonProperty(Required = Required.Always)>]
        Repos : Dictionary<User, Dictionary<RepoName, SerialisedRepo>>
    }
