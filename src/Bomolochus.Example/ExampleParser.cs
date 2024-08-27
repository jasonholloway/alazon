using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    public static Parsed<N>? Run<N>(this IParser<N> fn, string text)
        where N : Node
    {
        return fn
            .Run(new Context(
                ImmutableQueue.CreateRange(new ExampleLexer(text).Lex()), 8)
            )?.Parsing?.Complete();
    }
    
    public static IParser<Node.List> ParseRules =>
        Many(ParseRule);

    private static IParser<Node.Rule> ParseRule =>
        from expr in Optional(ParseExpression)
        from block in OneOf(ParseStatement, Expect("Expected statement"))
        select new Node.Rule(expr, block);


    /// Exp = Name|Value + (Noise|(ExpOp + Exp))?

    public static IParser<Node> ParseExpression =>
        ParseDisjunction;

    static IParser<Node> ParseDisjunction =>
        from head in ParseConjunction
        from tail in Many(
            from op in Take<Token.Op.Or>()
            from next in ParseConjunction
            select next
        )
        select tail.Nodes.Any() 
            ? new Node.Or([head, ..tail.Nodes])
            : head;

    static IParser<Node> ParseConjunction =>
        from head in ParseEquality
        from tail in Many(
            from op in Take<Token.Op.And>()
            from next in ParseEquality
            select next
        )
        select tail.Nodes.Any() 
            ? new Node.And([head, ..tail.Nodes])
            : head;

    static IParser<Node> ParseEquality =>
        from head in ParseProp
        from tail in Many(
            from op in Take<Token.Op.Is>()
            from next in OneOf(ParseProp, Expect("Expect something here mate"))
            select next
        )
        select tail.Nodes.Any() 
            ? new Node.Is([head, ..tail.Nodes]) 
            : head;

    private static IParser<Node> ParseProp =>
        Expand(ParseTerminal,
            left => 
                from op in Take<Token.Op.Dot>()
                from right in ParseTerminal
                select new Node.Prop(left, right)
        );
    
    private static IParser<Node> ParseTerminal =>
        OneOf(
            ParseNameNode, 
            ParseValueNode,
            (
                from open in Take<Token.OpenBracket>()
                from exp in ParseExpression
                from close in Take<Token.CloseBracket>()
                select exp
            ),
            ParseNoise
            );
    
    
    // private static IParser<Node> StartParsingExpression =>
    //     OneOf(
    //         ParseNameNode,
    //         ParseValueNode,
    //         ParseNoise
    //         );
    //
    // private static IParser<Node> ContinueParsingExpression(Node left) =>
    //     from op in Take<Token.Op>()
    //     from exp in op switch
    //     {
    //         Token.Op.Dot =>
    //             Barrier(precedence: 0,
    //                 from right in ParseNameNode
    //                 select new Node.Prop(left, right) as Node
    //             ),
    //         Token.Op.Is =>
    //             Barrier(precedence: 6,
    //                 from right in OneOf(ParseExpression, Expect("Expected expression"))
    //                 select new Node.Is(left, right)
    //             ),
    //         Token.Op.And =>
    //             Barrier(precedence: 7,
    //                 from right in ParseExpression
    //                 select new Node.And(left, right)
    //             ),
    //         Token.Op.Or =>
    //             Barrier(precedence: 8,
    //                 from right in ParseExpression
    //                 select new Node.Or(left, right)
    //             ),
    //         _ => null
    //     }
    //     select exp;

    public static IParser<Node.List> ParseStatements =>
        from head in ParseStatement
        from rest in Many(
            from sep in Take<Token.Semicolon>()
            from next in ParseStatement
            select next
        )
        select Node.List.Cons(head, rest);

    private static IParser<Node> ParseStatement =>
        Expand(ParseUnaryStatement, ParseBinaryStatement);

    private static IParser<Node> ParseUnaryStatement =>
        OneOf(
            ParseOpenBrace,
            ParseCall,
            ParseNameNode,
            ParseValueNode,
            ParseNoise
        );

    static IParser<Node> ParseBinaryStatement(Node left) =>
        from op in Take<Token.Op>()
        from statement in op switch
        {
            Token.Op.Dot =>
                from name in ParseNameNode
                select new Node.Prop(left, name) as Node,
            Token.Op.Incr =>
                Barrier(1,
                    from exp in ParseExpression
                    select new Node.Incr(left, exp)
                ),
            _ => null //need something better here?
        }
        select statement;

    //todo below should emit a Block node, not a simple List
    private static IParser<Node.List> ParseOpenBrace =>
        from open in Take<Token.OpenBrace>()
        from statements in ParseStatements
        from close in Take<Token.CloseBrace>()
        select statements;

    private static IParser<Node.Call> ParseCall =>
        from name in ParseNameNode
        from open in Take<Token.OpenBracket>()
        from arg0 in Isolate(ParseExpression)
        from close in Take<Token.CloseBracket>()
        select new Node.Call(name, [arg0]);

    private static IParser<Node> ParseOpenBracket =>
        from open in Take<Token.OpenBracket>()
        from exp in Isolate(ParseExpression)
        from close in Take<Token.CloseBracket>()
        select exp;

    private static IParser<Node.Ref> ParseNameNode =>
        from name in Take<Token.Name>()
        select new Node.Ref(name.Readable);

    private static IParser<Node> ParseValueNode =>
        OneOf(
            ParseString,
            ParseRegex,
            ParseNumber
        );

    private static IParser<Node> ParseString =>
        from str in Take<Token.Value.String>()
        select new Node.String(str.Readable);

    private static IParser<Node> ParseRegex =>
        from regex in Take<Token.Value.Regex>()
        select new Node.Regex(regex.Readable);

    private static IParser<Node> ParseNumber =>
        from num in Take<Token.Value.Number>()
        select new Node.Number(num.Val);

    private static IParser<Node> ParseNoise =>
        from noise in Take<Token.Noise>() //todo: parse for noise repeatedly forwards
        select new Node.Noise().WithError("Unrecognised symbol");
}