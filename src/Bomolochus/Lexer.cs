using Alazon.Text;

namespace Alazon;

public record Lexed(Split Text, Token GenericToken)
{
    public TextVec Size => Text.Readable.Size;
    
    public override string ToString()
        => $"Token({GenericToken.GetType().Name}:{Text.Readable.ReadAll()})";
}

public abstract class Lexer(Readable readable) 
{
    protected readonly TextSplitter Reader = new(readable);

    protected bool TryReadChar(out char claimed)
        => Reader.TryReadChar(out claimed);

    protected bool TryReadChar(char match)
        => TryReadChar(out var claimed) && claimed == match;

    protected bool TryReadChars(Predicate<char> predicate)
        => Reader.TryReadChars(predicate, out _);
    
    protected bool TryReadChars(Predicate<char> predicate, out Readable claimed)
        => Reader.TryReadChars(predicate, out claimed);


    protected bool AtEnd => Reader.AtEnd;
    
    protected Lexed Emit(Token token)
    {
        var text = Reader.Split();
        return new Lexed(text, token);
    }

    protected Lexed? Reset()
    {
        Reader.Reset();
        return null;
    }
}