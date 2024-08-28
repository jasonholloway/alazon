using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public static class ParsingCompleter
{
    public static Parsed<N> Complete<N>(this Parsing<N> parsing)
        where N : Parsable
    {
        var ac = new[] { parsing }.Aggregate(ParsingAcc.Empty, FoldIntermediates(parsing.Val));

        if (parsing.Val is Annotatable a
            && a.Extract() is {} addenda)
        {
            ac = ac with
            {
                Certainty = ac.Certainty * addenda.Certainty,
                Addenda = ac.Addenda + addenda
            };
        }
        
        var parsed = new _Parsed<N>(
            ac.Certainty, 
            ac.RootExtent ?? Extent.Empty, 
            ac.AllExtents.Aggregate(Extent.Empty, Extent.From), 
            ac.Addenda, 
            parsing.Val, 
            ac.Upstreams.ToArray());
        
        parsed.Extent.BackLink(parsed);
        parsing.Val.BackLink(parsed);
        
        return parsed;
    }

    static Func<ParsingAcc, Parsing, ParsingAcc> FoldIntermediates(Parsable root)
        => (ac, p) =>
        {
            switch (p)
            {
                case Parsing<Parsable> parsing when !ReferenceEquals(parsing.Val, root):
                {
                    var parsed = Complete(parsing);

                    return ac with
                    {
                        Certainty = ac.Certainty * parsed.Certainty,
                        AllExtents = ac.AllExtents.AddRange(parsed.OuterExtent),
                        Upstreams = ac.Upstreams.Add(parsed)
                    };
                }

                case ParsingGroup pg:
                {
                    var ac2 = pg.Upstreams.Aggregate(ParsingAcc.Empty, FoldIntermediates(root));

                    if (pg is Parsing<Parsable>)
                    {
                        //our value is a Node, therefore if not already set by more local parsing,
                        //we should set the RootExtent to home in on the value
                        ac = ac with
                        {
                            RootExtent = ac.RootExtent ?? ac2.RootExtent ?? ac2.AllExtents.Aggregate(Extent.Empty, Extent.From)
                        };
                    }

                    return ac with
                    {
                        Certainty = ac.Certainty * ac2.Certainty * pg.Addenda.Certainty, //todo not certain about this - needs revisiting
                        AllExtents = ac.AllExtents.AddRange(ac2.AllExtents),
                        Upstreams = ac.Upstreams.AddRange(ac2.Upstreams),
                        Addenda = ac.Addenda + ac2.Addenda
                    };
                }
                
                case ParsingText { Text: var text, Addenda: var addenda }:
                {
                    var extent = Extent.From(text.Readable);
                    
                    return ac with
                    {
                        Certainty = ac.Certainty * addenda.Certainty,
                        AllExtents = ac.AllExtents.Add(extent),
                        Addenda = ac.Addenda + addenda
                    };
                }

                default:
                    throw new NotImplementedException();
            }
        };

    // record FoundExtent(Extent Extent, bool IsSpace);
    

    record ParsingAcc(double Certainty, Extent? RootExtent, ImmutableArray<Extent> AllExtents, Addenda Addenda, ImmutableArray<Parsed> Upstreams)
    {
        public static readonly ParsingAcc Empty = new(1, null, [], Addenda.Empty, []);
    }
    
    class _Parsed<N>(double certainty, Extent innerExtent, Extent outerExtent, Addenda addenda, N value, Parsed[] upstreams) : Parsed<N> where N: Parsable
    {
        public Extent Extent => innerExtent;
        public Extent OuterExtent => outerExtent;
        
        public Addenda Addenda => addenda;
        public N Value => value;
        public IEnumerable<Parsed> Upstreams => upstreams;
        public double Certainty => certainty;
    }
}