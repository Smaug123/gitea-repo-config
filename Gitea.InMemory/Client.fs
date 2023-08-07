namespace Gitea.InMemory

open System
open Gitea.Declarative

type private ServerState =
    {
        Users : Map<User, Gitea.CreateUserOption>
        Repos : (User * Repo) list
    }

    member this.WithUser (create : Gitea.CreateUserOption) : ServerState =
        let user = User create.Username

        match Map.tryFind user this.Users with
        | Some _ ->
            create.Username
            |> failwithf "Behaviour of in-memory Gitea is not defined for adding the same user multiple times. Got: %s"
        | None ->
            { this with
                Users = Map.add user create this.Users
            }

    static member Empty =
        {
            Users = Map.empty
            Repos = []
        }

type private ServerMessage = | AddUser of Gitea.CreateUserOption * AsyncReplyChannel<unit>

type Server =
    private
    | Server of MailboxProcessor<ServerMessage>

    interface IDisposable with
        member this.Dispose () =
            match this with
            | Server t -> t.Dispose ()

[<RequireQualifiedAccess>]
module Client =

    let rec private loop (state : ServerState) (mailbox : MailboxProcessor<ServerMessage>) : Async<unit> =
        async {
            let! message = mailbox.Receive ()

            match message with
            | ServerMessage.AddUser (user, reply) ->
                reply.Reply ()
                return! loop (state.WithUser user) mailbox
        }

    let make () : Server * IGiteaClient =
        let server = MailboxProcessor.Start (loop ServerState.Empty)

        let client =
            { new IGiteaClient with
                member _.AdminGetAllUsers (page, limit) = failwith "Not implemented"

                member _.AdminCreateUser createUserOption =
                    async {
                        let! () = server.PostAndAsyncReply (fun reply -> AddUser (createUserOption, reply))
                        return Operations.createdUser createUserOption
                    }
                    |> Async.StartAsTask

                member _.AdminDeleteUser user = failwith "Not implemented"

                member _.AdminEditUser (user, editUserOption) = failwith "Not implemented"

                member _.AdminCreateRepo (user, createRepoOption) = failwith "Not implemented"

                member _.UserListRepos (user, page, count) = failwith "Not implemented"

                member _.RepoAddPushMirror (user, repo, createPushMirrorOption) = failwith "Not implemented"

                member _.RepoDeletePushMirror (user, repo, remoteName) = failwith "Not implemented"

                member _.RepoListPushMirrors (loginName, userName, page, count) = failwith "Not implemented"

                member _.RepoListBranchProtection (loginName, userName) = failwith "Not implemented"

                member _.RepoDeleteBranchProtection (user, repo, branch) = failwith "Not implemented"

                member _.RepoCreateBranchProtection (user, repo, createBranchProtectionOption) =
                    failwith "Not implemented"

                member _.RepoEditBranchProtection (user, repo, branch, editBranchProtectionOption) =
                    failwith "Not implemented"

                member _.RepoMigrate migrateRepoOptions = failwith "Not implemented"

                member _.RepoGet (user, repo) = failwith "Not implemented"

                member _.RepoDelete (user, repo) = failwith "Not implemented"

                member _.RepoEdit (user, repo, editRepoOption) = failwith "Not implemented"

                member _.RepoListCollaborators (loginName, userName, page, count) = failwith "Not implemented"

                member _.RepoAddCollaborator (user, repo, collaborator) = failwith "Not implemented"

                member _.RepoDeleteCollaborator (user, repo, collaborator) = failwith "Not implemented"
            }

        Server server, client
