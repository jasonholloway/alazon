namespace Bomolochus.Text;

public record TextVec(int Chars, int Lines, int Cols)
{
    public static readonly TextVec Empty = new(0, 0, 0);

    public override string ToString()
        => $"TextVec({Chars},{Lines},{Cols})";

    public TextVec Append(TextVec right)
        => new(
            Chars + right.Chars,
            Lines + right.Lines,
            right.Lines > 0 ? right.Cols : Cols + right.Cols
            );

    public static TextVec From(ReadOnlySpan<char> span)
    {
        var line = 0;
        var col = 0;

        foreach (var c in span)
        {
            switch (c)
            {
                //todo match other line ending combos!
                case '\n': 
                    line++;
                    col = 0;
                    break;
                
                default:
                    col++;
                    break;
            }
        }

        return new TextVec(span.Length, line, col);
    }
}