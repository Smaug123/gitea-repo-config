namespace Gitea.InMemory

open Gitea.Declarative

[<RequireQualifiedAccess>]
module Operations =
    let createdUser (createUserOption : Gitea.CreateUserOption) : Gitea.User =
        let result = Gitea.User ()
        result.Email <- createUserOption.Email
        result.Restricted <- createUserOption.Restricted
        // TODO: what is this username used for anyway
        // result.LoginName <- createUserOption.Username
        result.Visibility <- createUserOption.Visibility
        result.Created <- createUserOption.CreatedAt
        result.FullName <- createUserOption.FullName
        result.LoginName <- createUserOption.LoginName

        result
