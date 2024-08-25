namespace Bomolochus.Text;

public abstract record Readable(TextVec Size)
{
    public ReadableReader GetReader() => new(this);
    public string ReadAll() => GetReader().ReadAll();
    
    public static readonly Readable Empty = new ReadableEmpty();

    public static Readable From(string @string)
        => new ReadableBuffer(@string.AsMemory(), new BufferRange(0, @string.Length));

    public static implicit operator Readable(string s) 
        => From(s);
    
    //todo this needs to be append, as ordering actually matters?
    public static Readable operator +(Readable left, Readable right)
        => (left, right) switch
        {
            (ReadableBuffer l, ReadableBuffer r) 
                when r.Buffer.Equals(l.Buffer) && r.Range.Start.Equals(l.Range.End)
                => new ReadableBuffer(l.Buffer, new BufferRange(l.Range.Start, r.Range.End), l.Size.Append(r.Size)),
            
            (ReadableEmpty, ReadableEmpty) => Empty,
            (_, ReadableEmpty) => left,
            (ReadableEmpty, _) => right,
            
            _ => new ReadableNode(left, right)
        };
}

public record ReadableEmpty() : Readable(TextVec.Empty);

public record ReadableBuffer(ReadOnlyMemory<char> Buffer, BufferRange Range, TextVec? Size = null) 
    : Readable(Size ?? TextVec.From(Buffer.Span[Range.Start..Range.End]))
{
    public ReadOnlySpan<char> Span => Buffer.Span[Range.Start..Range.End];

    public bool TrySplit(int pos, out (Readable Left, Readable Right) split)
    {
        var splitIndex = Range.Start + pos;

        if (splitIndex < Range.End)
        {
            split = (
                new ReadableBuffer(Buffer, new BufferRange(Range.Start, splitIndex)), 
                new ReadableBuffer(Buffer, new BufferRange(splitIndex, Range.End))
                );
            return true;
        }
        
        if (splitIndex == Range.End)
        {
            split = (this, Empty);
            return true;
        }

        split = default;
        return false;
    }

    public override string ToString()
        => $"R(\"{Span}\")";
}

public record ReadableNode(Readable Left, Readable Right) : Readable(Left.Size.Append(Right.Size));
