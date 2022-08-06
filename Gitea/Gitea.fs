namespace Gitea

open System
open Microsoft.Extensions.Logging

type AlignmentError<'a> =
    | UnexpectedlyPresent
    | DoesNotExist of desired : 'a
    | ConfigurationDiffers of desired : 'a * actual : 'a

    override this.ToString () =
        match this with
        | UnexpectedlyPresent -> "Found on Gitea, but was not in configuration."
        | DoesNotExist _ -> "Present in configuration, but absent on Gitea."
        | ConfigurationDiffers (desired, actual) -> $"Differs from config. Desired: {desired}. Actual: {actual}."

[<RequireQualifiedAccess>]
module Gitea =

    let checkUsers
        (config : GiteaConfig)
        (client : Gitea.Client)
        : Async<Result<unit, Map<User, AlignmentError<UserInfo>>>>
        =
        async {
            let desiredUsers = config.Users

            let! actualUsers =
                Array.getPaginated (fun page count ->
                    client.AdminGetAllUsers (Some page, Some count) |> Async.AwaitTask
                )

            let actualUsers =
                actualUsers |> Seq.map (fun u -> User u.Login, UserInfo.Render u) |> Map.ofSeq

            let errors =
                actualUsers
                |> Map.toSeq
                |> Seq.choose (fun (user, actual) ->
                    match Map.tryFind user desiredUsers with
                    | None -> (user, AlignmentError.UnexpectedlyPresent) |> Some
                    | Some desired ->
                        if desired <> actual then
                            (user, AlignmentError.ConfigurationDiffers (desired, actual)) |> Some
                        else
                            None
                )
                |> Map.ofSeq

            let otherErrors =
                desiredUsers
                |> Map.toSeq
                |> Seq.choose (fun (user, desired) ->
                    match Map.tryFind user actualUsers with
                    | None -> (user, AlignmentError.DoesNotExist desired) |> Some
                    | Some actual ->
                        if desired <> actual then
                            (user, AlignmentError.ConfigurationDiffers (desired, actual)) |> Some
                        else
                            None
                )
                |> Map.ofSeq

            let together = Map.union (fun _ x _ -> x) errors otherErrors
            return if together.IsEmpty then Ok () else Error together
        }

    // TODO: check whether mirrors are out of sync e.g. in Public/Private status
    let checkRepos
        (config : GiteaConfig)
        (client : Gitea.Client)
        : Async<Result<unit, Map<User, Map<RepoName, AlignmentError<Repo>>>>>
        =
        async {
            let! errors =
                config.Repos
                |> Map.toSeq
                |> Seq.map (fun (User user as u, desiredRepos) ->
                    async {
                        let! repos =
                            Array.getPaginated (fun page count ->
                                client.UserListRepos (user, Some page, Some count) |> Async.AwaitTask
                            )

                        let actualRepos =
                            repos |> Seq.map (fun repo -> RepoName repo.Name, Repo.Render repo) |> Map.ofSeq

                        let errors1 =
                            actualRepos
                            |> Map.toSeq
                            |> Seq.choose (fun (repo, actual) ->
                                match Map.tryFind repo desiredRepos with
                                | None -> Some (repo, AlignmentError.UnexpectedlyPresent)
                                | Some desired ->
                                    if desired <> actual then
                                        (repo, AlignmentError.ConfigurationDiffers (desired, actual)) |> Some
                                    else
                                        None
                            )
                            |> Map.ofSeq

                        let errors2 =
                            desiredRepos
                            |> Map.toSeq
                            |> Seq.choose (fun (repo, desired) ->
                                match Map.tryFind repo actualRepos with
                                | None -> Some (repo, AlignmentError.DoesNotExist desired)
                                | Some actual ->
                                    if desired <> actual then
                                        (repo, AlignmentError.ConfigurationDiffers (desired, actual)) |> Some
                                    else
                                        None
                            )
                            |> Map.ofSeq

                        return u, Map.union (fun _ v _ -> v) errors1 errors2
                    }
                )
                |> Async.Parallel

            let errors = errors |> Array.filter (fun (_, m) -> not m.IsEmpty)

            return
                if errors.Length = 0 then
                    Ok ()
                else
                    Error (Map.ofArray errors)
        }

    let reconcileRepoErrors
        (logger : ILogger)
        (client : Gitea.Client)
        (githubApiToken : string option)
        (m : Map<User, Map<RepoName, AlignmentError<Repo>>>)
        : Async<unit>
        =
        m
        |> Map.toSeq
        |> Seq.collect (fun (User user, errMap) ->
            errMap
            |> Map.toSeq
            |> Seq.map (fun (RepoName r, err) ->
                match err with
                | AlignmentError.DoesNotExist desired ->
                    async {
                        let! _ =
                            match desired.GitHub, desired.Native with
                            | None, Some native ->
                                let options = Gitea.CreateRepoOption ()
                                options.Description <- desired.Description
                                options.Name <- r
                                options.Private <- native.Private
                                options.DefaultBranch <- native.DefaultBranch

                                try
                                    client.AdminCreateRepo (user, options) |> Async.AwaitTask
                                with e ->
                                    raise (AggregateException ($"Error creating {user}:{r}", e))
                            | Some uri, None ->
                                let options = Gitea.MigrateRepoOptions ()
                                options.Description <- desired.Description
                                options.Mirror <- Some true
                                options.RepoName <- r
                                options.RepoOwner <- user
                                options.CloneAddr <- uri.ToString ()
                                options.Issues <- Some true
                                options.Labels <- Some true
                                options.Lfs <- Some true
                                options.Milestones <- Some true
                                options.Releases <- Some true
                                options.Wiki <- Some true
                                options.PullRequests <- Some true
                                // TODO - migrate private status
                                githubApiToken |> Option.iter (fun t -> options.AuthToken <- t)

                                try
                                    client.RepoMigrate options |> Async.AwaitTask
                                with e ->
                                    raise (AggregateException ($"Error migrating {user}:{r}", e))
                            | None, None ->
                                // TODO: express this in JsonSchema
                                failwith $"You must supply exactly one of Native or GitHub for {user}:{r}."
                            | Some _, Some _ ->
                                failwith $"Repo {user}:{r} has both Native and GitHub set; you must set exactly one."

                        logger.LogInformation ("Created repo {User}: {Repo}", user.ToString (), r.ToString ())
                        return ()
                    }
                | err ->
                    async {
                        logger.LogInformation (
                            "Unable to reconcile: {User}, {Repo}: {Error}",
                            user.ToString (),
                            r.ToString (),
                            err
                        )
                    }
            )
        )
        |> Async.Parallel
        |> fun a -> async.Bind (a, Array.iter id >> async.Return)
