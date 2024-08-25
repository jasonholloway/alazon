using Alazon.Text;

namespace Alazon.Example;

public class ExampleLexer(Readable text) : Lexer(text)
{
    public IEnumerable<Lexed> Lex()
    {
        while (!AtEnd)
        {
            yield return 
                LexSpace()
                ?? LexName()
                ?? LexSymbol()
                ?? LexValue()
                ?? LexNoise()
                ?? throw new Exception($"Could not read text '{Reader.ReadAll()}'");
        }
    }
    
    Lexed? LexSpace()
        => TryReadChars(IsSpaceChar)
            ? Emit(new Token.Space())
            : Reset();

    Lexed? LexName() 
        => TryReadChars(IsWordChar, out var claimed)
            ? Emit(new Token.Name(claimed))
            : Reset();

    Lexed? LexSymbol()
        => TryReadChar(out var c) 
            ? c switch
            {
                ';' => Emit(new Token.Semicolon()),
                '(' => Emit(new Token.OpenBracket()),
                ')' => Emit(new Token.CloseBracket()),
                '{' => Emit(new Token.OpenBrace()),
                '}' => Emit(new Token.CloseBrace()),
                '&' => Emit(new Token.Op.And()),
                '|' => Emit(new Token.Op.Or()),
                '=' => Emit(new Token.Op.Is()),
                '.' => Emit(new Token.Op.Dot()),
                '+' => TryReadChar('=') ? Emit(new Token.Op.Incr()) : Reset(),
                //but what about a simple plus? should have a general Try() method that resets after itself
                _ => Reset()
            } 
            : Reset();

    Lexed? LexValue()
        => LexString()
           ?? LexRegex()
           ?? LexNumber();

    Lexed? LexNoise() 
        => TryReadChars(c => !IsSpaceChar(c) && c is not ')' and not '}')
            ? Emit(new Token.Noise()) 
            : Reset();

    Lexed? LexString()
    {
        if (TryReadChar('"') 
            && TryReadChars(c => c != '"', out var str) //todo need to deal with escaped quotes (some kind of context predicate that can reference previous char would do it)
            && TryReadChar('"'))
        {
            return Emit(new Token.Value.String(str));
        }

        return Reset();
    }
    
    Lexed? LexRegex()
    {
        //todo should cope with escaped forward slash
        if (TryReadChar('/') 
            && TryReadChars(c => c != '/', out var str)
            && TryReadChar('/'))
        {
            return Emit(new Token.Value.Regex(str));
        }

        return Reset();
    }

    Lexed? LexNumber()
        => TryReadChars(IsDigitChar, out var claimed)
            ? Emit(new Token.Value.Number(int.Parse(claimed.ReadAll()))) //todo seems like this could be done more efficiently? ie some kind of readable visitor
            : Reset();
    

    static bool IsWordChar(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    static bool IsDigitChar(char c)
        => c is >= '0' and <= '9';
    
    static bool IsSpaceChar(char c) 
        => c is ' ' or '\t' or '\r' or '\n' or '\f';
}