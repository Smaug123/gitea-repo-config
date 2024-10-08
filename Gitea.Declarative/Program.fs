﻿namespace Gitea.Declarative

open Argu

module Program =

    let printUserErrors (m : Map<User, AlignmentError<UserInfo>>) =
        m |> Map.iter (fun (User u) err -> printfn $"%s{u}: {err}")

    let printRepoErrors (m : Map<User, Map<RepoName, AlignmentError<Repo>>>) =
        m
        |> Map.iter (fun (User u) errMap -> errMap |> Map.iter (fun (RepoName r) err -> printfn $"%s{u}: %s{r}: {err}"))

    let subcommands =
        [|
            "reconcile",
            ("Reconcile a remote Gitea server with a declarative configuration",
             ArgsCrate.make RunArgs.OfParse Reconcile.run)

            "output-schema",
            ("Output a schema you can use to verify the `reconcile` config file",
             ArgsCrate.make OutputSchemaArgs.OfParse OutputSchema.run)

            "verify", ("Verify a `reconcile` configuration file", ArgsCrate.make VerifyArgs.OfParse Verify.run)

            "refresh-auth",
            ("Refresh authentication for push mirrors", ArgsCrate.make RefreshAuthArgs.OfParse RefreshAuth.run)
        |]
        |> Map.ofArray

    [<EntryPoint>]
    let main argv =
        // It looks like Argu doesn't really support the combination of subcommands and read-from-env-vars, so we just
        // roll our own.

        match Array.tryHead argv with
        | None
        | Some "--help" ->
            subcommands.Keys
            |> String.concat ","
            |> eprintfn "Subcommands (try each with `--help`): %s"

            127

        | Some commandName ->

        match Map.tryFind commandName subcommands with
        | None ->
            subcommands.Keys
            |> String.concat ","
            |> eprintfn "Unrecognised command '%s'. Subcommands (try each with `--help`): %s" commandName

            127

        | Some (_help, command) ->

        let argv = Array.tail argv
        let config = ConfigurationReader.FromEnvironmentVariables ()

        { new ArgsEvaluator<_> with
            member _.Eval<'a, 'b when 'b :> IArgParserTemplate>
                (ofParseResult : ParseResults<'b> -> Result<'a, _>)
                run
                =
                let parser = ArgumentParser.Create<'b> ()

                let parsed =
                    try
                        parser.Parse (argv, config, raiseOnUsage = true) |> Some
                    with :? ArguParseException as e ->
                        e.Message.Replace ("Gitea.Declarative ", sprintf "Gitea.Declarative %s " commandName)
                        |> eprintfn "%s"

                        None

                match parsed with
                | None -> Error 127
                | Some parsed ->

                match ofParseResult parsed with
                | Error e ->
                    e.Message.Replace ("Gitea.Declarative ", sprintf "Gitea.Declarative %s " commandName)
                    |> eprintfn "%s"

                    Error 127
                | Ok args ->

                run args |> Async.RunSynchronously |> Ok
        }
        |> command.Apply
        |> Result.cata id id
