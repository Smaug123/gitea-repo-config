namespace Gitea.Declarative

open System.Threading.Tasks

type IGiteaClient =
    abstract AdminGetAllUsers : page : int64 option * limit : int64 option -> Gitea.User array Task
    abstract AdminCreateUser : Gitea.CreateUserOption -> Gitea.User Task
    abstract AdminDeleteUser : user : string -> unit Task
    abstract AdminEditUser : user : string * Gitea.EditUserOption -> Gitea.User Task
    abstract AdminCreateRepo : user : string * Gitea.CreateRepoOption -> Gitea.Repository Task

    abstract UserListRepos : string * page : int64 option * count : int64 option -> Gitea.Repository array Task

    abstract RepoAddPushMirror : user : string * repo : string * Gitea.CreatePushMirrorOption -> Gitea.PushMirror Task

    abstract RepoListPushMirrors :
        loginName : string * userName : string * page : int64 option * count : int64 option ->
            Gitea.PushMirror array Task

    abstract RepoListBranchProtection : loginName : string * userName : string -> Gitea.BranchProtection array Task
    abstract RepoDeleteBranchProtection : user : string * repo : string * branch : string -> unit Task

    abstract RepoCreateBranchProtection :
        user : string * repo : string * Gitea.CreateBranchProtectionOption -> Gitea.BranchProtection Task

    abstract RepoEditBranchProtection :
        user : string * repo : string * branch : string * Gitea.EditBranchProtectionOption ->
            Gitea.BranchProtection Task

    abstract RepoMigrate : Gitea.MigrateRepoOptions -> Gitea.Repository Task
    abstract RepoGet : user : string * repo : string -> Gitea.Repository Task
    abstract RepoDelete : user : string * repo : string -> unit Task
    abstract RepoEdit : user : string * repo : string * Gitea.EditRepoOption -> Gitea.Repository Task

    abstract RepoListCollaborators :
        loginName : string * userName : string * page : int64 option * count : int64 option -> Gitea.User array Task

    abstract RepoAddCollaborator : user : string * repo : string * collaborator : string -> unit Task
    abstract RepoDeleteCollaborator : user : string * repo : string * collaborator : string -> unit Task

[<RequireQualifiedAccess>]
module IGiteaClient =
    let fromReal (client : Gitea.Client) : IGiteaClient =
        { new IGiteaClient with
            member _.AdminGetAllUsers (page, limit) = client.AdminGetAllUsers (page, limit)
            member _.AdminCreateUser user = client.AdminCreateUser user
            member _.AdminDeleteUser user = client.AdminDeleteUser user
            member _.AdminEditUser (user, option) = client.AdminEditUser (user, option)
            member _.AdminCreateRepo (user, option) = client.AdminCreateRepo (user, option)

            member _.UserListRepos (user, page, count) =
                client.UserListRepos (user, page, count)

            member _.RepoAddPushMirror (user, repo, options) =
                client.RepoAddPushMirror (user, repo, options)

            member _.RepoListPushMirrors (loginName, userName, page, count) =
                client.RepoListPushMirrors (loginName, userName, page, count)

            member _.RepoListBranchProtection (login, user) =
                client.RepoListBranchProtection (login, user)

            member _.RepoDeleteBranchProtection (user, repo, branch) =
                client.RepoDeleteBranchProtection (user, repo, branch)

            member _.RepoCreateBranchProtection (user, repo, options) =
                client.RepoCreateBranchProtection (user, repo, options)

            member _.RepoEditBranchProtection (user, repo, branch, edit) =
                client.RepoEditBranchProtection (user, repo, branch, edit)

            member _.RepoMigrate options = client.RepoMigrate options
            member _.RepoGet (user, repo) = client.RepoGet (user, repo)
            member _.RepoDelete (user, repo) = client.RepoDelete (user, repo)
            member _.RepoEdit (user, repo, options) = client.RepoEdit (user, repo, options)

            member _.RepoListCollaborators (login, user, page, count) =
                client.RepoListCollaborators (login, user, page, count)

            member _.RepoAddCollaborator (user, repo, collaborator) =
                client.RepoAddCollaborator (user, repo, collaborator)

            member _.RepoDeleteCollaborator (user, repo, collaborator) =
                client.RepoDeleteCollaborator (user, repo, collaborator)
        }
