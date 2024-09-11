using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public static class ParserOps 
{
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
                switch ((best, fn.Run(x.StartTransaction())))
                {
                    case (_, null): continue;
                    
                    case (_, { Parsing.Addenda.Certainty: 1 } p):
                        return p.Select(t => (t.Context.Commit(), t.Parsing)); 
                    
                    case (null, {} p):
                        best = p;
                        break;
                    
                    case ({ Parsing.Addenda.Certainty: var bestCertainty }, { Parsing.Addenda.Certainty: var certainty } p) 
                        when certainty > bestCertainty:
                        best = p;
                        break;
                }
            }

            return best?.Select(t => (t.Context.Commit(), t.Parsing))!;
        });

    public static IParser<Readable> MatchSpace()
        => Match(c => c is ' ' or '\t' or '\r' or '\n' or '\f');

    public static IParser<Readable> MatchWord()
        => Match(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
    
    public static IParser<Readable> MatchDigits()
        => Match(c => c is >= '0' and <= '9');
    
    public static IParser<Readable> Match(char @char) 
        => Parser.Create<Readable>(x =>
        {
            if (x.Text.TryReadChar(@char, out var claimed))
            {
                return new Result<Readable>(
                    x, 
                    Parsing.From(claimed, x.Text.Split(), Addenda.Empty)
                );
            }

            return null;
        });

    public static IParser<Readable> Match(Predicate<char> predicate) 
        => Parser.Create<Readable>(x =>
        {
            if (x.Text.TryReadChars(predicate, out var claimed))
            {
                return new Result<Readable>(
                    x, 
                    Parsing.From(claimed, x.Text.Split(), Addenda.Empty)
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

    
    
    
    public record Context(TextSplitter Text)
    {
        public Context StartTransaction()
            => new(Text.StartTransaction());

        public Context Commit()
            => new(Text.Commit());
    }
    
    
    
    
    
    
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
            => fn(x);
    }

    public interface IParser<out N>
    {
        IResult<N>? Run(Context x);
    }
}

public static class ParseResultExtensions 
{
    public static ParserOps.IResult<T2> Select<T, T2>(this ParserOps.IResult<T> result, Func<(ParserOps.Context Context, Parsing<T>? Parsing), (ParserOps.Context, Parsing<T2>?)> map)
    {
        var mapped = map((result.Context, result.Parsing));
        return new ParserOps.Result<T2>(mapped.Item1, mapped.Item2);
    }
}