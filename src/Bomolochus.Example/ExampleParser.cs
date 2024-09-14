using Bomolochus.Text;

namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    public static Parsed<N>? Run<N>(this IParser<N> parser, string text)
        where N : Node 
        => parser
            .Run(new Context(TextSplitter.Create(text)))?
            .Parsing?
            .Complete();

    public static IParser<Node.Rules> ParseRules =>
        from rules in ParseDelimitedList(ParseRule, Match(';'))
        select new Node.Rules(rules);
    
    // there's a difference between a half-parsed thing needing presentation to us
    // and complete absence...
    // but how do we distinguish? 
    //
    // above the Many will read in even an empty thing
    // or rather - it will parse in even an Expectation
    // An expectation should be there to fill in if we have other bits in place which commit us to a presence
    // but a Many doesn't commit us - though it kind of does in some cases
    // ie a bounded list, or an arg list between parentheses
    // certainly require something each time
    //
    // this boundedness however
    // if we have a stricter list, with designated separators and bounds
    // then it's clearer that we do in fact expect something
    //
    // the opposite case is then also freed up:
    // the boundless speculative list
    // which will read forwards until it is uncertain
    //
    // however: do we ever want this?
    // statements are to be separated by semicolons
    // expressions by commas
    // rules by newlines
    // and then if we find something that's not a separator...
    // then we know exactly what the problem is:
    // we are missing a separator! 
    // C# is like this
    //
    // EVERYTHING IS PARSED AS A LIST
    // I like this principal
    // we separate space with delimiters
    // and then parse fluently within was bounded section
    

    private static IParser<Node.Rule> ParseRule =>
        from expr in Optional(ParseExpression)
        from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
        select new Node.Rule(expr, block);

    public static IParser<Node> ParseExpression =>
        ParseDisjunction;

    private static IParser<Node> ParseDisjunction =>
        from els in ParseDelimitedList(ParseConjunction, Match('|'))
        select els.Length > 1 
            ? new Node.Or(els.ToArray()) 
            : els.Single();

    static IParser<Node> ParseConjunction =>
        from els in ParseDelimitedList(ParseEquality, Match('&'))
        select els.Length > 1 
            ? new Node.And(els.ToArray()) 
            : els.Single();

    static IParser<Node> ParseEquality =>
        from els in ParseDelimitedList(ParseProp, Match('='))
        select els.Length > 1 
            ? new Node.Is(els.ToArray()) 
            : els.Single();

    static IParser<Node> ParseProp =>
        Expand(ParseTerminal,
            left => 
                from op in Match('.')
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
        from statements in ParseEnclosedList(
            Match('{'),
            ParseExpression,
            Match(';'),
            Match('}')
        )
        select new Node.StatementBlock(statements);

    static IParser<Node> ParseExpressionBlock =>
        from open in Match('(')
        from exp in ParseExpression
        from close in Match(')')
        select new Node.ExpressionBlock(exp);

    private static IParser<Node> ParseList =>
        from els in ParseEnclosedList(
            Match('['),
            ParseExpression,
            Match(','),
            Match(']')
        )
        select new Node.List(els);

    static IParser<Node.Ref> ParseNameNode =>
        from name in MatchWord()
        select new Node.Ref(name);

    static IParser<Node> ParseValueNode =>
        OneOf(
            ParseString,
            ParseRegex,
            ParseNumber
        );

    static IParser<Node> ParseString =>
        from open in Match('"')
        from str in Match(c => c != '"')
        from close in Match('"')
        select new Node.String(str);

    static IParser<Node> ParseRegex =>
        from open in Match('/')
        from pattern in Match(c => c != '/')
        from close in Match('/')
        select new Node.Regex(pattern);

    static IParser<Node> ParseNumber =>
        from num in MatchDigits()
        select new Node.Number(int.Parse(num.ReadAll()));

    static IParser<Node> ParseNoise =>
        from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']') //todo: parse for noise repeatedly forwards
        select new Node.Noise().WithError("Unrecognised symbol");
    
    
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
}