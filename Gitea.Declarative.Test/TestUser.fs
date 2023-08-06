namespace Gitea.Declarative.Test

open System
open System.Threading.Tasks
open FsCheck
open Gitea.InMemory
open Gitea.Declarative
open FsUnitTyped
open Microsoft.Extensions.Logging.Abstractions
open NUnit.Framework

[<TestFixture>]
[<RequireQualifiedAccess>]
module TestUser =

    [<Test>]
    let ``We set MustChangePassword on creating a user`` () =
        Arb.register<CustomArb> () |> ignore

        let property (desiredUser : UserInfo) =
            let result = TaskCompletionSource<bool option> ()

            let client =
                { GiteaClientMock.Unimplemented with
                    AdminCreateUser =
                        fun options ->
                            async {
                                result.SetResult options.MustChangePassword
                                return null
                            }
                            |> Async.StartAsTask
                }

            [ User "username", AlignmentError.DoesNotExist desiredUser ]
            |> Map.ofList
            |> Gitea.reconcileUserErrors NullLogger.Instance (fun _ -> failwith "do not ask for user input") client
            |> Async.RunSynchronously

            result.Task.Wait (TimeSpan.FromSeconds 10.0) |> shouldEqual true

            result.Task.Result |> shouldEqual (Some true)

        Check.QuickThrowOnFailure property
