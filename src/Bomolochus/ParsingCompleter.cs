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
                    
                    //so at this point 
                    //the inner thing might possibly need its gutters absorbing - but how so?
                    //
                    //we're folding a new territory surrounding a child value
                    //we haven't found our root yet
                    //because we need to fold before knowing we're root...
                    //almost like the inner needs pre-scanning
                    //
                    //in our case we're the final parsed in the list
                    //but the inner can't know this!
                    //and we've not found our root yet, so we don't know this either
                    //so we don't know enough to shove everything into the same bucket
                    //so we must differentiate by putting into 'registers'
                    //
                    //but our accumulation into registers
                    //needs to also be done by the other sections
                    //ie an enountered token needs to shuffle the registers all back
                    //
                    //could we generalize over the register assignment?
                    //in fact, do we even need to assign them inline? could their processing be deferred?
                    //we'd accumulate triples of extents
                    //and then they'd be finished of finally on completion
                    //ie when we have the whole set
                    //
                    //but we'd need to know more than just the list of them
                    //some of them will be root triples

                    ac = ac.Mode switch
                    {
                        FoldMode.Grasping => ac with
                        {
                            // Left = [..ac.Left, parsed.Left, parsed.Centre, parsed.Right]
                            Left = [..ac.Left, parsed.Left],
                            Centre = [..ac.Centre, parsed.Centre],
                            Right = [..ac.Right, parsed.Right],
                        },
                        FoldMode.Clenched => ac with
                        {
                            Right = [..ac.Right, parsed.Left, parsed.Centre, parsed.Right]
                        },
                        _ => ac
                    };

                    return ac with
                    {
                        Certainty = ac.Certainty * parsed.Certainty,
                        Upstreams = ac.Upstreams.Add(parsed)
                    };
                }
                
                //must be a parsing of our root, but not actual tokens
                //so we fold through upstreams
                case ParsingGroup pg and Parsing<Parsable> pp when ReferenceEquals(pp.Val, rootVal):
                {
                    var inner = pg.Upstreams.Aggregate(FoldAcc.Empty, Fold(rootVal));

                    ac = ac.Mode switch
                    {
                        FoldMode.Grasping => inner.Mode switch
                        {
                            FoldMode.Grasping => ac with
                            {
                                //WE ARE ROOT
                                //everything below us is ours - though spaces could still be separated out here
                                Mode = FoldMode.Clenched,
                                Centre = [..ac.Centre, ..inner.Left, ..inner.Centre, ..inner.Right]
                            },
                            FoldMode.Clenched => ac with
                            {
                                //ROOT IS BELOW US
                                //we are intermediate and singular (unless root node is passed back multiple times!)
                                //therefore we can absorb gutters
                                Mode = FoldMode.Clenched,
                                Left = [..ac.Left, ..inner.Left],
                                Centre = [..ac.Centre, ..inner.Centre],
                                Right = inner.Right
                            },
                            _ => throw new NotImplementedException()
                        },
                        FoldMode.Clenched => ac with
                        {
                            //WE ARE PAST ROOT
                            //as such everything goes in right gutter
                            Right = [..ac.Right, ..inner.Left, ..inner.Centre, ..inner.Right]
                        },
                        _ => throw new NotImplementedException()
                    };

                    return ac with
                    {
                        Certainty = ac.Certainty * inner.Certainty * pg.Addenda.Certainty,
                        Upstreams = ac.Upstreams.AddRange(inner.Upstreams),
                        Addenda = ac.Addenda + inner.Addenda
                    };
                }
                
                case ParsingGroup pg and Parsing<Parsable> pp:
                    throw new NotImplementedException("WE ARE NOT ROOT - WHAT TO DO?");
                
                case ParsingText { Text: var text, Addenda: var addenda }:
                {
                    var extent = Extent.From(text.Readable);

                    ac = ac.Mode switch
                    {
                        FoldMode.Grasping => ac with
                        {
                            Left = ac.Left.Add(extent)
                        },
                        FoldMode.Clenched => ac with
                        {
                            Right = ac.Right.Add(extent)
                        },
                        _ => throw new NotImplementedException()
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
        public static readonly FoldAcc Empty = new(FoldMode.Grasping, [], [], [], 1, Addenda.Empty, []);
    }

    record Triple(bool IsRoot, Extent Left, Extent Centre, Extent Right);
    

    enum FoldMode
    {
        Grasping, Clenched
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