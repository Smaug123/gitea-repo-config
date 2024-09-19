namespace Gitea.Declarative

open System
open System.Collections.Generic
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
        (client : GiteaClient.IGiteaClient)
        : Async<Result<unit, Map<User, AlignmentError<UserInfo>>>>
        =
        async {
            let desiredUsers = config.Users

            let! actualUsers =
                List.getPaginated (fun page limit -> client.AdminGetAllUsers (page, limit) |> Async.AwaitTask)

            let actualUsers =
                actualUsers
                |> Seq.map (fun u ->
                    match u.Login with
                    | None -> failwith "Gitea presented a user with no login!"
                    | Some login -> User login, UserInfo.Render u
                )
                |> Map.ofSeq

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
        (logger : ILogger)
        (config : GiteaConfig)
        (client : GiteaClient.IGiteaClient)
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
                            List.getPaginated (fun page count ->
                                client.UserListRepos (user, page, count) |> Async.AwaitTask
                            )

                        let! actualRepos =
                            repos
                            |> Seq.map (fun repo ->
                                async {
                                    let! rendered = Repo.Render client repo

                                    match repo.Name with
                                    | None -> return failwith "Gitea presented us with a repo with no name!"
                                    | Some repoName -> return RepoName repoName, rendered
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
                                | None ->
                                    if desired.Deleted = Some true then
                                        logger.LogInformation (
                                            "The repo {User}:{Repo} is configured as Deleted, and is absent from the server. Remove this repo from configuration.",
                                            user,
                                            let (RepoName repo) = repo in repo
                                        )

                                        None
                                    else
                                        Some (repo, AlignmentError.DoesNotExist desired)
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

    let private createPushMirrorOption (target : Uri) (githubToken : string) : GiteaClient.CreatePushMirrorOption =
        {
            SyncOnCommit = Some true
            RemoteAddress = target.ToString () |> Some
            RemoteUsername = Some githubToken
            RemotePassword = Some githubToken
            Interval = Some "8h0m0s"
            AdditionalProperties = Dictionary ()
        }

    let reconcileDifferingConfiguration
        (logger : ILogger)
        (client : GiteaClient.IGiteaClient)
        (githubApiToken : string option)
        (user : string)
        (repoName : string)
        (desired : Repo)
        (actual : Repo)
        : Async<unit>
        =
        if desired.Deleted = Some true then
            async {
                logger.LogWarning ("Deleting repo {User}:{Repo}", user, repoName)
                return! Async.AwaitTask (client.RepoDelete (user, repoName))
            }
        else

        match desired.GitHub, actual.GitHub with
        | None, Some gitHub ->
            async {
                logger.LogCritical (
                    "Unable to reconcile the desire to move a repo from GitHub-based to Gitea-based. This feature is not exposed on the Gitea API. You must manually convert the following repo to a normal repository first: {User}:{Repo}.",
                    user,
                    repoName
                )
            }
        | Some _, None ->
            async {
                logger.LogError (
                    "Unable to reconcile the desire to move a repo from Gitea-based to GitHub-based: {User}:{Repo}.",
                    user,
                    repoName
                )
            }
        | Some desiredGitHub, Some actualGitHub ->
            async {
                let mutable hasChanged = false

                if desiredGitHub.Uri <> actualGitHub.Uri then
                    logger.LogError (
                        "Refusing to migrate repo {User}:{Repo} to a different GitHub URL. Desired: {DesiredUrl}. Actual: {ActualUrl}.",
                        user,
                        repoName,
                        desiredGitHub.Uri,
                        actualGitHub.Uri
                    )

                let options : GiteaClient.EditRepoOption =
                    {
                        AdditionalProperties = Dictionary ()
                        AllowManualMerge = None
                        AllowMergeCommits = None
                        AllowRebase = None
                        AllowRebaseExplicit = None
                        AllowRebaseUpdate = None
                        AllowSquashMerge = None
                        Archived = None
                        AutodetectManualMerge = None
                        DefaultAllowMaintainerEdit = None
                        DefaultBranch = None
                        DefaultDeleteBranchAfterMerge = None
                        DefaultMergeStyle = None
                        EnablePrune = None
                        ExternalTracker = None
                        ExternalWiki = None
                        HasIssues = None
                        HasProjects = None
                        HasPullRequests = None
                        HasWiki = None
                        IgnoreWhitespaceConflicts = None
                        InternalTracker = None
                        MirrorInterval =
                            if desiredGitHub.MirrorInterval <> actualGitHub.MirrorInterval then
                                logger.LogDebug (
                                    "On {User}:{Repo}, setting {Property}",
                                    user,
                                    repoName,
                                    "MirrorInterval"
                                )

                                hasChanged <- true

                            Some desiredGitHub.MirrorInterval
                        Name = None
                        Private = None
                        Template = None
                        Website = None
                        Description =
                            if desired.Description <> actual.Description then
                                logger.LogDebug ("On {User}:{Repo}, setting {Property}", user, repoName, "Description")
                                hasChanged <- true

                            Some desired.Description
                    }

                if hasChanged then
                    let! result = client.RepoEdit (user, repoName, options) |> Async.AwaitTask
                    return ()
            }
        | None, None ->

        async {
            let mutable hasChanged = false

            let desired' =
                match desired.Native with
                | None ->
                    failwith
                        $"Expected a native section of desired for {user}:{repoName} since there was no GitHub, but got None"
                | Some n -> n

            let actual' =
                match actual.Native with
                | None ->
                    failwith
                        $"Expected a native section of actual for {user}:{repoName} since there was no GitHub, but got None"
                | Some n -> n

            let setPropertyIfNecessary (desired : 'a option) (actual : 'a option) (propertyName : string) : 'a option =
                match desired, actual with
                | None, None -> None
                | None, Some v ->
                    // This has been taken out of our management; do nothing.
                    logger.LogDebug (
                        "On {User}:{Repo}, no longer managing {Property} property (value: {CurrentValue})",
                        user,
                        repoName,
                        propertyName,
                        v
                    )

                    None
                | Some desired', _ ->
                    if Some desired' <> actual then
                        logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, repoName, propertyName)
                        hasChanged <- true

                    Some desired'

            let setProperty (desired : 'a) (actual : 'a) (propertyName : string) : 'a =
                if desired <> actual then
                    logger.LogDebug ("On {User}:{Repo}, will set {Property} property", user, repoName, propertyName)
                    hasChanged <- true

                desired

            let options : GiteaClient.EditRepoOption =
                {
                    AdditionalProperties = Dictionary ()
                    AllowManualMerge = None
                    AllowMergeCommits =
                        setPropertyIfNecessary desired'.AllowMergeCommits actual'.AllowMergeCommits "AllowMergeCommits"
                    AllowRebase = setPropertyIfNecessary desired'.AllowRebase actual'.AllowRebase "AllowRebase"
                    AllowRebaseExplicit =
                        setPropertyIfNecessary
                            desired'.AllowRebaseExplicit
                            actual'.AllowRebaseExplicit
                            "AllowRebaseExplicit"
                    AllowRebaseUpdate =
                        setPropertyIfNecessary desired'.AllowRebaseUpdate actual'.AllowRebaseUpdate "AllowRebaseUpdate"
                    AllowSquashMerge =
                        setPropertyIfNecessary desired'.AllowSquashMerge actual'.AllowSquashMerge "AllowSquashMerge"
                    Archived = None
                    AutodetectManualMerge = None
                    DefaultAllowMaintainerEdit = None
                    DefaultBranch = setProperty desired'.DefaultBranch actual'.DefaultBranch "DefaultBranch" |> Some

                    DefaultDeleteBranchAfterMerge =
                        setPropertyIfNecessary
                            desired'.DeleteBranchAfterMerge
                            actual'.DeleteBranchAfterMerge
                            "DeleteBranchAfterMerge"

                    DefaultMergeStyle =
                        setPropertyIfNecessary desired'.DefaultMergeStyle actual'.DefaultMergeStyle "DefaultMergeStyle"
                        |> Option.map (fun ms -> (ms : MergeStyle).ToString ())

                    Description =
                        setPropertyIfNecessary (Some desired.Description) (Some actual.Description) "Description"

                    EnablePrune = None
                    ExternalTracker = None
                    ExternalWiki = None
                    HasIssues = setPropertyIfNecessary desired'.HasIssues actual'.HasIssues "HasIssues"

                    HasProjects = setPropertyIfNecessary desired'.HasProjects actual'.HasProjects "HasProjects"

                    HasPullRequests =
                        setPropertyIfNecessary desired'.HasPullRequests actual'.HasPullRequests "HasPullRequests"

                    HasWiki = setPropertyIfNecessary desired'.HasWiki actual'.HasWiki "HasWiki"

                    IgnoreWhitespaceConflicts =
                        setPropertyIfNecessary
                            desired'.IgnoreWhitespaceConflicts
                            actual'.IgnoreWhitespaceConflicts
                            "IgnoreWhitespaceConflicts"

                    InternalTracker = None
                    MirrorInterval = None
                    Name = None
                    Private = setPropertyIfNecessary desired'.Private actual'.Private "Private"

                    Template = None
                    Website = None

                }

            do!
                if hasChanged then
                    logger.LogInformation ("Editing repo {User}:{Repo}", user, repoName)
                    client.RepoEdit (user, repoName, options) |> Async.AwaitTask |> Async.Ignore
                else
                    async.Return ()

            do!
                match desired'.Mirror, actual'.Mirror with
                | None, None -> async.Return ()
                | None, Some m ->
                    async { logger.LogError ("Refusing to delete push mirror for {User}:{Repo}", user, repoName) }
                | Some desired, None ->
                    match githubApiToken with
                    | None ->
                        async {
                            logger.LogCritical (
                                "Cannot add push mirror for {User}:{Repo} due to lack of GitHub API token",
                                user,
                                repoName
                            )
                        }
                    | Some token ->
                        async {
                            logger.LogInformation ("Setting up push mirror on {User}:{Repo}", user, repoName)
                            let pushMirrorOption = createPushMirrorOption desired.GitHubAddress token
                            let! _ = client.RepoAddPushMirror (user, repoName, pushMirrorOption) |> Async.AwaitTask
                            return ()
                        }
                | Some desired, Some actual ->
                    if desired <> actual then
                        async { logger.LogCritical ("Push mirror on {User}:{Repo} differs.", user, repoName) }
                    else
                        async.Return ()

            do!
                let desiredButNotPresent =
                    Set.difference desired'.Collaborators actual'.Collaborators

                let presentButNotDesired =
                    Set.difference actual'.Collaborators desired'.Collaborators

                [|
                    desiredButNotPresent
                    |> Seq.map (fun desired ->
                        async {
                            logger.LogTrace (
                                "Setting collaborator {Collaborator} on repo {User}:{Repo}",
                                desired,
                                user,
                                repoName
                            )

                            let option : GiteaClient.AddCollaboratorOption =
                                {
                                    AdditionalProperties = Dictionary ()
                                    Permission = None
                                }

                            do! client.RepoAddCollaborator (user, repoName, desired, option) |> Async.AwaitTask
                        }
                    )
                    |> Async.Parallel
                    |> Async.map (Array.iter id)

                    presentButNotDesired
                    |> Seq.map (fun desired ->
                        async {
                            logger.LogTrace (
                                "Deleting collaborator {Collaborator} on repo {User}:{Repo}",
                                desired,
                                user,
                                repoName
                            )

                            do! client.RepoDeleteCollaborator (user, repoName, desired) |> Async.AwaitTask
                        }
                    )
                    |> Async.Parallel
                    |> Async.map (Array.iter id)
                |]
                |> Async.Parallel
                |> Async.map (Array.iter id)

            do!
                // TODO: lift this out to a function and then put it into the new-repo flow too
                // The current behaviour is kind of desirable, because it gives you a chance to push to
                // the protected branch before it becomes protected.
                let extraActualProtected =
                    Set.difference actual'.ProtectedBranches desired'.ProtectedBranches

                let extraDesiredProtected =
                    Set.difference desired'.ProtectedBranches actual'.ProtectedBranches

                Seq.append (Seq.map Choice1Of2 extraActualProtected) (Seq.map Choice2Of2 extraDesiredProtected)
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
                                repoName
                            )

                            let! _ =
                                client.RepoDeleteBranchProtection (user, repoName, x.BranchName)
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
                                repoName
                            )

                            let s : GiteaClient.CreateBranchProtectionOption =
                                {
                                    AdditionalProperties = Dictionary ()
                                    ApprovalsWhitelistTeams = None
                                    ApprovalsWhitelistUsername = None
                                    BlockOnOfficialReviewRequests = None
                                    BlockOnOutdatedBranch = y.BlockOnOutdatedBranch
                                    BlockOnRejectedReviews = None
                                    BranchName = Some y.BranchName
                                    DismissStaleApprovals = None
                                    EnableApprovalsWhitelist = None
                                    EnableMergeWhitelist = None
                                    EnablePush = None
                                    EnablePushWhitelist = None
                                    EnableStatusCheck = None
                                    MergeWhitelistTeams = None
                                    MergeWhitelistUsernames = None
                                    ProtectedFilePatterns = None
                                    PushWhitelistDeployKeys = None
                                    PushWhitelistTeams = None
                                    PushWhitelistUsernames = None
                                    RequireSignedCommits = None
                                    RequiredApprovals = None
                                    RuleName = Some y.BranchName
                                    StatusCheckContexts = None
                                    UnprotectedFilePatterns = None
                                }

                            let! _ = client.RepoCreateBranchProtection (user, repoName, s) |> Async.AwaitTask
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
                                repoName
                            )

                            let statusCheck, contents =
                                match y.RequiredStatusChecks with
                                | None -> false, None
                                | Some checks -> true, Some checks

                            let s : GiteaClient.EditBranchProtectionOption =
                                {
                                    AdditionalProperties = Dictionary ()
                                    ApprovalsWhitelistTeams = None
                                    ApprovalsWhitelistUsername = None
                                    BlockOnOfficialReviewRequests = None
                                    BlockOnOutdatedBranch = y.BlockOnOutdatedBranch
                                    BlockOnRejectedReviews = None
                                    DismissStaleApprovals = None
                                    EnableApprovalsWhitelist = None
                                    EnableMergeWhitelist = None
                                    EnablePush = None
                                    EnablePushWhitelist = None
                                    EnableStatusCheck = Some statusCheck
                                    MergeWhitelistTeams = None
                                    MergeWhitelistUsernames = None
                                    ProtectedFilePatterns = None
                                    PushWhitelistDeployKeys = None
                                    PushWhitelistTeams = None
                                    PushWhitelistUsernames = None
                                    RequireSignedCommits = None
                                    RequiredApprovals = None
                                    StatusCheckContexts = contents
                                    UnprotectedFilePatterns = None
                                }

                            let! _ =
                                client.RepoEditBranchProtection (user, repoName, y.BranchName, s)
                                |> Async.AwaitTask

                            return ()
                        }
                    | [ Choice1Of2 _ ; Choice1Of2 _ ]
                    | [ Choice2Of2 _ ; Choice2Of2 _ ] -> failwith "can't have the same choice appearing twice"
                    | _ :: _ :: _ :: _ -> failwith "can't have appeared three times"
                )
                |> Async.Parallel
                |> Async.map (Array.iter id)
        }

    let reconcileRepoErrors
        (logger : ILogger)
        (client : GiteaClient.IGiteaClient)
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
                        | None, None -> failwith $"You must supply exactly one of Native or GitHub for {user}:{r}."
                        | Some _, Some _ ->
                            failwith $"Repo {user}:{r} has both Native and GitHub set; you must set exactly one."
                        | None, Some native ->
                            let options : GiteaClient.CreateRepoOption =
                                {
                                    AdditionalProperties = Dictionary ()
                                    AutoInit = None
                                    DefaultBranch = Some native.DefaultBranch
                                    Description = Some desired.Description
                                    Gitignores = None
                                    IssueLabels = None
                                    License = None
                                    Name = r
                                    Private = native.Private
                                    Readme = None
                                    Template = None
                                    TrustModel = None
                                }

                            let! result = client.AdminCreateRepo (user, options) |> Async.AwaitTask |> Async.Catch

                            match result with
                            | Choice2Of2 e -> raise (AggregateException ($"Error creating {user}:{r}", e))
                            | Choice1Of2 _ -> ()

                            match native.Mirror, githubApiToken with
                            | None, _ -> ()
                            | Some mirror, None -> failwith "Cannot push to GitHub mirror with an API key"
                            | Some mirror, Some token ->
                                logger.LogInformation ("Setting up push mirror for {User}:{Repo}", user, r)

                                let options : GiteaClient.CreatePushMirrorOption =
                                    {
                                        AdditionalProperties = Dictionary ()
                                        Interval = Some "8h0m0s"
                                        RemoteAddress = (mirror.GitHubAddress : Uri).ToString () |> Some
                                        RemotePassword = Some token
                                        RemoteUsername = Some token
                                        SyncOnCommit = Some true
                                    }

                                let! mirrors =
                                    List.getPaginated (fun page count ->
                                        client.RepoListPushMirrors (user, r, page, count) |> Async.AwaitTask
                                    )

                                match mirrors |> List.tryFind (fun m -> m.RemoteAddress = options.RemoteAddress) with
                                | None ->
                                    let! _ = client.RepoAddPushMirror (user, r, options) |> Async.AwaitTask
                                    ()
                                | Some existing ->
                                    if existing.SyncOnCommit <> Some true then
                                        failwith $"sync on commit should have been true for {user}:{r}"

                                ()
                        | Some github, None ->
                            let options : GiteaClient.MigrateRepoOptions =
                                {
                                    AdditionalProperties = Dictionary ()
                                    AuthPassword = None
                                    AuthToken = githubApiToken
                                    AuthUsername = None
                                    CloneAddr = string<Uri> github.Uri
                                    Issues = Some true
                                    Labels = Some true
                                    Lfs = Some true
                                    LfsEndpoint = None
                                    Milestones = Some true
                                    Mirror = Some true
                                    MirrorInterval = Some "8h0m0s"
                                    // TODO - migrate private status
                                    Private = None
                                    PullRequests = Some true
                                    Releases = Some true
                                    RepoName = r
                                    RepoOwner = Some user
                                    Service = None
                                    Uid = None
                                    Wiki = Some true
                                    Description = Some desired.Description
                                }

                            let! result = client.RepoMigrate options |> Async.AwaitTask |> Async.Catch

                            match result with
                            | Choice2Of2 e -> raise (AggregateException ($"Error migrating {user}:{r}", e))
                            | Choice1Of2 _ -> ()

                        logger.LogInformation ("Created repo {User}: {Repo}", user, r)

                        let! newlyCreated = client.RepoGet (user, r) |> Async.AwaitTask
                        let! newlyCreated = Repo.Render client newlyCreated
                        do! reconcileDifferingConfiguration logger client githubApiToken user r desired newlyCreated
                        return ()
                    }
                | AlignmentError.UnexpectedlyPresent ->
                    async {
                        logger.LogError (
                            "In the absence of the `deleted: true` configuration, refusing to delete unexpectedly present repo: {User}, {Repo}",
                            user,
                            r
                        )
                    }
                | AlignmentError.ConfigurationDiffers (desired, actual) ->
                    reconcileDifferingConfiguration logger client githubApiToken user r desired actual
            )
        )
        |> Async.Parallel
        |> fun a -> async.Bind (a, Array.iter id >> async.Return)

    let rec constructEditObject
        (log : ILogger)
        (user : string)
        (updates : UserInfoUpdate list)
        (body : GiteaClient.EditUserOption)
        : GiteaClient.EditUserOption
        =
        match updates with
        | [] -> body
        | h :: rest ->
            match h with
            | UserInfoUpdate.Admin (desired, actual) ->
                match desired, actual with
                | None, None -> body
                | None, Some _ ->
                    log.LogDebug ("No longer managing property {Property} for user {User}", "Admin", user)
                    body
                | Some desired, _ ->
                    log.LogDebug ("Editing {User}, property {Property}", user, "Admin")

                    { body with
                        Admin = Some desired
                    }
            | UserInfoUpdate.Email (desired, actual) ->
                log.LogDebug ("Editing {User}, property {Property}", user, "Email")

                { body with
                    Email = Some desired
                }
            | UserInfoUpdate.Visibility (desired, actual) ->
                log.LogDebug ("Editing {User}, property {Property}", user, "Visibility")

                { body with
                    Visibility = Some desired
                }
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

                body
            |> constructEditObject log user rest

    let reconcileUserErrors
        (log : ILogger)
        (getUserInput : string -> string)
        (client : GiteaClient.IGiteaClient)
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

                    let options : GiteaClient.CreateUserOption =
                        {
                            AdditionalProperties = Dictionary ()
                            CreatedAt = None
                            Email = desired.Email
                            FullName = Some user
                            LoginName = Some user
                            MustChangePassword = Some true
                            Password = pwd
                            Restricted = None
                            SendNotify = None
                            SourceId = None
                            Username = user
                            Visibility =
                                match desired.Visibility with
                                | None -> Some "public"
                                | Some v -> Some v


                        }

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
                                client.AdminDeleteUser(user, false).Result
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
                            let body : GiteaClient.EditUserOption =
                                {
                                    AdditionalProperties = Dictionary ()
                                    Active = None
                                    Admin = None
                                    AllowCreateOrganization = None
                                    AllowGitHook = None
                                    AllowImportLocal = None
                                    Description = None
                                    Email = None
                                    FullName = None
                                    Location = None
                                    LoginName = user
                                    MaxRepoCreation = None
                                    MustChangePassword = None
                                    Password = None
                                    ProhibitLogin = None
                                    Restricted = None
                                    SourceId =
                                        // Wouldn't it be lovely if *any* of this were documented?
                                        // I still have no idea what this does; it's optional when creating a user,
                                        // but mandatory when editing a user.
                                        0
                                    Visibility = None
                                    Website = None
                                }

                            let body = constructEditObject log user updates body

                            client.AdminEditUser(user, body).Result |> ignore
                        )
                }
        )
        |> Async.Parallel
        |> fun a -> async.Bind (a, Array.iter id >> async.Return)

    let toRefresh (client : GiteaClient.IGiteaClient) : Async<Map<User, Map<RepoName, GiteaClient.PushMirror list>>> =
        async {
            let! users = List.getPaginated (fun page limit -> client.AdminGetAllUsers (page, limit) |> Async.AwaitTask)

            let! results =
                users
                |> Seq.map (fun user ->
                    async {
                        let loginName =
                            match user.LoginName with
                            | None -> failwith "Gitea returned a User with no login name!"
                            | Some name -> name

                        let! repos =
                            List.getPaginated (fun page count ->
                                client.UserListRepos (loginName, page, count) |> Async.AwaitTask
                            )

                        let! pushMirrorResults =
                            repos
                            |> Seq.map (fun r ->
                                async {
                                    let repoName =
                                        match r.Name with
                                        | None -> failwith "Gitea returned a Repo with no name!"
                                        | Some name -> name

                                    let! mirrors =
                                        List.getPaginated (fun page count ->
                                            Async.AwaitTask (
                                                client.RepoListPushMirrors (loginName, repoName, page, count)
                                            )
                                        )

                                    return RepoName repoName, mirrors
                                }
                            )
                            |> Async.Parallel

                        return User loginName, Map.ofArray pushMirrorResults
                    }
                )
                |> Async.Parallel

            return results |> Map.ofArray
        }

    let refreshAuth
        (logger : ILogger)
        (client : GiteaClient.IGiteaClient)
        (githubToken : string)
        (instructions : Map<User, Map<RepoName, GiteaClient.PushMirror list>>)
        : Async<unit>
        =
        instructions
        |> Map.toSeq
        |> Seq.collect (fun (User user, repos) ->
            Map.toSeq repos
            |> Seq.collect (fun (RepoName repoName, mirrors) ->
                mirrors
                |> Seq.map (fun mirror ->
                    async {
                        let remoteAddress =
                            match mirror.RemoteAddress with
                            | None ->
                                failwith $"Gitea returned a mirror with no remote address, for repo %s{repoName}!"
                            | Some remoteAddress -> remoteAddress

                        let remoteName =
                            match mirror.RemoteName with
                            | None -> failwith $"Gitea returned a mirror with no remote name, for repo %s{repoName}!"
                            | Some remoteAddress -> remoteAddress

                        logger.LogInformation (
                            "Refreshing push mirror on {User}:{Repo} to {PushMirrorRemote}",
                            user,
                            repoName,
                            remoteAddress
                        )

                        let option =
                            { createPushMirrorOption (Uri remoteAddress) githubToken with
                                Interval = mirror.Interval
                                SyncOnCommit = mirror.SyncOnCommit
                            }

                        let! newMirror = Async.AwaitTask (client.RepoAddPushMirror (user, repoName, option))

                        let! deleteOldMirror =
                            Async.AwaitTask (client.RepoDeletePushMirror (user, repoName, remoteName))

                        return ()
                    }
                )
            )
        )
        |> Async.Parallel
        |> Async.map (Array.iter id)
