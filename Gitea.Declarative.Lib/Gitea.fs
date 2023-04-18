namespace Gitea.Declarative

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
                    let desiredRepos = desiredRepos |> Map.map (fun _ v -> v.OverrideDefaults ())

                    async {
                        let! repos =
                            Array.getPaginated (fun page count ->
                                client.UserListRepos (user, Some page, Some count) |> Async.AwaitTask
                            )

                        let! actualRepos =
                            repos
                            |> Seq.map (fun repo ->
                                async {
                                    let! rendered = Repo.Render client repo
                                    return RepoName repo.Name, rendered
                                }
                            )
                            |> Async.Parallel

                        let actualRepos = Map.ofArray actualRepos

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
                        logger.LogDebug ("Creating {User}:{Repo}", user, r)

                        match desired.GitHub, desired.Native with
                        | None, Some native ->
                            let options = Gitea.CreateRepoOption ()
                            options.Description <- desired.Description
                            options.Name <- r
                            options.Private <- native.Private
                            options.DefaultBranch <- native.DefaultBranch

                            let! result = client.AdminCreateRepo (user, options) |> Async.AwaitTask |> Async.Catch

                            match result with
                            | Choice2Of2 e -> raise (AggregateException ($"Error creating {user}:{r}", e))
                            | Choice1Of2 _ -> ()

                            match native.Mirror, githubApiToken with
                            | None, _ -> ()
                            | Some mirror, None -> failwith "Cannot push to GitHub mirror with an API key"
                            | Some mirror, Some token ->
                                logger.LogInformation ("Setting up push mirror for {User}:{Repo}", user, r)
                                let options = Gitea.CreatePushMirrorOption ()
                                options.SyncOnCommit <- Some true
                                options.RemoteAddress <- (mirror.GitHubAddress : Uri).ToString ()
                                options.RemoteUsername <- token
                                options.RemotePassword <- token
                                options.Interval <- "8h0m0s"

                                let! mirrors = getAllPushMirrors client user r

                                match mirrors |> Array.tryFind (fun m -> m.RemoteAddress = options.RemoteAddress) with
                                | None ->
                                    let! _ = client.RepoAddPushMirror (user, r, options) |> Async.AwaitTask
                                    ()
                                | Some existing ->
                                    if existing.SyncOnCommit <> Some true then
                                        failwith $"sync on commit should have been true for {user}:{r}"

                                ()
                        | Some github, None ->
                            let options = Gitea.MigrateRepoOptions ()
                            options.Description <- desired.Description
                            options.Mirror <- Some true
                            options.RepoName <- r
                            options.RepoOwner <- user
                            options.CloneAddr <- string<Uri> github.Uri
                            options.Issues <- Some true
                            options.Labels <- Some true
                            options.Lfs <- Some true
                            options.Milestones <- Some true
                            options.Releases <- Some true
                            options.Wiki <- Some true
                            options.PullRequests <- Some true
                            // TODO - migrate private status
                            githubApiToken |> Option.iter (fun t -> options.AuthToken <- t)

                            let! result = client.RepoMigrate options |> Async.AwaitTask |> Async.Catch

                            match result with
                            | Choice2Of2 e -> raise (AggregateException ($"Error migrating {user}:{r}", e))
                            | Choice1Of2 _ -> ()
                        | None, None -> failwith $"You must supply exactly one of Native or GitHub for {user}:{r}."
                        | Some _, Some _ ->
                            failwith $"Repo {user}:{r} has both Native and GitHub set; you must set exactly one."

                        logger.LogInformation ("Created repo {User}: {Repo}", user, r)
                        return ()
                    }
                | AlignmentError.UnexpectedlyPresent ->
                    async {
                        logger.LogError (
                            "For safety, refusing to delete unexpectedly present repo: {User}, {Repo}",
                            user,
                            r
                        )
                    }
                | AlignmentError.ConfigurationDiffers (desired, actual) ->
                    match desired.GitHub, actual.GitHub with
                    | None, Some gitHub ->
                        async {
                            logger.LogCritical (
                                "Unable to reconcile the desire to move a repo from GitHub-based to Gitea-based. This feature is not exposed on the Gitea API. You must manually convert the following repo to a normal repository first: {User}:{Repo}.",
                                user,
                                r
                            )
                        }
                    | Some _, None ->
                        async {
                            logger.LogError (
                                "Unable to reconcile the desire to move a repo from Gitea-based to GitHub-based: {User}:{Repo}.",
                                user,
                                r
                            )
                        }
                    | Some desiredGitHub, Some actualGitHub ->
                        async {
                            let mutable hasChanged = false
                            let options = Gitea.EditRepoOption ()

                            if desiredGitHub.Uri <> actualGitHub.Uri then
                                logger.LogError (
                                    "Refusing to migrate repo {User}:{Repo} to a different GitHub URL. Desired: {DesiredUrl}. Actual: {ActualUrl}.",
                                    user,
                                    r,
                                    desiredGitHub.Uri,
                                    actualGitHub.Uri
                                )

                            if desiredGitHub.MirrorInterval <> actualGitHub.MirrorInterval then
                                logger.LogDebug ("On {User}:{Repo}, setting {Property}", user, r, "MirrorInterval")
                                options.MirrorInterval <- desiredGitHub.MirrorInterval
                                hasChanged <- true

                            if desired.Description <> actual.Description then
                                logger.LogDebug ("On {User}:{Repo}, setting {Property}", user, r, "Description")
                                options.Description <- desired.Description
                                hasChanged <- true

                            if hasChanged then
                                let! result = client.RepoEdit (user, r, options) |> Async.AwaitTask
                                return ()
                        }
                    | None, None ->

                    async {
                        let mutable hasChanged = false
                        let options = Gitea.EditRepoOption ()

                        if desired.Description <> actual.Description then
                            options.Description <- desired.Description
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "Description")
                            hasChanged <- true

                        let desired =
                            match desired.Native with
                            | None ->
                                failwith
                                    $"Expected a native section of desired for {user}:{r} since there was no GitHub, but got None"
                            | Some n -> n

                        let actual =
                            match actual.Native with
                            | None ->
                                failwith
                                    $"Expected a native section of actual for {user}:{r} since there was no GitHub, but got None"
                            | Some n -> n

                        if desired.Private <> actual.Private then
                            options.Private <- desired.Private
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "Private")
                            hasChanged <- true

                        if desired.AllowRebase <> actual.AllowRebase then
                            options.AllowRebase <- desired.AllowRebase
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "AllowRebase")
                            hasChanged <- true

                        if desired.DefaultBranch <> actual.DefaultBranch then
                            options.DefaultBranch <- desired.DefaultBranch

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "DefaultBranch"
                            )

                            hasChanged <- true

                        if desired.HasIssues <> actual.HasIssues then
                            options.HasIssues <- desired.HasIssues
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "HasIssues")
                            hasChanged <- true

                        if desired.HasProjects <> actual.HasProjects then
                            options.HasProjects <- desired.HasProjects
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "HasProjects")
                            hasChanged <- true

                        if desired.HasWiki <> actual.HasWiki then
                            options.HasWiki <- desired.HasWiki
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "HasWiki")
                            hasChanged <- true

                        if desired.HasPullRequests <> actual.HasPullRequests then
                            options.HasPullRequests <- desired.HasPullRequests

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "HasPullRequests"
                            )

                            hasChanged <- true

                        if desired.AllowMergeCommits <> actual.AllowMergeCommits then
                            options.AllowMergeCommits <- desired.AllowMergeCommits

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "AllowMergeCommits"
                            )

                            hasChanged <- true

                        if desired.AllowRebaseExplicit <> actual.AllowRebaseExplicit then
                            options.AllowRebaseExplicit <- desired.AllowRebaseExplicit

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "AllowRebaseExplicit"
                            )

                            hasChanged <- true

                        if desired.AllowRebase <> actual.AllowRebase then
                            options.AllowRebase <- desired.AllowRebase
                            logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, r, "AllowRebase")
                            hasChanged <- true

                        if desired.AllowRebaseUpdate <> actual.AllowRebaseUpdate then
                            options.AllowRebaseUpdate <- desired.AllowRebaseUpdate

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "AllowRebaseUpdate"
                            )

                            hasChanged <- true

                        if desired.AllowSquashMerge <> actual.AllowSquashMerge then
                            options.AllowSquashMerge <- desired.AllowSquashMerge

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "AllowSquashMerge"
                            )

                            hasChanged <- true

                        if desired.DefaultMergeStyle <> actual.DefaultMergeStyle then
                            options.DefaultMergeStyle <-
                                desired.DefaultMergeStyle |> Option.map MergeStyle.toString |> Option.toObj

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "DefaultMergeStyle"
                            )

                            hasChanged <- true

                        if desired.IgnoreWhitespaceConflicts <> actual.IgnoreWhitespaceConflicts then
                            options.IgnoreWhitespaceConflicts <- desired.IgnoreWhitespaceConflicts

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "IgnoreWhitespaceConflicts"
                            )

                            hasChanged <- true

                        if desired.DeleteBranchAfterMerge <> actual.DeleteBranchAfterMerge then
                            options.DefaultDeleteBranchAfterMerge <- desired.DeleteBranchAfterMerge

                            logger.LogDebug (
                                "On {User}:{Repo}, will set {Property} property",
                                user,
                                r,
                                "DeleteBranchAfterMerge"
                            )

                            hasChanged <- true

                        do!
                            if hasChanged then
                                logger.LogInformation ("Editing repo {User}:{Repo}", user, r)
                                client.RepoEdit (user, r, options) |> Async.AwaitTask |> Async.Ignore
                            else
                                async.Return ()

                        do!
                            match desired.Mirror, actual.Mirror with
                            | None, None -> async.Return ()
                            | None, Some m ->
                                async {
                                    logger.LogError ("Refusing to delete push mirror for {User}:{Repo}", user, r)
                                }
                            | Some desired, None ->
                                match githubApiToken with
                                | None ->
                                    async {
                                        logger.LogCritical (
                                            "Cannot add push mirror for {User}:{Repo} due to lack of GitHub API token",
                                            user,
                                            r
                                        )
                                    }
                                | Some token ->
                                    async {
                                        logger.LogInformation ("Setting up push mirror on {User}:{Repo}", user, r)
                                        let options = Gitea.CreatePushMirrorOption ()
                                        options.SyncOnCommit <- Some true
                                        options.RemoteAddress <- (desired.GitHubAddress : Uri).ToString ()
                                        options.RemoteUsername <- token
                                        options.RemotePassword <- token
                                        options.Interval <- "8h0m0s"
                                        let! _ = client.RepoAddPushMirror (user, r, options) |> Async.AwaitTask
                                        return ()
                                    }
                            | Some desired, Some actual ->
                                if desired <> actual then
                                    async { logger.LogCritical ("Push mirror on {User}:{Repo} differs.", user, r) }
                                else
                                    async.Return ()

                        do!
                            // TODO: lift this out to a function and then put it into the new-repo flow too
                            let extraActualProtected =
                                Set.difference actual.ProtectedBranches desired.ProtectedBranches

                            let extraDesiredProtected =
                                Set.difference desired.ProtectedBranches actual.ProtectedBranches

                            Seq.append
                                (Seq.map Choice1Of2 extraActualProtected)
                                (Seq.map Choice2Of2 extraDesiredProtected)
                            |> Seq.groupBy (fun b ->
                                match b with
                                | Choice1Of2 b -> b.BranchName
                                | Choice2Of2 b -> b.BranchName
                            )
                            |> Seq.map (fun (key, values) ->
                                match Seq.toList values with
                                | [] -> failwith "can't have appeared no times in a groupBy"
                                | [ Choice1Of2 x ] ->
                                    // This is an extra rule; delete it
                                    async {
                                        logger.LogInformation (
                                            "Deleting branch protection rule {BranchProtection} on {User}:{Repo}",
                                            x.BranchName,
                                            user,
                                            r
                                        )

                                        let! _ =
                                            client.RepoDeleteBranchProtection (user, r, x.BranchName)
                                            |> Async.AwaitTask

                                        return ()
                                    }
                                | [ Choice2Of2 y ] ->
                                    // This is an absent rule; add it
                                    async {
                                        logger.LogInformation (
                                            "Creating branch protection rule {BranchProtection} on {User}:{Repo}",
                                            y.BranchName,
                                            user,
                                            r
                                        )

                                        let s = Gitea.CreateBranchProtectionOption ()
                                        s.BranchName <- y.BranchName
                                        s.RuleName <- y.BranchName
                                        s.BlockOnOutdatedBranch <- y.BlockOnOutdatedBranch
                                        let! _ = client.RepoCreateBranchProtection (user, r, s) |> Async.AwaitTask
                                        return ()
                                    }
                                | [ Choice1Of2 x ; Choice2Of2 y ]
                                | [ Choice2Of2 y ; Choice1Of2 x ] ->
                                    // Need to reconcile the two; the Choice2Of2 is what we want to keep
                                    async {
                                        logger.LogInformation (
                                            "Reconciling branch protection rule {BranchProtection} on {User}:{Repo}",
                                            y.BranchName,
                                            user,
                                            r
                                        )

                                        let s = Gitea.EditBranchProtectionOption ()
                                        s.BlockOnOutdatedBranch <- y.BlockOnOutdatedBranch

                                        let! _ =
                                            client.RepoEditBranchProtection (user, r, y.BranchName, s)
                                            |> Async.AwaitTask

                                        return ()
                                    }
                                | [ Choice1Of2 _ ; Choice1Of2 _ ]
                                | [ Choice2Of2 _ ; Choice2Of2 _ ] ->
                                    failwith "can't have the same choice appearing twice"
                                | _ :: _ :: _ :: _ -> failwith "can't have appeared three times"
                            )
                            |> Async.Parallel
                            |> Async.map (Array.iter id)
                    }
            )
        )
        |> Async.Parallel
        |> fun a -> async.Bind (a, Array.iter id >> async.Return)

    let reconcileUserErrors
        (log : ILogger)
        (getUserInput : string -> string)
        (client : Gitea.Client)
        (m : Map<User, AlignmentError<UserInfo>>)
        =
        let userInputLock = obj ()

        m
        |> Map.toSeq
        |> Seq.map (fun (User user, err) ->
            match err with
            | AlignmentError.DoesNotExist desired ->
                async {
                    log.LogDebug ("Creating {User}", user)
                    let rand = Random ()

                    let pwd =
                        Array.init 15 (fun _ -> rand.Next (65, 65 + 25) |> byte)
                        |> System.Text.Encoding.ASCII.GetString

                    let options = Gitea.CreateUserOption ()
                    options.Email <- desired.Email
                    options.Username <- user
                    options.FullName <- user

                    options.Visibility <-
                        match desired.Visibility with
                        | None -> "public"
                        | Some v -> v

                    options.LoginName <- user
                    options.MustChangePassword <- Some true
                    options.Password <- pwd
                    let! _ = client.AdminCreateUser options |> Async.AwaitTask

                    lock
                        userInputLock
                        (fun () ->
                            log.LogCritical (
                                "Created user {User} with password {Password}, which you must now change",
                                user,
                                pwd
                            )
                        )

                    return ()
                }
            | AlignmentError.UnexpectedlyPresent ->
                async {
                    lock
                        userInputLock
                        (fun () ->
                            let answer =
                                UserInput.getDefaultNo getUserInput $"User %s{user} unexpectedly present. Remove?"

                            if answer then
                                client.AdminDeleteUser(user).Result
                            else
                                log.LogCritical ("Refusing to delete user {User}, who is unexpectedly present.", user)
                        )
                }
            | AlignmentError.ConfigurationDiffers (desired, actual) ->
                let updates = UserInfo.Resolve desired actual

                async {
                    lock
                        userInputLock
                        (fun () ->
                            let body = Gitea.EditUserOption ()

                            for update in updates do
                                match update with
                                | UserInfoUpdate.Admin (desired, _) ->
                                    log.LogDebug ("Editing {User}, property {Property}", user, "Admin")
                                    body.Admin <- desired
                                | UserInfoUpdate.Email (desired, _) ->
                                    log.LogDebug ("Editing {User}, property {Property}", user, "Email")
                                    body.Email <- desired
                                | UserInfoUpdate.Visibility (desired, _) ->
                                    log.LogDebug ("Editing {User}, property {Property}", user, "Visibility")
                                    body.Visibility <- desired
                                | UserInfoUpdate.Website (desired, actual) ->
                                    // Per https://github.com/go-gitea/gitea/issues/17126,
                                    // the website parameter can't currently be edited.
                                    // This is a bug that is unlikely to be fixed.
                                    let actual =
                                        match actual with
                                        | None -> "<no website>"
                                        | Some uri -> uri.ToString ()

                                    log.LogCritical (
                                        "User {User} has conflicting website, desired {DesiredWebsite}, existing {ActualWebsite}, which a bug in Gitea means can't be reconciled via the API.",
                                        user,
                                        desired,
                                        actual
                                    )

                            body.LoginName <- user
                            client.AdminEditUser(user, body).Result |> ignore
                        )
                }
        )
        |> Async.Parallel
        |> fun a -> async.Bind (a, Array.iter id >> async.Return)
