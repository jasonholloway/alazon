using NUnit.Framework;

namespace Bomolochus.Text.Tests;

public class ExtentTests
{
    [Test]
    public void ReadsIntoSplits()
    {
        var reader = new TextSplitter("kitten mews meekly");

        Assert.That(reader.TryReadChars(c => c != ' ', out var part0) && part0.ReadAll() == "kitten");
        Assert.That(reader.TryReadChars(c => c == ' ', out var part1) && part1.ReadAll() == " ");
        Assert.That(reader.TryReadChars(c => c != ' ', out var part2) && part2.ReadAll() == "mews");
        var split0 = reader.Split();
        
        Assert.That(reader.TryReadChars(c => c == ' ', out var part3) && part3.ReadAll() == " ");
        Assert.That(reader.TryReadChars(c => c != ' ', out var part4) && part4.ReadAll() == "meekly");
        Assert.That(reader.TryReadChars(_ => true, out _), Is.False);
        var split1 = reader.Split();
        
        Assert.That(reader.TryReadChars(_ => true, out _), Is.False);

        Assert.That(split0.Readable.ReadAll(), Is.EqualTo("kitten mews"));
        Assert.That(split1.Readable.ReadAll(), Is.EqualTo(" meekly"));
    }
    
    //and given some splits, we can convert them into Extents
    //but doing so involves sealing the parse tree
    //
    //well, we'd walk backwards through the parse tree
    //converting splits to Extents and as we ascend, grouping the Extents under us

    // [Test]
    // public void ConvertsSplitsToExtents()
    // {
    //     var reader = new TextSplitter(Readable.From("kitten mews meekly"));
    //     
    //     Assert.That(reader.TryReadChars(c => c != ' ', out var part0) && part0.ReadAll() == "kitten");
    //     var text0 = reader.Split();
    //     Assert.That(text0.Get().Readable.ReadAll(), Is.EqualTo("kitten"));
    //     
    //     Assert.That(reader.TryReadChars(c => c == ' ', out var part1) && part1.ReadAll() == " ");
    //     var text1 = reader.Split();
    //     Assert.That(text1.Get().Readable.ReadAll(), Is.EqualTo(" "));
    //     
    //     Assert.That(reader.TryReadChars(c => c != ' ', out var part2) && part2.ReadAll() == "mews");
    //     var text2 = reader.Split();
    //     Assert.That(text2.Get().Readable.ReadAll(), Is.EqualTo("mews"));
    //
    //     var extent = reader.BuildExtentTree();
    //     Assert.That(extent.ReadAll(), Is.EqualTo("kitten mews"));
    //     
    //     Assert.That(text0.Get().Readable.ReadAll(), Is.EqualTo("kitten"));
    //     Assert.That(text1.Get().Readable.ReadAll(), Is.EqualTo(" "));
    //     Assert.That(text2.Get().Readable.ReadAll(), Is.EqualTo("mews"));
    // }
    
    // [Test]
    // public void Contains()
    // {
    //     var tree = Extent.From(
    //         Extent.From(
    //             Extent.From((0, 0), "a"),
    //             Extent.From((0, 1), "b")
    //         ),
    //         Extent.From((0, 2), "c")
    //         );
    //
    //     Assert.That(tree.Contains((ExtentLeaf)Right(tree)), Is.True);
    //     Assert.That(tree.Contains((ExtentLeaf)Extent.From((0, 3), "")), Is.False);
    //     Assert.That(tree.Contains((ExtentLeaf)Extent.From((3, 0), "")), Is.False);
    //     Assert.That(tree.Contains((ExtentLeaf)Left(Left(tree))), Is.True);
    //     Assert.That(tree.Contains((ExtentLeaf)Right(Left(tree))), Is.True);
    // }
    
    [Test]
    public void Groups_Leaf()
    {
        var leaf = Extent.From("hello");

        var grouped = Extent.Group(leaf, leaf);

        Assert.That(grouped, Is.EqualTo(leaf));
    }
    
    [Test]
    public void Groups_Simple()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.From("b")
            ),
            Extent.From("c")
            );

        //group b + c
        var grouped = Extent.Group(Right(Left(tree)), Right(tree));

        Assert.That(grouped.ReadAll(), Is.EqualTo("bc"));
        Assert.That(tree.ReadAll(), Is.EqualTo("abc"));
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("b"),
                Extent.From("c")
            )));
        
        Assert.That(tree, Is.EqualTo(
            Extent.Combine(
                Extent.From("a"),
                grouped
            )));
    }
    
    
    
    [Test]
    public void Groups_TwoSurplus()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.Combine(
                    Extent.From("b"),
                    Extent.From("c")
                    )
            ),
            Extent.From("d")
            );

        //group c + d
        var grouped = Extent.Group(Right(Right(Left(tree))), Right(tree));
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("c"),
                Extent.From("d")
            )));
    }
    
    [Test]
    public void Groups_SubTrees()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.Combine(
                    Extent.From("b"),
                    Extent.From("c")
                    )
            ),
            Extent.Combine(
                Extent.Combine( 
                    Extent.From("d"),
                    Extent.From("e")
                    ),
                Extent.From("f")
            )
        );

        //group c + d
        var grouped = Extent.Group(
            Right(Right(Left(tree))), //c
            Left(Right(tree)) //d,e
            );
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("c"),
                Extent.Combine(
                    Extent.From("d"),
                    Extent.From("e")
                )
            )));
    }

    static Extent Left(Extent ex) => ((ExtentNode)ex).Left;
    static Extent Right(Extent ex) => ((ExtentNode)ex).Right;
}