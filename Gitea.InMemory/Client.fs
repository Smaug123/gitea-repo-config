namespace Gitea.InMemory

open System
open Gitea.Declarative

type private ServerState =
    {
        Users : Map<User, GiteaClient.CreateUserOption>
        Repos : (User * Repo) list
    }

    member this.WithUser (create : GiteaClient.CreateUserOption) : ServerState =
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

type private ServerMessage = | AddUser of GiteaClient.CreateUserOption * AsyncReplyChannel<unit>

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

    let make () : Server * GiteaClient.IGiteaClient =
        let server = MailboxProcessor.Start (loop ServerState.Empty)

        let client =
            { GiteaClient.GiteaClientMock.Empty with
                AdminCreateUser =
                    fun (createUserOption, _ct) ->
                        async {
                            let! () = server.PostAndAsyncReply (fun reply -> AddUser (createUserOption, reply))
                            return Operations.createdUser createUserOption
                        }
                        |> Async.StartAsTask
            }

        Server server, client
