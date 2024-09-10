namespace Bomolochus.Text.Tests;

public class TransactionalStack<T>(int initialBufferSize = 256)
{
    private readonly Transaction _root = new(null, 0, 0, new T[initialBufferSize], 0);

    public void Push(T value)
        => _root.Push(value);

    public T Pop()
        => _root.Pop();

    public Transaction StartTransaction(int pullSize, int initialBufferSize)
        => _root.StartTransaction(pullSize, initialBufferSize);


    public class Transaction(Transaction? upstream, int upstreamVersion, int fromCursor, T[] buffer, int cursor)
    {
        private int _version = 0;
        
        public void Push(T value)
        {
            if (buffer.Length <= cursor)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }
            
            buffer[cursor++] = value;
            _version++;
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

            _version++;
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
                cursor = upstream.SuckData(upstreamVersion, fromCursor, buffer);
                fromCursor -= cursor;
            }
        }

        private int SuckData(int expectedVersion, int from, Span<T> sink)
        {
            if (expectedVersion != _version)
            {
                throw new InvalidOperationException();
            }
            
            var limit = cursor + from;

            if (limit <= 0)
            {
                if (upstream != null)
                {
                    return upstream.SuckData(upstreamVersion, limit, sink);
                }

                return 0;
            }
            
            var c = Math.Min(limit, sink.Length >> 1);
            
            buffer[(limit - c)..limit].CopyTo(sink);
            
            return c;
        }

        private void Update(int expectedVersion, int from, T[] srcBuffer, int srcLen)
        {
            if (expectedVersion != _version)
            {
                throw new InvalidOperationException();
            }
            
            var start = from + cursor;
            
            if (start <= 0)
            {
                buffer = srcBuffer;
                fromCursor += start;
                cursor = srcLen;
            }
            else
            {
                cursor = start + srcLen;

                while (cursor > buffer.Length)
                {
                    Array.Resize<T>(ref buffer, buffer.Length * 2);
                }

                srcBuffer.AsSpan(Range.EndAt(srcLen)).CopyTo(buffer.AsSpan(Range.StartAt(start)));
            }
        }
        
        public void Commit()
        {
            if (upstream == null)
            {
                throw new InvalidOperationException();
            }
            
            upstream.Update(upstreamVersion, fromCursor, buffer, cursor);
        }

        public Transaction StartTransaction(int initialPull, int initialSize) 
            => new(this, _version, 0, new T[initialSize], 0);
    }
}