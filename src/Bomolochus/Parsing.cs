using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public interface Parsed
{
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
    Addenda Addenda { get; }
    
    public static Parsing<T> From<T>(T val, Split split, Addenda addenda)
        => new ParsingText<T>(val, split, IsSpace: val is Token.Space, addenda);
    
    public static Parsing<T> From<T>(T val, ImmutableArray<Parsing> upstreams, Addenda addenda)    
        => new ParsingGroup<T>(val, upstreams, addenda);    
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

public record ParsingGroup<T>(T Val, ImmutableArray<Parsing> Upstreams, Addenda? Addenda = null) 
    : ParsingVal<T>(Val, Addenda ?? Addenda.Empty), ParsingGroup;

public record ParsingText<T>(T Val, Split Text, bool IsSpace = false, Addenda? Addenda = null) 
    : ParsingVal<T>(Val, Addenda ?? Addenda.Empty), ParsingText;

public abstract record ParsingVal<T>(T Val, Addenda Addenda) : Parsing<T>;