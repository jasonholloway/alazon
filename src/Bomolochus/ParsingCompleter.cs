using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public static class ParsingCompleter
{
    public static Parsed<N> Complete<N>(this Parsing<N> parsing)
        where N : Parsable
    {
        var ac = Fold(parsing.Val)(FoldAcc.Empty, parsing);

        if (parsing.Val is Annotatable a
            && a.Extract() is {} addenda)
        {
            ac = ac with
            {
                Certainty = ac.Certainty * addenda.Certainty,
                Addenda = ac.Addenda + addenda
            };
        }
        
        var folded = new Folded<N>(
            ac.Certainty, 
            ac.Left.Aggregate(Extent.Empty, Extent.From),
            ac.Centre.Aggregate(Extent.Empty, Extent.From),
            ac.Right.Aggregate(Extent.Empty, Extent.From),
            ac.Addenda, 
            parsing.Val, 
            ac.Upstreams.ToArray());
        
        folded.Centre.BackLink(folded);
        parsing.Val.BackLink(folded);
        
        return folded;
    }

    static Func<FoldAcc, Parsing, FoldAcc> Fold(Parsable rootVal)
        => (ac, p) =>
        {
            switch (p)
            {
                //we've got a node, but it's not our current one
                //so we start a spanking new territorial fold
                case Parsing<Parsable> parsing when !ReferenceEquals(parsing.Val, rootVal):
                {
                    var parsed = Complete(parsing);

                    switch (ac.Mode)
                    {
                        case FoldMode.Naive:
                            ac = ac with
                            {
                                Left = ac.Left.Add(parsed.Left),
                                Centre = ac.Centre.AddRange(ac.Right).Add(parsed.Centre),
                                Right = [parsed.Right]
                            };
                            break;
                        
                        case FoldMode.FoundRoot:
                            ac = ac with
                            {
                                Centre = ac.Centre.AddRange(ac.Right).Add(parsed.Left).Add(parsed.Centre),
                                Right = [parsed.Right]
                            };
                            break;
                    }

                    return ac with
                    {
                        Certainty = ac.Certainty * parsed.Certainty,
                        Upstreams = ac.Upstreams.Add(parsed)
                    };
                }
                
                //must be a parsing of our root, but not actual tokens
                //so we fold through upstreams
                case ParsingGroup pg:
                {
                    var inner = pg.Upstreams.Aggregate(FoldAcc.Empty, Fold(rootVal));

                    var isRoot = ac.Mode == FoldMode.Naive 
                                 && inner.Mode == FoldMode.Naive
                                 && pg is Parsing<Parsable> pp
                                 && ReferenceEquals(pp.Val, rootVal);
                    
                    if (isRoot)
                    {
                        ac = ac with
                        {
                            Mode = FoldMode.FoundRoot,
                            Centre = ac.Centre.AddRange(ac.Right).AddRange(inner.Left).AddRange(inner.Centre),
                            Right = inner.Right
                        };
                    }
                    else
                    {
                        switch (ac.Mode)
                        {
                            case FoldMode.Naive:
                                ac = ac with
                                {
                                    Mode = inner.Mode,
                                    Left = ac.Left.AddRange(inner.Left),
                                    Centre = ac.Centre.AddRange(ac.Right).AddRange(inner.Centre),
                                    Right = inner.Right
                                };
                                break;
                            
                            case FoldMode.FoundRoot:
                                ac = ac with
                                {
                                    Mode = inner.Mode,
                                    Left = ac.Left.AddRange(inner.Left),
                                    Centre = ac.Centre.AddRange(ac.Right).AddRange(inner.Centre),
                                    Right = inner.Right
                                };
                                break;
                        }
                        
                        
                    }

                    return ac with
                    {
                        Certainty = ac.Certainty * inner.Certainty * pg.Addenda.Certainty,
                        Upstreams = ac.Upstreams.AddRange(inner.Upstreams),
                        Addenda = ac.Addenda + inner.Addenda
                    };
                }
                
                case ParsingText { Text: var text, Addenda: var addenda }:
                {
                    var extent = Extent.From(text.Readable);

                    ac = ac.Mode switch
                    {
                        FoldMode.Naive => ac with
                        {
                            Left = ac.Left.Add(extent)
                        },
                        FoldMode.FoundRoot => ac with
                        {
                            Right = ac.Right.Add(extent)
                        }
                    };

                    return ac with
                    {
                        Certainty = ac.Certainty * addenda.Certainty,
                        Addenda = ac.Addenda + addenda
                    };
                }

                default:
                    throw new NotImplementedException();
            }
        };

    record FoldAcc(FoldMode Mode, ImmutableArray<Extent> Left, ImmutableArray<Extent> Centre, ImmutableArray<Extent> Right, double Certainty, Addenda Addenda, ImmutableArray<Parsed> Upstreams)
    {
        public static readonly FoldAcc Empty = new(FoldMode.Naive, [], [], [], 1, Addenda.Empty, []);
    }

    enum FoldMode
    {
        Naive, FoundRoot, Tail
    }
    
    
    
    class Folded<N>(double certainty, Extent left, Extent centre, Extent right, Addenda addenda, N value, Parsed[] upstreams) : Parsed<N> where N: Parsable
    {
        public Extent Left => left;
        public Extent Centre => centre;
        public Extent Right => right;
        
        public Addenda Addenda => addenda;
        public N Value => value;
        public IEnumerable<Parsed> Upstreams => upstreams;
        public double Certainty => certainty;
    }
}