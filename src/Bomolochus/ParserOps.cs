using System.Collections.Immutable;

namespace Bomolochus;

public static class ParserOps 
{
    // public static IParser<N> Barrier<N>(int precedence, IParser<N> inner) =>
    //     Parser.Create(x =>
    //     {
    //         if (x.Precedence >= precedence
    //             && inner.Run(x with { Precedence = precedence }) is Result<N> r) //slightly iffy matching against concrete invariant type here
    //         {
    //             return r with
    //             {
    //                 Context = r.Context with
    //                 {
    //                     Precedence = x.Precedence
    //                 }
    //             };
    //         }
    //
    //         return null;
    //     });
    //
    // public static IParser<N> Isolate<N>(IParser<N> inner) =>
    //     Parser.Create(x =>
    //     {
    //         if (inner.Run(x with { Precedence = 100 }) is Result<N> r) //slightly iffy matching against concrete invariant type here
    //         {
    //             return r with
    //             {
    //                 Context = r.Context with
    //                 {
    //                     Precedence = x.Precedence
    //                 }
    //             };
    //         }
    //
    //         return null;
    //     });

    public static IParser<N> Optional<N>(IParser<N> inner) => 
        Parser.Create(x => inner.Run(x) switch
        {
            {} r => r,
            null => new Result<N>(x, null)
        });

    public static IParser<N> Expand<N>(IParser<N> first, Func<N, IParser<N>> repeatedly) => 
        Parser.Create<N>(x =>
        {
            if (first.Run(x) is not { Context: var x1, Parsing: { } p1 })
            {
                return null;
            }
            
            var val = p1.Val;
            ImmutableArray<Parsing> acParsed = [p1];

            while (true)
            {
                if (repeatedly(val).Run(x1) is not { Context: var x2, Parsing: { } p2 })
                {
                    return new Result<N>(x1, Parsing.From(val, acParsed, Addenda.Empty));
                }
                
                x1 = x2;
                val = p2.Val;
                acParsed = [..acParsed, p2];
            }
        });

    public static IParser<ImmutableArray<Node>> AtLeastOne(IParser<Node> inner) =>
        Many(inner, true);
    
    public static IParser<ImmutableArray<N>> Many<N>(IParser<N> inner, bool nullOnEmpty = false) 
        where N : Node => 
        Parser.Create(x =>
        {
            var acNodes = ImmutableArray<N>.Empty;
            var acParsed = ImmutableArray<Parsing>.Empty;
            var certainty = 1D;
            
            while (certainty >= 1 
                   && inner.Run(x) is { Context: var x1, Parsing: { Val: {} val } parsing })
            {
                (acParsed, acNodes) = val switch
                {
                    Node.Syntax => (acParsed.Add(parsing), acNodes),
                    _ => (acParsed.Add(parsing), acNodes.Add(val))
                };
                x = x1;
                certainty *= parsing.Addenda.Certainty;
            }

            if (nullOnEmpty && acNodes.IsEmpty)
            {
                return null;
            }

            return new Result<ImmutableArray<N>>(
                x, 
                Parsing.From(acNodes, acParsed, Addenda.Empty)
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
                    
                    case (_, { Parsing.Addenda.Certainty: 1 } p):
                        return p; 
                    
                    case (null, {} p):
                        best = p;
                        break;
                    
                    case ({ Parsing.Addenda.Certainty: var bestCertainty }, { Parsing.Addenda.Certainty: var certainty } p) 
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
                    Parsing.From(token, text, Addenda.Empty)
                );
            }

            return null;
        });
    
    /* TODO
     * for Expect to be able to capture a slim Extent dynamically
     * we need DYNAMIC LEXING!
     * no way round this
     * as an expectation can't be known till we parse
     *
     * but - parsing requires jump-backability
     * and our Extents are currently mutable
     * No! we just need Splits
     * they become 'proper' immovable mutables only on sealing, I believe
     *
     * there we go then...
     * DYNAMIC LEXING IS NEEDED!
     * create SPLITS as we go
     *
     * which will allow us top flexibility in articulating Extents
     * which is unavoidable for the Expect function
     * so there we have it yes
     */
        
    public static IParser<Node.Expect> Expect(string expectation)
        => Return(new Node.Expect()).WithError(expectation);

    public static IParser<N> Return<N>(N node) => 
        Parser.Create(x => 
            new Result<N>(x, Parsing.From(node, [], Addenda.Empty))
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
