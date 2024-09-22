namespace Gitea.InMemory

open System.Collections.Generic

[<RequireQualifiedAccess>]
module Operations =
    let createdUser (createUserOption : GiteaClient.CreateUserOption) : GiteaClient.User =
        let result : GiteaClient.User =
            {
                AdditionalProperties = Dictionary ()
                Active = None
                AvatarUrl = None
                Created = None
                Description = None
                Email = Some createUserOption.Email
                FollowersCount = None
                FollowingCount = None
                FullName = createUserOption.FullName
                Id = None
                IsAdmin = None
                Language = None
                LastLogin = None
                Location = None
                Login = None
                LoginName = createUserOption.LoginName
                ProhibitLogin = failwith "todo"
                Restricted = createUserOption.Restricted
                StarredReposCount = None
                Visibility = createUserOption.Visibility
                Website = None
            }

        result
