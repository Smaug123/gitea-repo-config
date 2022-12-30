namespace Gitea.Declarative

[<RequireQualifiedAccess>]
module internal UserInput =

    let rec getDefaultNo (getUserInput : string -> string) (message : string) : bool =
        let answer = getUserInput $"{message} (y/N): "

        match answer with
        | "y"
        | "Y" -> true
        | "n"
        | "N"
        | "" -> false
        | _ -> getDefaultNo getUserInput message
