namespace Gitea

open SwaggerProvider

[<AutoOpen>]
module GiteaClient =

    [<Literal>]
    let Host = "https://gitea.patrickstevens.co.uk/swagger.v1.json"

    type Gitea = SwaggerClientProvider<Host>
