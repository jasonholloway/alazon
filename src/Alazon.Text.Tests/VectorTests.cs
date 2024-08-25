using NUnit.Framework;

namespace Alazon.Text.Tests;

public class VectorTests
{
    [TestCase(0, 0, 0, "woof!", 5, 0, 5)]
    [TestCase(10, 7, 2, "woof!", 15, 7, 7)]
    [TestCase(0, 0, 0,
        """
        moo
        moo
        """,
        7, 1, 3)]
    [TestCase(0, 0, 0, 
        """
        woof
        woof
        woof
        
        """,
        15, 3, 0)]
    public void Progressing(int offset0, int line0, int col0, string fragment, int offset1, int line1, int col1)
        => Assert.That(
            new TextVec(offset0, line0, col0).Append(TextVec.From(fragment)),
            Is.EqualTo(new TextVec(offset1, line1, col1))
            );
}