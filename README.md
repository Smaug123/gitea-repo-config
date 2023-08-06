# .NET Declarative Gitea

This is a small project to allow you to specify a [Gitea](https://github.com/go-gitea/) configuration in a declarative manner, and to resolve differences between intended and actual config.

## Supported features

* Creation of users
* Creation of repositories as pull mirrors from GitHub
* Creation of repositories
  * Optional push mirroring to GitHub
  * Optional branch protection rules
  * Pull request configuration (e.g. whether rebase-merges are allowed, etc)
  * Collaborators
* Deletion of repositories, guarded by the `"deleted": true` configuration

# Arguments

Run with the `--help` argument for a full listing.
The main argument you provide is a JSON configuration file, which should conform to [the schema](./Gitea.Declarative.Lib/GiteaConfig.schema.json); there is [an example](./Gitea.Declarative.Test/GiteaConfig.json) in the tests.

# How to build and run

* With Nix: `nix run github:Smaug123/dotnet-gitea-declarative -- --help`.
* From source: clone the repository, and `dotnet run`.

# Examples

A no-op, where the configuration is already in sync with the remote Gitea instance:
![No-op update](./docs/noop-update.svg)

# Development

To upgrade the NuGet dependencies in the flake, run `nix build .#fetchDeps` and copy the resulting file into `nix/deps.nix`.

## Formatting

There are pull request checks on this repo, enforcing [Fantomas](https://github.com/fsprojects/fantomas/)-compliant formatting.
Consider performing the following command to set the pre-commit hook in the repo which checks formatting:
```bash
git config core.hooksPath hooks/
```
You will need to ensure that Fantomas is installed when you run this hook, either by running from a `nix develop` shell, or by executing `dotnet tool restore` from the root of the repository first.
