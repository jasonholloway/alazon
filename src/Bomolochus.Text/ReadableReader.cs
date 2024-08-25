using System.Text;

namespace Alazon.Text;

//TextVec's come with Readable?
//
//
//
//
//


//todo should Extents cache their Readable?
//instead of reforming the tree on each use?
//possibly - but we need to have the readable available on Extent construction
//which we might have...

/// <summary>
/// Mutable walker through a Readable to yield spans to read 
/// </summary>
public class ReadableReader(Readable readable)
{
    enum State { ReadLeft, ReadRight }
    
    private readonly Stack<(State State, Readable Readable)> _stack = new([(State.ReadLeft, readable)]);
    private Readable _staged = Readable.Empty;
    
    //todo OPTIMISE: allow ReadableNodes to have List<Readable> children
    //with frame state containing integer cursor
    
    //todo OPTIMISE: store a current buffer
    //instead of popping/pushing on every character!

    public Readable Emit()
    {
        var staged = _staged;
        _staged = Readable.Empty;
        return staged;
    }

    public void Reset()
    {
        Push(_staged);
        _staged = Readable.Empty;
    }

    public bool AtEnd {
        get
        {
            while (TryPop(out var buffer))
            {
                if (!buffer.Span.IsEmpty)
                {
                    Push(buffer);
                    return false;
                }
            }

            return true;
        }
    }

    public TAc Visit<TAc>(TAc seed, Visitor<TAc> visitor)
    {
        var ac = seed;
        
        while (TryPop(out var buffer))
        {
            ac = visitor(ac, buffer.Span);
        }

        return ac;
    }

    public delegate TAc Visitor<TAc>(TAc ac, ReadOnlySpan<char> span);
    

    void Push(Readable readable)
    {
        if (readable != Readable.Empty)
        {
            _stack.Push((State.ReadLeft, readable));
        }
    }

    bool TryPop(out ReadableBuffer buffer)
    {
        while (_stack.TryPop(out var frame))
        {
            switch (frame)
            {
                case (_, ReadableEmpty):
                    continue;
                
                case (_, ReadableBuffer leaf):
                    buffer = leaf;
                    return true;

                case (State.ReadLeft, ReadableNode node):
                {
                    _stack.Push((State.ReadRight, node));
                    _stack.Push((State.ReadLeft, node.Left));
                    continue;
                }

                case (State.ReadRight, ReadableNode node):
                {
                    _stack.Push((State.ReadLeft, node.Right));
                    continue;
                }

                default: throw new NotImplementedException();
            }
        }
        
        buffer = default!;
        return false;
    }



    public bool TryReadChar(out char @char)
    {
        while (TryPop(out var buff))
        {
            var span = buff.Span;

            if (buff.TrySplit(1, out var split))
            {
                @char = span[0];
                _staged += split.Left;
                Push(split.Right);
                return true;
            }
        }

        @char = default;
        return false;
    }
    
    public bool TryReadChars(Predicate<char> predicate, out Readable claimed)
    {
        claimed = Readable.Empty;
        
        while (TryPop(out var buff))
        {
            var i = 0;
            var span = buff.Span;
            
            for (; i < span.Length && predicate(span[i]); i++) {}

            if (i > 0 && buff.TrySplit(i, out var split))
            {
                claimed += split.Left;
                Push(split.Right);
            }
            else
            {
                Push(buff);
                break;
            }
        }

        _staged += claimed;
        return claimed != Readable.Empty;
    }

    public string ReadAll()
        => Visit(new StringBuilder(), (sb, span) => sb.Append(span)).ToString();
}