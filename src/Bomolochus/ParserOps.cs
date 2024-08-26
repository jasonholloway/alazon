using System.Collections.Immutable;

namespace Bomolochus;

public static class ParserOps 
{
    public static IParser<N> Barrier<N>(int precedence, IParser<N> inner) =>
        Parser.Create(x =>
        {
            if (x.Precedence >= precedence
                && inner.Run(x with { Precedence = precedence }) is Result<N> r) //slightly iffy matching against concrete invariant type here
            {
                return r with
                {
                    Context = r.Context with
                    {
                        Precedence = x.Precedence
                    }
                };
            }

            return null;
        });
    
    public static IParser<N> Isolate<N>(IParser<N> inner) =>
        Parser.Create(x =>
        {
            if (inner.Run(x with { Precedence = 100 }) is Result<N> r) //slightly iffy matching against concrete invariant type here
            {
                return r with
                {
                    Context = r.Context with
                    {
                        Precedence = x.Precedence
                    }
                };
            }

            return null;
        });

    public static IParser<N> Optional<N>(IParser<N> inner) => 
        Parser.Create(x => inner.Run(x) switch
        {
            {} r => r,
            null => new Result<N>(x, null)
        });

    public static IParser<N> Expand<N>(IParser<N> first, Func<N, IParser<N>> repeatedly) =>
        from a in first
        from z in Parser.Create(x =>
        {
            var n = a;
            var acParsed = ImmutableArray<Parsing>.Empty;

            while (true)
            {
                if (repeatedly(n).Run(x) is { Context: var x2, Parsing: {} p }) //what about null parsed?
                {
                    x = x2;
                    n = p.Val;
                    acParsed = acParsed.Add(p);
                    continue;
                }
                
                return new Result<N>(x, Parsing.From(n, acParsed));
            }
        })
        select z;

    public static IParser<Node.List> AtLeastOne(IParser<Node> inner) =>
        Many(inner, true);
    
    public static IParser<Node.List> Many(IParser<Node> inner, bool nullOnEmpty = false) => 
        Parser.Create(x =>
        {
            var acNodes = ImmutableArray<Node>.Empty;
            var acParsed = ImmutableArray<Parsing>.Empty;
            
            while (inner.Run(x) is { Context: var x1, Parsing: { Certainty: 1 } parsing })
            {
                //todo shouldn't continue on null parsed
                //otherwise will continue for ever...
                
                (acParsed, acNodes) = parsing switch
                {
                    null => (acParsed, acNodes),
                    { Val: Node.Syntax } => (acParsed.Add(parsing), acNodes),
                    { Val: Node.List { Nodes: var nested } } => (acParsed.Add(parsing), acNodes.AddRange(nested)),
                    { Val: var val } => (acParsed.Add(parsing), acNodes.Add(val)),
                };
                x = x1;
            }

            if (nullOnEmpty && acNodes.IsEmpty)
            {
                return null;
            }

            return new Result<Node.List>(
                x, 
                Parsing.From(new Node.List(acNodes), acParsed)
                );
        });

    public static IParser<Node> OneOf(params IParser<Node>[] fns)
        => Parser.Create(x =>
        {
            IResult<Node>? best = null;
            
            foreach (var fn in fns)
            {
                switch ((best, fn.Run(x)))
                {
                    case (_, null): continue;
                    
                    case (_, { Parsing.Certainty: 1 } p):
                        return p; 
                    
                    case (null, {} p):
                        best = p;
                        break;
                    
                    case ({ Parsing.Certainty: var bestCertainty }, { Parsing.Certainty: var certainty } p) 
                        when certainty > bestCertainty:
                        best = p;
                        break;
                }
            }

            return best!;
        });
    
    public static IParser<TToken> Take<TToken>(Func<TToken, bool>? match = null)
        where TToken : Token
        => Parser.Create(x =>
        {
            if (!x.Tokens.IsEmpty
                && x.Tokens.Peek() is { GenericToken: TToken token, Text: var text }
                && (match is null || match(token)))
            {
                return new Result<TToken>(
                    x with { Tokens = x.Tokens.Dequeue() },
                    Parsing.From(token, text)
                );
            }

            return null;
        });
    
        
    public static IParser<Node.Expect> Expect(string expectation)
        => Return(new Node.Expect()).WithError(expectation);

    public static IParser<N> Return<N>(N node) => 
        Parser.Create(x => 
            new Result<N>(x, Parsing.From(node, []))
        );

    public record Context(ImmutableQueue<Lexed> Tokens, int Precedence);
    
    public interface IResult<out N>
    {
        Context Context { get; }
        Parsing<N>? Parsing { get; }
    }

    public record Result<N>(Context Context, Parsing<N>? Parsing) : IResult<N>;

    public abstract class Parser
    {
        public static IParser<N> Create<N>(Func<Context, IResult<N>?> fn) 
            => new Parser<N>(fn);
    }

    class Parser<N>(Func<Context, IResult<N>?> fn) : Parser, IParser<N>
    {
        public IResult<N>? Run(Context x)
        {
            // while (!x.Tokens.IsEmpty
            //        && x.Tokens.Peek() is Lexed<Token.Space> { Vector: var lexedVec })
            // {
            //     x = x with
            //     {
            //         Tokens = x.Tokens.Dequeue(),
            //         Vector = x.Vector + lexedVec 
            //     };
            // }
            //
            return fn(x);
        }
    }

    public interface IParser<out N>
    {
        IResult<N>? Run(Context x);
    }
}
