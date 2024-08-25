using Alazon.Text;

namespace Alazon;

public abstract record Token
{
    public record Space : Token;
    
    public record Semicolon : Token;
    
    public record OpenBracket : Token;
    public record CloseBracket : Token;
    
    public record OpenBrace : Token;
    public record CloseBrace : Token;
    
    public record Name(Readable Readable) : Token;
    
    public record Noise : Token;

    public abstract record Op : Token
    {
        public record Is : Op;
        public record And : Op;
        public record Or : Op;
        public record Dot : Op;
        public record Incr : Op;
        public record Remove : Op;
    };

    public record Value : Token
    {
        public record String(Readable Readable) : Value;
        public record Number(int Val) : Value;
        public record Regex(Readable Readable) : Value;
    }
}