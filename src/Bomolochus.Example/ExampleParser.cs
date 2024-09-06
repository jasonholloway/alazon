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
        from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
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

    static IParser<Node> ParseProp =>
        Expand(ParseTerminal,
            left => 
                from op in Take<Token.Op.Dot>()
                from right in ParseTerminal
                select new Node.Prop(left, right)
        );
    
    static IParser<Node> ParseTerminal =>
        OneOf(
            ParseExpressionBlock,
            ParseList,
            ParseNameNode, 
            ParseValueNode,
            ParseNoise
            );

    public static IParser<Node.StatementBlock> ParseStatementBlock =>
        from open in Take<Token.OpenBrace>()
        from head in Optional(ParseExpression)
        from rest in Many(
            from sep in Take<Token.Semicolon>()
            from next in ParseExpression
            select next
        )
        from close in Take<Token.CloseBrace>()
        select new Node.StatementBlock(head == null ? [] : [head, ..rest]);

    static IParser<Node> ParseExpressionBlock =>
        from open in Take<Token.OpenParenthesis>()
        from exp in ParseExpression
        from close in Take<Token.CloseParenthesis>()
        select new Node.ExpressionBlock(exp);
    
    
    
    // we want a dynamic noise parser
    // which we can give a character set to
    // ie, parse as noise until we find a separator...
    // but to do this would require
    // lazy tokenizing, and also repeatable tokenizing
    // which'd be kind of nice actually
    // given that amazing performance isn't our aim here
    // it would also simplify the specification
    // if tokens were removed
    // or rather - if tokens were dynamic
    // I like this idea, though it would be a violent convulsion of the code
    // towards betterness!
    
    
    static IParser<Node> ParseList =>
        from open in Take<Token.OpenBracket>()
        from head in ParseExpression
        from rest in Many(
            from comma in Take<Token.Comma>()
            from next in OneOf(ParseExpression, Expect("BLAH"))
            select next
            )
        from close in Take<Token.CloseBracket>()
        select new Node.List([head, ..rest]);

    static IParser<Node.Ref> ParseNameNode =>
        from name in Take<Token.Name>()
        select new Node.Ref(name.Readable);

    static IParser<Node> ParseValueNode =>
        OneOf(
            ParseString,
            ParseRegex,
            ParseNumber
        );

    static IParser<Node> ParseString =>
        from str in Take<Token.Value.String>()
        select new Node.String(str.Readable);

    static IParser<Node> ParseRegex =>
        from regex in Take<Token.Value.Regex>()
        select new Node.Regex(regex.Readable);

    static IParser<Node> ParseNumber =>
        from num in Take<Token.Value.Number>()
        select new Node.Number(num.Val);

    static IParser<Node> ParseNoise =>
        from noise in Take<Token.Noise>() //todo: parse for noise repeatedly forwards
        select new Node.Noise().WithError("Unrecognised symbol");
}

