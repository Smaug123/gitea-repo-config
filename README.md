# .NET Declarative Gitea

This is a small project to allow you to specify a [Gitea](https://github.com/go-gitea/) configuration in a declarative manner, and to resolve differences between intended and actual config.

# How to build and run

With Nix: `nix run github:Smaug123/dotnet-gitea-declarative -- --help`.
The config file you provide as an argument should conform to [the schema](./Gitea.Declarative.Lib/GiteaConfig.schema.json); there is [an example](./Gitea.Declarative.Test/GiteaConfig.json) in the tests.

## Building from source

Just clone and `dotnet build`.

To upgrade the NuGet dependencies in the flake, run `nix build .#fetchDeps` and copy the resulting file into `nix/deps.nix`.
