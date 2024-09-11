namespace Bomolochus.Text;

public class TextSplitter
{
    private TextSplitter? _parent;
    private ReadableReader _reader;
    private Split? _lastSplit;

    public static TextSplitter Create(Readable readable)
        => new(null, ReadableReader.Create(readable), null);

    
    private TextSplitter(TextSplitter? parent, ReadableReader reader, Split? lastSplit)
    {
        _parent = parent;
        _reader = reader;
        _lastSplit = lastSplit;
    }

    public Split Split()
    {
        var readable = _reader.Emit();
        return _lastSplit = new Split(_lastSplit, readable);
    }

    public void Reset()
        => _reader.Reset();
    
    public bool TryReadChar(char @char, out Readable claimed)
        => _reader.TryReadChar(@char, out claimed);

    public bool TryReadChar(out char @char)
        => _reader.TryReadChar(out @char);

    public bool TryReadChars(Predicate<char> predicate, out Readable claimed)
        => _reader.TryReadChars(predicate, out claimed);

    public string ReadAll()
        => _reader.ReadAll();



    public TextSplitter StartTransaction()
        => new(this, _reader.StartTransaction(), _lastSplit);

    public TextSplitter Commit()
    {
        if (_parent != null)
        {
            _parent._reader = _reader.Commit();
            _parent._lastSplit = _lastSplit;
            return _parent;
        }

        return this;
    }
};