using System.Collections.Immutable;

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

    public static IParser<Node.Rules> ParseRules =>
        from rules in Many(ParseRule)
        select new Node.Rules(rules);

    private static IParser<Node.Rule> ParseRule =>
        from expr in Optional(ParseExpression)
        from block in OneOf(ParseStatements, Expect("Expected statement block"))
        select new Node.Rule(expr, block);


    public static IParser<Node> ParseExpression =>
        ParseDisjunction;

    static IParser<Node> ParseDisjunction =>
        from head in ParseConjunction
        from tail in Optional(AtLeastOne(
            from op in Take<Token.Op.Or>()
            from next in ParseConjunction
            select next
        ))
        select tail != null
            ? new Node.Or([head, ..tail])
            : head;

    static IParser<Node> ParseConjunction =>
        from head in ParseEquality
        from tail in Optional(AtLeastOne(
            from op in Take<Token.Op.And>()
            from next in ParseEquality
            select next
        ))
        select tail != null
            ? new Node.And([head, ..tail])
            : head;

    static IParser<Node> ParseEquality =>
        from head in ParseProp
        from tail in Optional(AtLeastOne(
            from op in Take<Token.Op.Is>()
            from next in OneOf(ParseProp, Expect("Expect something here mate"))
            select next
        ))
        select tail != null 
            ? new Node.Is([head, ..tail]) 
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
            ParseBrackets,
            ParseNameNode, 
            ParseValueNode,
            ParseNoise
            );
    
    
    /* In the world of AWK
     * statements are different from expressions
     *
     * a statement could be an assignment
     * which would have to be separated by semicolon
     *
     * or it could be a call of a function
     *
     *
     * 
     */
    
    
    
    

    public static IParser<Node.Statements> ParseStatements =>
        from open in Take<Token.OpenBrace>()
        from head in ParseExpression
        from rest in Many(
            from sep in Take<Token.Semicolon>()
            from next in ParseExpression
            select next
        )
        from close in Take<Token.CloseBrace>()
        select new Node.Statements([head, ..rest]);

    // private static IParser<Node> ParseStatement =>
    //     Expand(ParseUnaryStatement, ParseBinaryStatement);


    //
    //
    // static IParser<Node> ParseStatementTerminal =>
    //     OneOf(
    //         ParseBraces,
    //         ParseNameNode,
    //         ParseValueNode,
    //         ParseNoise
    //         );
    //
    //
    //
    //
    //
    // static IParser<Node> ParseUnaryStatement =>
    //     OneOf(
    //         ParseBraces,
    //         ParseCall,
    //         ParseNameNode,
    //         ParseValueNode,
    //         ParseNoise
    //     );
    //
    // static IParser<Node> ParseBinaryStatement(Node left) =>
    //     from op in Take<Token.Op>()
    //     from statement in op switch
    //     {
    //         Token.Op.Dot =>
    //             from name in ParseNameNode
    //             select new Node.Prop(left, name) as Node,
    //         Token.Op.Incr =>
    //             Barrier(1,
    //                 from exp in ParseExpression
    //                 select new Node.Incr(left, exp)
    //             ),
    //         _ => null //need something better here?
    //     }
    //     select statement;

    // private static IParser<Node> ParseBraces =>
    //     from open in Take<Token.OpenBrace>()
    //     from statements in ParseStatements
    //     from close in Take<Token.CloseBrace>()
    //     select new Node.Braces(statements.Inner); //statements should just return an array, ready to be wrapped

    // private static IParser<Node.Call> ParseCall =>
    //     from name in ParseNameNode
    //     from open in Take<Token.OpenBracket>()
    //     from arg0 in Isolate(ParseExpression)
    //     from close in Take<Token.CloseBracket>()
    //     select new Node.Call(name, [arg0]);

    private static IParser<Node> ParseBrackets =>
        from open in Take<Token.OpenBracket>()
        from exp in ParseExpression
        from close in Take<Token.CloseBracket>()
        select new Node.Brackets(exp);

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

