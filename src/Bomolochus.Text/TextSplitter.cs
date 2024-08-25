namespace Bomolochus.Text;

public class TextSplitter(Readable readable)
{
    private readonly ReadableReader _reader = new(readable);

    private Split? _lastSplit;

    public Split Split()
    {
        var readable = _reader.Emit();
        return _lastSplit = new Split(_lastSplit, readable);
    }

    public void Reset()
        => _reader.Reset();

    public bool AtEnd => _reader.AtEnd;

    public bool TryReadChar(out char @char)
        => _reader.TryReadChar(out @char);

    public bool TryReadChars(Predicate<char> predicate, out Readable claimed)
        => _reader.TryReadChars(predicate, out claimed);

    public string ReadAll()
        => _reader.ReadAll();
};