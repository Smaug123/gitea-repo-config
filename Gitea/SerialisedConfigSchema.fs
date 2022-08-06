namespace Gitea

open System
open System.ComponentModel
open Newtonsoft.Json

[<RequireQualifiedAccess>]
[<Struct>]
[<Description "Information about a repo that is to be created on Gitea without syncing from GitHub.">]
type internal SerialisedNativeRepo =
    {
        [<Description "The default branch name for this repository, e.g. 'main'">]
        [<JsonProperty(Required = Required.Always)>]
        DefaultBranch : string
        [<Description "Whether this repository is a Gitea private repo">]
        [<JsonProperty(Required = Required.DisallowNull)>]
        Private : Nullable<bool>
    }

[<RequireQualifiedAccess>]
[<CLIMutable>]
type internal SerialisedRepo =
    {
        [<JsonProperty(Required = Required.Always)>]
        [<Description "The text that will accompany this repository in the Gitea UI">]
        Description : string
        [<Description "If this repo is to sync from GitHub, the URI (e.g. 'https://github.com/Smaug123/nix-maui')">]
        [<JsonProperty(Required = Required.Default)>]
        GitHub : Uri
        [<Description "If this repo is to be created natively on Gitea, the information about the repo.">]
        [<JsonProperty(Required = Required.Default)>]
        Native : Nullable<SerialisedNativeRepo>
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
        Users : Map<User, SerialisedUserInfo>
        [<JsonProperty(Required = Required.Always)>]
        Repos : Map<User, Map<RepoName, SerialisedRepo>>
    }
