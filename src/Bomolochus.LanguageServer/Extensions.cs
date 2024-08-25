using Bomolochus.Text;

namespace Bomolochus.LanguageServer;

public static class Extensions
{
    public static IEnumerable<Parsed> Enumerate(this Parsed parsed) 
        => EnumerableEx.Return(parsed)
            .Concat(parsed.Upstreams.SelectMany(Enumerate));
    
    public static Range ToRange(this (TextVec From, TextVec To) tup) 
        => new(
            new Position(tup.From.Lines, tup.From.Cols), 
            new Position(tup.To.Lines, tup.To.Cols)
        );
}