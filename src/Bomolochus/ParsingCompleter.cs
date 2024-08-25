using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public static class ParsingCompleter
{
    public static Parsed<N> Complete<N>(this Parsing<N> parsing)
        where N : Parsable
    {
        var ac = FoldIntermediates(parsing.Val)(ParsingAcc.Empty, parsing);

        if (parsing.Val is Annotatable a
            && a.Extract() is {} addenda)
        {
            ac = ac with
            {
                Certainty = ac.Certainty * addenda.Certainty,
                Addenda = ac.Addenda + addenda
            };
        }

        var (spaceBefore, innerExtent, spaceAfter) = SeparateExtents(ac.FoundExtents);
        var outerExtent = Extent.From(spaceBefore, Extent.From(innerExtent, spaceAfter));
        
        var parsed = new _Parsed<N>(ac.Certainty, innerExtent, outerExtent, ac.Addenda, parsing.Val, ac.Upstreams.ToArray());
        
        parsed.Extent.BackLink(parsed);
        parsing.Val.BackLink(parsed);
        
        return parsed;
    }

    static (Extent Before, Extent Extent, Extent After) SeparateExtents(ImmutableArray<FoundExtent> foundExtents)
    {
        var start = foundExtents.TakeWhile(f => f.IsSpace).Count();
        var end = foundExtents.Length - foundExtents.Reverse().TakeWhile(f => f.IsSpace).Count();

        return (
            foundExtents[..start].Aggregate(Extent.Empty, (ac, f) => Extent.From(ac, f.Extent)),
            foundExtents[start..end].Aggregate(Extent.Empty, (ac, f) => Extent.From(ac, f.Extent)),
            foundExtents[end..].Aggregate(Extent.Empty, (ac, f) => Extent.From(ac, f.Extent))
            );
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
                        FoundExtents = ac.FoundExtents.Add(new FoundExtent(parsed.OuterExtent, IsSpace: false)),
                        Upstreams = ac.Upstreams.Add(parsed)
                    };
                }

                case ParsingGroup { Upstreams: var upstreams, Addenda: var addenda }:
                {
                    ac = upstreams.Aggregate(ac, FoldIntermediates(root));

                    return ac with
                    {
                        Certainty = ac.Certainty * p.Certainty,
                        Addenda = ac.Addenda + addenda //unsure about this
                    };
                }

                case ParsingText { Text: var text, Addenda: var addenda, IsSpace: var isSpace }:
                {
                    return ac with
                    {
                        Certainty = ac.Certainty * addenda.Certainty,
                        FoundExtents = ac.FoundExtents.Add(new FoundExtent(Extent.From(text.Readable), isSpace)),
                        Addenda = ac.Addenda + addenda
                    };
                }

                default:
                    throw new NotImplementedException();
            }
        };

    record FoundExtent(Extent Extent, bool IsSpace);
    

    record ParsingAcc(double Certainty, ImmutableArray<FoundExtent> FoundExtents, Addenda Addenda, ImmutableArray<Parsed> Upstreams)
    {
        public static readonly ParsingAcc Empty = new(1, [], Addenda.Empty, []);
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