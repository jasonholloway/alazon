using System.Collections.Immutable;

namespace Bomolochus;

using static ParserOps;

public static class ParserFnExtensions
{
    public static IParser<B> Select<A, B>(
        this IParser<A> fn,
        Func<A, B> map) =>
        Parser.Create(x => fn.Run(x) switch
        {
            { Context: var x2, Parsing: var parsed  } =>
                new Result<B>(x2, parsed switch
                {
                    { Val: var val } => Parsing.From(map(val), [parsed], parsed.Addenda),
                    null => null
                }),
            null => null
        });
    
    public static IParser<C> SelectMany<A, B, C>(
        this IParser<A> fn0, 
        Func<A, IParser<B>> map,
        Func<A, B, C> join) => 
        Parser.Create(x =>
        {
            var upstreams = ImmutableArray<Parsing>.Empty;
            
            if (MatchSpace().Run(x) is { Context: var x02, Parsing: var p02 })
            {
                if (p02 is not null)
                {
                    upstreams = upstreams.Add(p02);
                }
                
                //todo should parse in *many* tokens of space
                x = x02;
            }
            
            if (fn0.Run(x) is { Context: var x1, Parsing: var p1 }) //todo parsed might be null!
            {
                if (p1 is not null)
                {
                    upstreams = upstreams.Add(p1);
                }
                
                var pv1 = p1 is { Val: var v1 } ? v1 : default!;
                var fn1 = map(pv1);
                
                if (MatchSpace().Run(x1) is { Context: var x12, Parsing: var p12 })
                {
                    if (p12 is not null)
                    {
                        upstreams = upstreams.Add(p12);
                    }
                    
                    //todo should parse in *many* tokens of space
                    x1 = x12;
                }
                
                if (fn1.Run(x1) is { Context: var x2, Parsing: var p2 })
                {
                    if (p2 is not null)
                    {
                        upstreams = upstreams.Add(p2);
                    }
                    
                    var pv2 = p2 is { Val: var v2 } ? v2 : default!;
                    
                    //todo parse in trailing spaces here
                    
                    return new Result<C>(
                        x2, 
                        (p1, p2) switch
                        {
                            ({} a, {} b) => Parsing.From(
                                join(pv1, pv2), 
                                upstreams, 
                                a.Addenda + b.Addenda
                                ),
                            
                            (null, null) => Parsing.From(
                                join(pv1, pv2), 
                                upstreams,
                                Addenda.Empty
                                ),
                            
                            ({} a, _) => Parsing.From(
                                join(pv1, pv2), 
                                upstreams,
                                a.Addenda
                                ),
                            
                            (_, {} b) => Parsing.From(
                                join(pv1, pv2), 
                                upstreams,
                                b.Addenda
                                )
                        }
                    );
                }
            }

            return null;
        });
}