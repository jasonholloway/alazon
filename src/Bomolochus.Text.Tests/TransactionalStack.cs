namespace Bomolochus.Text.Tests;

public class TransactionalStack<T>(int initialBufferSize = 256)
{
    private readonly Transaction _root = new(null, 0, new T[initialBufferSize], 0);

    public void Push(T value)
        => _root.Push(value);

    public T Pop()
        => _root.Pop();

    public Transaction StartTransaction(int pullSize, int initialBufferSize)
        => _root.StartTransaction(pullSize, initialBufferSize);


    public class Transaction(Transaction? upstream, int upstreamCursor, T[] buffer, int cursor)
    {
        private int _childCount = 0;
        
        //todo: check childCount is 0
        //todo: stacked transactions suck data through
        
        public void Push(T value)
        {
            if (buffer.Length <= cursor)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }
            
            buffer[cursor++] = value;
        }

        public T Peek()
        {
            if (cursor <= 0)
            {
                SuckUp();
                
                if (cursor <= 0)
                {
                    throw new InvalidOperationException();
                }
            }

            return buffer[cursor - 1];
        }

        public T Pop()
        {
            if (cursor <= 0)
            {
                SuckUp();
                
                if (cursor <= 0)
                {
                    throw new InvalidOperationException();
                }
            }
            
            return buffer[--cursor];
        }

        private void SuckUp()
        {
            if (upstream != null)
            {
                (upstream, upstreamCursor, cursor) = upstream.SuckData(upstreamCursor, buffer);
            }
        }

        private (Transaction? Upstream, int UpstreamCursor, int Count) SuckData(int suckCursor, Span<T> sink)
        {
            if (suckCursor <= 0)
            {
                if (upstream != null)
                {
                    return upstream.SuckData(upstreamCursor, sink);
                }
                
                return (null, 0, 0);
            }
            
            var c = Math.Min(suckCursor, buffer.Length >> 1);
            var from = suckCursor - c;
            
            buffer[from..suckCursor].CopyTo(sink);

            return (this, from, c);
        }
        
        public void Commit()
        {
            
        }

        public Transaction StartTransaction(int initialPull, int initialSize)
        {
            _childCount++;
            return new Transaction(this, cursor, new T[initialSize], 0);
        }
    }
}