namespace Gitea.Declarative

open System
open System.IO
open Newtonsoft.Json

type NativeRepo =
    {
        DefaultBranch : string
        Private : bool option
    }

    static member internal OfSerialised (s : SerialisedNativeRepo) =
        {
            NativeRepo.DefaultBranch = s.DefaultBranch
            Private = s.Private |> Option.ofNullable
        }

type Repo =
    {
        Description : string
        GitHub : Uri option
        Native : NativeRepo option
    }

    static member Render (u : Gitea.Repository) : Repo =
        {
            Description = u.Description
            GitHub =
                if String.IsNullOrEmpty u.OriginalUrl then
                    None
                else
                    Some (Uri u.OriginalUrl)
            Native =
                if String.IsNullOrEmpty u.OriginalUrl then
                    {
                        Private = u.Private
                        DefaultBranch = u.DefaultBranch
                    }
                    |> Some
                else
                    None
        }

    static member internal OfSerialised (s : SerialisedRepo) =
        {
            Repo.Description = s.Description
            GitHub = Option.ofObj s.GitHub
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
            GiteaConfig.Users = s.Users |> Map.map (fun _ -> UserInfo.OfSerialised)
            Repos = s.Repos |> Map.map (fun _ -> Map.map (fun _ -> Repo.OfSerialised))
        }

[<RequireQualifiedAccess>]
module GiteaConfig =
    let get (file : FileInfo) : GiteaConfig =
        let s =
            use reader = new StreamReader (file.OpenRead ())
            reader.ReadToEnd ()

        JsonConvert.DeserializeObject<SerialisedGiteaConfig> s
        |> GiteaConfig.OfSerialised
