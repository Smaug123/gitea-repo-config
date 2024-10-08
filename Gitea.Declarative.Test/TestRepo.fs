namespace Gitea.Declarative.Test

open System
open System.Threading.Tasks
open Gitea.Declarative
open Gitea.InMemory
open Microsoft.Extensions.Logging.Abstractions
open NUnit.Framework
open FsUnitTyped
open FsCheck

[<TestFixture>]
module TestRepo =

    [<Test>]
    let ``We refuse to delete a repo if we get to Reconcile without positive confirmation`` () =
        let property (gitHubToken : string option) =
            let client = GiteaClient.GiteaClientMock.Empty

            let lf, messages = LoggerFactory.makeTest ()
            let logger = lf.CreateLogger "test"

            [
                User "username", Map.ofList [ RepoName "repo", AlignmentError.UnexpectedlyPresent ]
            ]
            |> Map.ofList
            |> Gitea.reconcileRepoErrors logger client gitHubToken
            |> Async.RunSynchronously

            messages ()
            |> List.filter (fun s -> s.Contains ("refusing to delete", StringComparison.OrdinalIgnoreCase))
            |> List.length
            |> shouldEqual 1

        Check.QuickThrowOnFailure property

    [<Test>]
    let ``We refuse to delete repos when they're not configured to be deleted`` () =
        Arb.register<CustomArb> () |> ignore

        let property
            (user : User)
            (repos : Map<RepoName, Repo>)
            (userInfo : UserInfo)
            (repo : GiteaClient.Repository)
            (reposToReturn : GiteaClient.Repository list)
            =
            let reposToReturn = repo :: reposToReturn

            let reposToReturn =
                if reposToReturn.Length >= 5 then
                    reposToReturn.[0..3]
                else
                    reposToReturn

            for repo in reposToReturn do
                match repo.Name with
                | None -> failwith "generator should have put a name on every repo"
                | Some _ -> ()

            let lf, messages = LoggerFactory.makeTest ()
            let logger = lf.CreateLogger "test"

            let client =
                { GiteaClient.GiteaClientMock.Empty with
                    UserListRepos =
                        fun (_username, _page, _limit, ct) ->
                            async {
                                return
                                    reposToReturn
                                    |> List.filter (fun r -> not (repos.ContainsKey (RepoName (Option.get r.Name))))
                            }
                            |> fun a -> Async.StartAsTask (a, ?cancellationToken = ct)

                    RepoListPushMirrors =
                        fun (_, _, _, _, ct) ->
                            async { return [] } |> fun a -> Async.StartAsTask (a, ?cancellationToken = ct)

                    RepoListBranchProtection =
                        fun (_, _, ct) -> async { return [] } |> fun a -> Async.StartAsTask (a, ?cancellationToken = ct)

                    RepoListCollaborators =
                        fun (_, _, _, _, ct) ->
                            async { return [] } |> fun a -> Async.StartAsTask (a, ?cancellationToken = ct)
                }

            let config : GiteaConfig =
                {
                    Users = Map.ofList [ user, userInfo ]
                    Repos =
                        let repos =
                            repos
                            |> Map.map (fun _ r ->
                                { r with
                                    Deleted =
                                        match r.Deleted with
                                        | Some true -> Some false
                                        | _ -> None
                                }
                            )

                        [ user, repos ] |> Map.ofList
                }

            let recoveredUser, error =
                Gitea.checkRepos logger config client
                |> Async.RunSynchronously
                |> Result.getError
                |> Map.toSeq
                |> Seq.exactlyOne

            recoveredUser |> shouldEqual user

            for repoName, _configuredRepo in Map.toSeq repos do
                match Map.tryFind repoName error with
                | Some (AlignmentError.DoesNotExist _) -> ()
                | a -> failwithf "Failed: %+A" a

            let messages = messages ()
            messages |> shouldEqual []

        Check.QuickThrowOnFailure property

    [<Test>]
    let ``We point out when repos have been deleted`` () =
        Arb.register<CustomArb> () |> ignore

        let property (user : User) (repos : Map<RepoName, Repo>) (userInfo : UserInfo) =

            let lf, messages = LoggerFactory.makeTest ()
            let logger = lf.CreateLogger "test"

            let client =
                { GiteaClient.GiteaClientMock.Empty with
                    UserListRepos = fun _ -> Task.FromResult []

                    RepoListPushMirrors = fun _ -> async { return [] } |> Async.StartAsTask

                    RepoListBranchProtection = fun _ -> async { return [] } |> Async.StartAsTask

                    RepoListCollaborators = fun _ -> async { return [] } |> Async.StartAsTask
                }

            let config : GiteaConfig =
                {
                    Users = Map.ofList [ user, userInfo ]
                    Repos =
                        let repos =
                            repos
                            |> Map.map (fun _ r ->
                                { r with
                                    Deleted = Some true
                                }
                            )

                        [ user, repos ] |> Map.ofList
                }

            Gitea.checkRepos logger config client |> Async.RunSynchronously |> Result.get

            for message in messages () do
                message.Contains ("Remove this repo from configuration", StringComparison.OrdinalIgnoreCase)
                |> shouldEqual true

        Check.QuickThrowOnFailure property

    [<Test>]
    let ``We decide to delete repos which are configured to Deleted = true`` () =
        Arb.register<CustomArb> () |> ignore

        let property
            (user : User)
            (oneExistingRepoName : RepoName)
            (oneExistingRepo : Repo)
            (existingRepos : Map<RepoName, Repo>)
            (userInfo : UserInfo)
            =

            let existingRepos = existingRepos |> Map.add oneExistingRepoName oneExistingRepo

            let giteaUser = Types.emptyUser (user.ToString ())

            let client =
                { GiteaClient.GiteaClientMock.Empty with
                    UserListRepos =
                        fun _ ->
                            async {
                                return
                                    existingRepos
                                    |> Map.toSeq
                                    |> Seq.map (fun (RepoName repoName, _repoSpec) ->
                                        { Types.emptyRepo repoName "main" with
                                            Name = Some repoName
                                            Owner = Some giteaUser
                                        }
                                    )
                                    |> Seq.toList
                            }
                            |> Async.StartAsTask

                    RepoListPushMirrors = fun _ -> async { return [] } |> Async.StartAsTask

                    RepoListBranchProtection = fun _ -> async { return [] } |> Async.StartAsTask

                    RepoListCollaborators = fun _ -> async { return [] } |> Async.StartAsTask
                }

            let config : GiteaConfig =
                {
                    Users = Map.ofList [ user, userInfo ]
                    Repos =
                        let repos =
                            existingRepos
                            |> Map.map (fun _ r ->
                                { r with
                                    Deleted = Some true
                                }
                            )

                        [ user, repos ] |> Map.ofList
                }

            let recoveredUser, errors =
                Gitea.checkRepos NullLogger.Instance config client
                |> Async.RunSynchronously
                |> Result.getError
                |> Map.toSeq
                |> Seq.exactlyOne

            recoveredUser |> shouldEqual user

            existingRepos.Keys |> shouldEqual errors.Keys

            for _repo, config in Map.toSeq errors do
                match config with
                | AlignmentError.ConfigurationDiffers (desired, _) -> desired.Deleted |> shouldEqual (Some true)
                | a -> failwithf "Unexpected alignment: %+A" a

        Check.QuickThrowOnFailure property

// TODO: test that we delete repos which come up as ConfigurationDiffers (desired.Deleted = Some true)
