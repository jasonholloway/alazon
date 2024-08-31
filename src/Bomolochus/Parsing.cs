using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public interface Parsed
{
    double Certainty { get; }
    Extent Centre { get; }
    Extent Left { get; }
    Extent Right { get; }
    Addenda Addenda { get; }
    IEnumerable<Parsed> Upstreams { get; }
}

public interface Parsed<out N> : Parsed
    where N : Parsable
{
    N Value { get; }
}

public interface Parsing
{
    double Certainty { get; }
    Addenda Addenda { get; }
    
    public static Parsing<T> From<T>(T val, Split split, double certainty = 1)
        => new ParsingText<T>(certainty, val, split, IsSpace: val is Token.Space);
    
    public static Parsing<T> From<T>(T val, ImmutableArray<Parsing> upstreams, double certainty = 1)    
        => new ParsingGroup<T>(certainty, val, upstreams);    
}

public interface Parsing<out N> : Parsing
{
    N Val { get; }
}

public interface ParsingText : Parsing
{
    Split Text { get; }
    bool IsSpace { get; }
}

public interface ParsingGroup : Parsing
{
    ImmutableArray<Parsing> Upstreams { get; }
}

public record ParsingGroup<T>(double Certainty, T Val, ImmutableArray<Parsing> Upstreams, Addenda? Addenda = null) 
    : ParsingVal<T>(Certainty, Val, Addenda ?? Addenda.Empty), ParsingGroup;

public record ParsingText<T>(double Certainty, T Val, Split Text, bool IsSpace = false, Addenda? Addenda = null) 
    : ParsingVal<T>(Certainty, Val, Addenda ?? Addenda.Empty), ParsingText;

public abstract record ParsingVal<T>(double Certainty, T Val, Addenda Addenda) : Parsing<T>;