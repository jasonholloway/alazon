namespace Bomolochus.Text;

public static class TransactionalStack
{
    public static TransactionalStack<T> Create<T>(int initialBufferSize) 
        => new(null, 0, 0, new T[initialBufferSize], 0);
}

public class TransactionalStack<T>
{
    private readonly TransactionalStack<T>? _upstream;
    private readonly int _upstreamVersion;
    private int _fromCursor;
    private T[] _buffer;
    private int _cursor;
    private int _version = 0;
    
    internal TransactionalStack(TransactionalStack<T>? upstream, int upstreamVersion, int fromCursor, T[] buffer, int cursor)
    {
        _upstream = upstream;
        _upstreamVersion = upstreamVersion;
        _fromCursor = fromCursor;
        _buffer = buffer;
        _cursor = cursor;
    }
        
    public void Push(T value)
    {
        if (_buffer.Length <= _cursor)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }
        
        _buffer[_cursor++] = value;
        _version++;
    }

    public T Peek()
    {
        if (_cursor <= 0)
        {
            SuckUp();
            
            if (_cursor <= 0)
            {
                throw new InvalidOperationException();
            }
        }

        _version++;
        return _buffer[_cursor - 1];
    }

    public bool TryPop(out T val)
    {
        if (_cursor <= 0)
        {
            SuckUp();
            
            if (_cursor <= 0)
            {
                val = default!;
                return false;
            }
        }
        
        val = _buffer[--_cursor];
        return true;
    }

    public T Pop()
        => TryPop(out var val) 
            ? val 
            : throw new InvalidOperationException();

    private void SuckUp()
    {
        if (_upstream != null)
        {
            _cursor = _upstream.SuckData(_upstreamVersion, _fromCursor, _buffer);
            _fromCursor -= _cursor;
        }
    }

    private int SuckData(int expectedVersion, int from, Span<T> sink)
    {
        if (expectedVersion != _version)
        {
            throw new InvalidOperationException();
        }
        
        var limit = _cursor + from;

        if (limit <= 0)
        {
            if (_upstream != null)
            {
                return _upstream.SuckData(_upstreamVersion, _fromCursor + limit, sink);
            }

            return 0;
        }
        
        var c = Math.Min(limit, sink.Length >> 1);
        
        _buffer[(limit - c)..limit].CopyTo(sink);
        
        return c;
    }

    private void Update(int expectedVersion, int from, T[] srcBuffer, int srcLen)
    {
        if (expectedVersion != _version)
        {
            throw new InvalidOperationException();
        }
        
        var start = from + _cursor;
        
        if (start <= 0)
        {
            _buffer = srcBuffer;
            _cursor = srcLen;
            _fromCursor += start;
        }
        else
        {
            _cursor = start + srcLen;

            while (_cursor > _buffer.Length)
            {
                Array.Resize<T>(ref _buffer, _buffer.Length * 2);
            }

            srcBuffer.AsSpan(Range.EndAt(srcLen)).CopyTo(_buffer.AsSpan(Range.StartAt(start)));
        }
    }
        
    public TransactionalStack<T> Commit()
    {
        if (_upstream == null)
        {
            throw new InvalidOperationException();
        }
        
        _upstream.Update(_upstreamVersion, _fromCursor, _buffer, _cursor);

        return _upstream;
    }

    public TransactionalStack<T> StartTransaction(int initialPull, int initialSize) 
        => new(this, _version, 0, new T[initialSize], 0);
}