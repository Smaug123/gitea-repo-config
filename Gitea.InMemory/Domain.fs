namespace Gitea.InMemory

open System
open System.Threading.Tasks
open Gitea.Declarative

type BranchName = | BranchName of string

type BranchProtectionRule =
    {
        RequiredChecks : string Set
    }

type NativeRepo =
    {
        BranchProtectionRules : (BranchName * BranchProtectionRule) list
    }

type Repo =
    | GitHubMirror of Uri
    | NativeRepo of NativeRepo

type GiteaState =
    {
        Users : User Set
        Repositories : Map<User * RepoName, Repo>
    }

/// Allows us to use handy record-updating syntax.
/// (I have a considerable dislike of Moq and friends.)
type GiteaClientMock =
    {
        AdminGetAllUsers : int64 option * int64 option -> Gitea.User array Task
        AdminCreateUser : Gitea.CreateUserOption -> Gitea.User Task
        AdminDeleteUser : string -> unit Task
        AdminEditUser : string * Gitea.EditUserOption -> Gitea.User Task
        AdminCreateRepo : string * Gitea.CreateRepoOption -> Gitea.Repository Task

        UserListRepos : string * int64 option * int64 option -> Gitea.Repository array Task

        RepoAddPushMirror : string * string * Gitea.CreatePushMirrorOption -> Gitea.PushMirror Task
        RepoListPushMirrors : string * string * int64 option * int64 option -> Gitea.PushMirror array Task

        RepoListBranchProtection : string * string -> Gitea.BranchProtection array Task
        RepoDeleteBranchProtection : string * string * string -> unit Task
        RepoCreateBranchProtection : string * string * Gitea.CreateBranchProtectionOption -> Gitea.BranchProtection Task
        RepoEditBranchProtection :
            string * string * string * Gitea.EditBranchProtectionOption -> Gitea.BranchProtection Task

        RepoMigrate : Gitea.MigrateRepoOptions -> Gitea.Repository Task
        RepoGet : string * string -> Gitea.Repository Task
        RepoDelete : string * string -> unit Task
        RepoEdit : string * string * Gitea.EditRepoOption -> Gitea.Repository Task

        RepoListCollaborators : string * string * int64 option * int64 option -> Gitea.User array Task
        RepoAddCollaborator : string * string * string -> unit Task
        RepoDeleteCollaborator : string * string * string -> unit Task
    }

    static member Unimplemented =
        {
            AdminGetAllUsers = fun _ -> failwith "Unimplemented"
            AdminCreateUser = fun _ -> failwith "Unimplemented"
            AdminDeleteUser = fun _ -> failwith "Unimplemented"
            AdminEditUser = fun _ -> failwith "Unimplemented"
            AdminCreateRepo = fun _ -> failwith "Unimplemented"

            UserListRepos = fun _ -> failwith "Unimplemented"

            RepoAddPushMirror = fun _ -> failwith "Unimplemented"
            RepoListPushMirrors = fun _ -> failwith "Unimplemented"

            RepoListBranchProtection = fun _ -> failwith "Unimplemented"
            RepoDeleteBranchProtection = fun _ -> failwith "Unimplemented"
            RepoCreateBranchProtection = fun _ -> failwith "Unimplemented"
            RepoEditBranchProtection = fun _ -> failwith "Unimplemented"

            RepoMigrate = fun _ -> failwith "Unimplemented"
            RepoGet = fun _ -> failwith "Unimplemented"
            RepoDelete = fun _ -> failwith "Unimplemented"
            RepoEdit = fun _ -> failwith "Unimplemented"

            RepoListCollaborators = fun _ -> failwith "Unimplemented"
            RepoAddCollaborator = fun _ -> failwith "Unimplemented"
            RepoDeleteCollaborator = fun _ -> failwith "Unimplemented"
        }

    interface IGiteaClient with
        member this.AdminGetAllUsers (page, limit) = this.AdminGetAllUsers (page, limit)
        member this.AdminCreateUser user = this.AdminCreateUser user
        member this.AdminDeleteUser user = this.AdminDeleteUser user
        member this.AdminEditUser (user, option) = this.AdminEditUser (user, option)
        member this.AdminCreateRepo (user, option) = this.AdminCreateRepo (user, option)

        member this.UserListRepos (user, page, count) = this.UserListRepos (user, page, count)

        member this.RepoAddPushMirror (user, repo, options) =
            this.RepoAddPushMirror (user, repo, options)

        member this.RepoListPushMirrors (loginName, userName, page, count) =
            this.RepoListPushMirrors (loginName, userName, page, count)

        member this.RepoListBranchProtection (login, user) =
            this.RepoListBranchProtection (login, user)

        member this.RepoDeleteBranchProtection (user, repo, branch) =
            this.RepoDeleteBranchProtection (user, repo, branch)

        member this.RepoCreateBranchProtection (user, repo, options) =
            this.RepoCreateBranchProtection (user, repo, options)

        member this.RepoEditBranchProtection (user, repo, branch, edit) =
            this.RepoEditBranchProtection (user, repo, branch, edit)

        member this.RepoMigrate options = this.RepoMigrate options
        member this.RepoGet (user, repo) = this.RepoGet (user, repo)
        member this.RepoDelete (user, repo) = this.RepoDelete (user, repo)
        member this.RepoEdit (user, repo, options) = this.RepoEdit (user, repo, options)

        member this.RepoListCollaborators (login, user, page, count) =
            this.RepoListCollaborators (login, user, page, count)

        member this.RepoAddCollaborator (user, repo, collaborator) =
            this.RepoAddCollaborator (user, repo, collaborator)

        member this.RepoDeleteCollaborator (user, repo, collaborator) =
            this.RepoDeleteCollaborator (user, repo, collaborator)
