namespace Gitea.Declarative

open Argu

type ArgsEvaluator<'ret> =
    abstract Eval<'a, 'b when 'b :> IArgParserTemplate> :
        (ParseResults<'b> -> Result<'a, ArguParseException>) -> ('a -> Async<int>) -> 'ret

type ArgsCrate =
    abstract Apply<'ret> : ArgsEvaluator<'ret> -> 'ret

[<RequireQualifiedAccess>]
module ArgsCrate =
    let make<'a, 'b when 'b :> IArgParserTemplate>
        (ofResult : ParseResults<'b> -> Result<'a, ArguParseException>)
        (run : 'a -> Async<int>)
        =
        { new ArgsCrate with
            member _.Apply e = e.Eval ofResult run
        }
