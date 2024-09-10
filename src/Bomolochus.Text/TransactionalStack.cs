namespace Bomolochus.Text;

public static class TransactionalStack
{
    public static Transaction<T> Create<T>(int initialBufferSize)
        => new(null, 0, 0, new T[initialBufferSize], 0);

    public class Transaction<T>(Transaction<T>? upstream, int upstreamVersion, int fromCursor, T[] buffer, int cursor)
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

        public bool TryPop(out T val)
        {
            if (cursor <= 0)
            {
                SuckUp();
                
                if (cursor <= 0)
                {
                    val = default!;
                    return false;
                }
            }
            
            val = buffer[--cursor];
            return true;
        }

        public T Pop()
            => TryPop(out var val) 
                ? val 
                : throw new InvalidOperationException();

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
                cursor = srcLen;
                fromCursor += start;
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

        public Transaction<T> StartTransaction(int initialPull, int initialSize) 
            => new(this, _version, 0, new T[initialSize], 0);
    }
}