using NUnit.Framework;

namespace Alazon.Example.Tests;

public class LexerTests
{
    [Test]
    public void SingleLine()
    {
        var lexed = new ExampleLexer("woof 123; ***").Lex();
        
        Assert.That(lexed
                .Select(l => (l.ToString(), l.Size.ToString())),
            Is.EqualTo(new[]
            {
                ("Token(Name:woof)", "TextVec(4,0,4)"),
                ("Token(Space: )", "TextVec(1,0,1)"),
                ("Token(Number:123)", "TextVec(3,0,3)"),
                ("Token(Semicolon:;)", "TextVec(1,0,1)"),
                ("Token(Space: )", "TextVec(1,0,1)"),
                ("Token(Noise:***)", "TextVec(3,0,3)")
            }));
    }
    
    [Test]
    public void Multiline()
    {
        var lexed = new ExampleLexer(
            """
            woof
            
            123
            """
            ).Lex();
        
        Assert.That(lexed
                .Select(l => (l.ToString(), l.Size.ToString())),
            Is.EqualTo(new[]
            {
                ("Token(Name:woof)", "TextVec(4,0,4)"),
                ("Token(Space:\n\n)", "TextVec(2,2,0)"),
                ("Token(Number:123)", "TextVec(3,0,3)"),
            }));
    }
    
}