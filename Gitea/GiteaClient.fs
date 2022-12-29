namespace Gitea

open SwaggerProvider

[<AutoOpen>]
module GiteaClient =

    [<Literal>]
    let Host = "file://" + __SOURCE_DIRECTORY__ + "/swagger.v1.json"

    type Gitea = SwaggerClientProvider<Host>
