using System.Text;

namespace Bomolochus.Text;

    //todo OPTIMISE: allow ReadableNodes to have List<Readable> children
    //with frame state containing integer cursor
    
    //todo OPTIMISE: store a current buffer
    //instead of popping/pushing on every character!

/// <summary>
/// Mutable walker through a Readable to yield spans to read 
/// </summary>
public class ReadableReader
{
    public enum State { ReadLeft, ReadRight }

    private ReadableReader? _parent;
    private TransactionalStack<(State State, Readable Readable)> _stack;
    private Readable _staged;
    
    // private readonly Stack<Checkpointed> _checkpointed = [];
    // public record Checkpointed(Readable Staged, TransactionalStack<(State, Readable)> Stack);

    public static ReadableReader Create(Readable readable)
    {
        var stack = TransactionalStack.Create<(State, Readable)>(16);
        stack.Push((State.ReadLeft, readable));
        return new ReadableReader(null, stack, Readable.Empty);
    }
    
    private ReadableReader(ReadableReader? parent, TransactionalStack<(State, Readable)> stack, Readable staged)
    {
        _parent = parent;
        _stack = stack;
        _staged = staged;
    }

    public Readable Emit()
    {
        var staged = _staged;
        _staged = Readable.Empty;
        return staged;
    }



    public ReadableReader StartTransaction() 
        => new(this, _stack.StartTransaction(0, 16), _staged);
    
    public ReadableReader Commit()
    {
        if (_parent != null)
        {
            _parent._staged = _staged;
            _parent._stack = _stack.Commit();
            return _parent;
        }

        return this;
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

    // public Checkpointed Checkpoint()
    // {
    //     var s = new Checkpointed(_staged, _stack);
    //     _checkpointed.Push(s);
    //     _stack = _stack.StartTransaction(16, 32);
    //     return s;
    // }
    //
    // public void ResetTo(Checkpointed c0)
    // {
    //     Start:
    //     
    //     var s = _checkpointed.Pop();
    //     if(s != c0) goto Start;
    //     
    //     _stack = s.Stack;
    //     _staged = s.Staged;
    // }
}