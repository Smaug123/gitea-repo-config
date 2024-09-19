namespace Gitea.InMemory

open System
open System.Threading.Tasks
open Gitea.Declarative

module Types =

    type BranchName = | BranchName of string

    type BranchProtectionRule =
        {
            RequiredChecks : string Set
        }

    type NativeRepo =
        {
            BranchProtectionRules : (BranchName * BranchProtectionRule) list
        }

    type Repo =
        | GitHubMirror of Uri
        | NativeRepo of NativeRepo
