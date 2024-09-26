namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    /* a new line needs to be usable as a delimiter
     * yet currently it automatically gets read as mere spacing
     *
     * when trying to match a particular char
     * this positive char needs removing from the space set
     * but also: this means that trailing space doesn't quite work
     * as we need to know what the next parsing is first
     *
     * which would work if ParseEnd was a parser
     * as this would then parse forwards through trailing stuff
     */
    
    
    
    public static readonly Parser<Node.Rules> ParseRules = new(() =>
        from rules in ParseDelimitedList(ParseRule, OneOf(Match(';'), Match('\n')))
        select new Node.Rules(rules)
    );
    
    public static readonly Parser<Node> ParseExpression = new(() => 
        ParseDisjunction
    );

    static readonly Parser<Node> ParseDisjunction = new(() => 
        from els in ParseDelimitedList(ParseConjunction, Match('|'))
        select els.Length > 1 
            ? new Node.Or(els.ToArray()) 
            : els.Single()
    );
    
    static readonly Parser<Node.Rule> ParseRule = new(() => 
        from expr in Optional(ParseExpression)
        from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
        select new Node.Rule(expr, block)
    );

    static readonly Parser<Node> ParseConjunction = new(() =>
        from els in ParseDelimitedList(ParseEquality, Match('&'))
        select els.Length > 1 
            ? new Node.And(els.ToArray()) 
            : els.Single()
    );

    static readonly Parser<Node> ParseEquality = new(() =>
        from els in ParseDelimitedList(
            OneOf(ParseProp, Expect("Expression expected")), 
            Match('=')
            )
        select els.Length > 1 
            ? new Node.Is(els.ToArray()) 
            : els.Single()
    );

    static readonly Parser<Node> ParseProp = new(() =>
        Expand(ParseTerminal,
            left => 
                from op in Match('.')
                from right in ParseTerminal
                select new Node.Prop(left, right)
        ));

    private static readonly Parser<Node> ParseCall = new(() =>
        from name in ParseNameNode
        from args in ParseEnclosedList(
            Match('('),
            ParseExpression,
            Match(','),
            Match(')')
            )
        select new Node.Call(name, args.ToArray())
    );
    
    static readonly Parser<Node> ParseTerminal = new(() => 
        OneOf(
            ParseCall,
            ParseExpressionBlock,
            ParseList,
            ParseNameNode, 
            ParseValueNode,
            ParseNoise
            ));

    public static readonly Parser<Node.StatementBlock> ParseStatementBlock = new(() => 
        from statements in ParseEnclosedList(
            Match('{'),
            ParseExpression,
            Match(';'),
            Match('}')
        )
        select new Node.StatementBlock(statements)
    );

    static readonly Parser<Node.ExpressionBlock> ParseExpressionBlock = new(() => 
        from open in Match('(')
        from exp in ParseExpression
        from close in Match(')')
        select new Node.ExpressionBlock(exp)
    );

    private static readonly Parser<Node.List> ParseList = new(() =>
        from els in ParseEnclosedList(
            Match('['),
            OneOf(ParseExpression, Expect("Element expected")),
            Match(','),
            Match(']')
        )
        select new Node.List(els)
    );

    static readonly Parser<Node.Ref> ParseNameNode = new(() => 
        from name in MatchWord()
        select new Node.Ref(name)
    );

    static readonly Parser<Node> ParseValueNode = new(() =>
        OneOf<Node>(
            ParseString,
            ParseRegex,
            ParseNumber
        ));

    static readonly Parser<Node.String> ParseString = new(() =>
        from open in Match('"')
        from str in Match(c => c != '"')
        from close in Match('"')
        select new Node.String(str)
    );

    static Parser<Node.Regex> ParseRegex => new(() =>
        from open in Match('/')
        from pattern in Match(c => c != '/')
        from close in Match('/')
        select new Node.Regex(pattern)
    );

    static Parser<Node.Number> ParseNumber => new(() =>
        from num in MatchDigits()
        select new Node.Number(int.Parse(num.ReadAll()))
    );

    private static Parser<Node.Noise> ParseNoise => new(() =>
        from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']' and not '{')
        select new Node.Noise().WithError("Unrecognised symbol")
    );
}