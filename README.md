# .NET Declarative Gitea

This is a small project to allow you to specify a [Gitea](https://github.com/go-gitea/) configuration in a declarative manner, and to resolve differences between intended and actual config.

# How to build

Just `dotnet build`.

To upgrade the NuGet dependencies in the flake, run `nix build .#fetchDeps` and copy the resulting file into `nix/deps.nix`.
