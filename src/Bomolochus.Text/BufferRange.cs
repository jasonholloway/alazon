namespace Bomolochus.Text;

public readonly struct BufferRange(int start, int end)
{
    public readonly int Start = start;
    public readonly int End = end;
}