using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public class ParserOps 
{
    public static ParserExp<N> Optional<N>(IParser<N> inner) => 
        new(x => inner.Run(x) switch
        {
            {} r => r,
            null => new Result<N>(x, null)
        });

    public static Parser<N> Expand<N>(IParser<N> first, Func<N, IParser<N>> repeatedly) => 
        new(x =>
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
                    return new Result<N>(x1, Parsing.From(val, acParsed, acParsed.Aggregate(Addenda.Empty, (ac, p) => ac + p.Addenda)));
                }
                
                x1 = x2;
                val = p2.Val;
                acParsed = [..acParsed, p2];
            }
        });

    public static ParserExp<ImmutableArray<N>> ParseEnclosedList<N>(
        IParser<object> parseOpen, 
        IParser<N> parseElement,
        IParser<object> parseDelimiter, 
        IParser<object> parseClose
        ) where N : Node =>
        from open in parseOpen
        from elements in ParseDelimitedList(parseElement, parseDelimiter)
        from close in parseClose
        select elements;
    
    public static Parser<ImmutableArray<N>> ParseDelimitedList<N>(IParser<N> parseElement,
        IParser<object> parseDelimiter)
        => Expand(
            from first in parseElement
            select ImmutableArray.Create(first), 
            ac =>
                from delimiter in parseDelimiter
                from next in parseElement
                select ac.Add(next)
            );

    public static Parser<T> OneOf<T>(params IParser<T>[] fns)
        => new(
            spacing: new Spacing(
                //parse space chars if they appear in _all_ below
                fns.Aggregate(
                    seed: default(IEnumerable<char>), 
                    (ac, f) => ac != null ? ac.Intersect(f.Spacing.SpaceChars) : ac
                    ) ?? [], 
                //respect non-space chars is they appear in _any_ below
                fns.SelectMany(f => f.Spacing.NonSpaceChars)),
            parse: x =>
            {
                IResult<T>? best = null;
                
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

    public static Parser<Readable> MatchWord()
        => Match(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
    
    public static Parser<Readable> MatchDigits()
        => Match(c => c is >= '0' and <= '9');
    
    public static Parser<Readable> Match(char @char) 
        => new(
            parse: x =>
            {
                if (x.Text.TryReadChar(@char, out var claimed))
                {
                    return new Result<Readable>(
                        x, 
                        Parsing.From(claimed, x.Text.Split(), Addenda.Empty)
                    );
                }

                return null;
            },
            spacing: new Spacing([], [@char])
            );

    public static Parser<Readable> Match(Predicate<char> predicate) 
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

    public static Parser<Node> Expect(string expectation)
        => Return<Node>(new Node.Expect()).WithError(expectation);

    public static Parser<N> Return<N>(N node) => 
        Parser.Create(x => 
            new Result<N>(x, Parsing.From(node, [], Addenda.Empty))
        );
    
    
    public record Context(TextSplitter Text, ImmutableHashSet<char> SpaceChars, bool SpaceParsable = true)
    {
        public Context StartTransaction()
            => this with { Text = Text.StartTransaction() };

        public Context Commit()
            => this with { Text = Text.Commit() };
    }
    
    
    
    
    
    
    public interface IResult<out N>
    {
        Context Context { get; }
        Parsing<N>? Parsing { get; }
    }
    
    

    public record Result<N>(Context Context, Parsing<N>? Parsing) : IResult<N>;

    public abstract class Parser
    {
        public static Parser<N> Create<N>(Func<Context, IResult<N>?> fn) 
            => new(fn);

        public static Parser<N> Create<N>(Func<IParser<N>> fn)
            => new(fn);
    }

    public class Parser<N> : Parser, IParser<N>
    {
        private readonly Lazy<(Func<Context, IResult<N>?> Fn, Spacing Spacing)> _lz;

        public Spacing Spacing => _lz.Value.Spacing;
        protected Func<Context, IResult<N>?> Parse => _lz.Value.Fn;

        public Parser(Func<Context, IResult<N>?> parse, Spacing? spacing = null)
        {
            _lz = new Lazy<(Func<Context, IResult<N>?>, Spacing)>(() => 
                (parse, spacing ?? Spacing.Empty)
            );
        }

        public Parser(Func<IParser<N>> parse)
        {
            _lz = new Lazy<(Func<Context, IResult<N>?>, Spacing)>(() =>
            {
                var fn = parse();
                return (x => fn.Run(x), fn.Spacing);
            });
        }

        public IResult<N>? Run(Context x0)
        {
            var x = x0;
            
            x = x with
            {
                SpaceChars = x.SpaceChars.Union(Spacing.SpaceChars).Except(Spacing.NonSpaceChars),
                SpaceParsable = true //todo should be set ol
            };
                
            if (x.SpaceParsable 
                && x.Text.TryReadChars(x.SpaceChars.Contains, out _))
            {
                var space = x.Text.Split();

                if (Parse(x with { SpaceParsable = false }) is { } result)
                {
                    return result.Select(t => (
                        t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars },
                        Parsing.From(
                            t.Parsing!.Val, 
                            [new ParsingText<Readable>(space.Readable, space, true), t.Parsing]
                            )
                        ));
                }
            }

            return Parse(x)?.Select(t => (
                t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars }, 
                t.Parsing)
            );
        }
    }
    
    public record ParserExp<N>(Func<Context, IResult<N>?> parse, Spacing? spacing = null) : IParser<N>
    {
        public IResult<N>? Run(Context x)
            => parse(x);

        public Spacing Spacing => spacing ?? Spacing.Empty;
    }

    public interface IParser<out N>
    {
        IResult<N>? Run(Context x);
        Spacing Spacing { get; }
    }
}

public record Spacing(IEnumerable<char> SpaceChars, IEnumerable<char> NonSpaceChars)
{
    public static readonly Spacing Empty = new([], []);
}

public static class ParseResultExtensions 
{
    public static ParserOps.IResult<T2> Select<T, T2>(this ParserOps.IResult<T> result, Func<(ParserOps.Context Context, Parsing<T>? Parsing), (ParserOps.Context, Parsing<T2>?)> map)
    {
        var mapped = map((result.Context, result.Parsing));
        return new ParserOps.Result<T2>(mapped.Item1, mapped.Item2);
    }
}