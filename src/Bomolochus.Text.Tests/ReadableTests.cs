using NUnit.Framework;

namespace Bomolochus.Text.Tests;

public class ReadableTests
{
    [Test]
    public void ReadAll_Simple()
    {
        var reader = Readable.From("big dog yaps").GetReader();
        Assert.That(reader.ReadAll(), Is.EqualTo("big dog yaps"));
    }
    
    [Test]
    public void ReadAll_Tree()
    {
        var reader = new ReadableNode(new ReadableNode(Readable.From("big"), Readable.From(" dog")), Readable.From(" yaps")).GetReader();
        Assert.That(reader.ReadAll(), Is.EqualTo("big dog yaps"));
    }
    
    [Test]
    public void ReadAll_Tree_MarksEnd()
    {
        var reader = new ReadableNode(
            Readable.From("woof"),
            Readable.From("")
            ).GetReader();

        reader.TryReadChars(c => true, out _);
        reader.Emit();

        Assert.That(reader.AtEnd, Is.True);
    }
    
    [Test]
    public void ReadIndividualChars()
    {
        var reader = 
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"), 
                    Readable.From("ig")), 
                Readable.From(" d")
            ).GetReader();

        Assert.That(reader.TryReadChar(out var c0));
        Assert.That(c0, Is.EqualTo('b'));
        
        Assert.That(reader.TryReadChar(out var c1));
        Assert.That(c1, Is.EqualTo('i'));
        
        Assert.That(reader.TryReadChar(out var c2));
        Assert.That(c2, Is.EqualTo('g'));
        
        Assert.That(reader.TryReadChar(out var c3));
        Assert.That(c3, Is.EqualTo(' '));
        
        Assert.That(reader.TryReadChar(out var c4));
        Assert.That(c4, Is.EqualTo('d'));
        
        Assert.That(reader.TryReadChar(out _), Is.False);

        Assert.That(reader.AtEnd, Is.True);
    }
    
    [Test]
    public void ReadChars()
    {
        var reader = new TextSplitter(
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"), 
                    Readable.From("ig")), 
                Readable.From(" dog")
            )
        );
        
        Assert.That(reader.TryReadChars(c => c is 'b' or 'i', out var s0), Is.True);
        Assert.That(s0.GetReader().ReadAll(), Is.EqualTo("bi"));
        
        Assert.That(reader.TryReadChars(c => c is 'g', out var s1), Is.True);
        Assert.That(s1.GetReader().ReadAll(), Is.EqualTo("g"));
        
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("big"));
        
        Assert.That(reader.TryReadChars(c => true, out var rest), Is.True);
        Assert.That(rest.GetReader().ReadAll(), Is.EqualTo(" dog"));
        
        Assert.That(reader.TryReadChars(c => true, out _), Is.False);
    }
    
    [Test]
    public void ReadChars_Reset()
    {
        var reader = new TextSplitter(
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"),
                    Readable.From("ig")),
                Readable.From(" dog")
            )
        );
        
        Assert.That(reader.TryReadChars(c => c is 'b' or 'i' or 'g' or ' ', out var s0), Is.True);
        Assert.That(s0.GetReader().ReadAll(), Is.EqualTo("big "));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("big "));

        Assert.That(reader.TryReadChars(c => c is 'd' or 'o' or 'g', out var s1), Is.True);
        Assert.That(s1.GetReader().ReadAll(), Is.EqualTo("dog"));
        
        Assert.That(reader.TryReadChars(c => true, out _), Is.False);
        
        reader.Reset();
        
        Assert.That(reader.TryReadChars(c => c is 'd' or 'o', out var s2), Is.True);
        Assert.That(s2.GetReader().ReadAll(), Is.EqualTo("do"));
        
        Assert.That(reader.TryReadChars(c => true, out var s3), Is.True);
        Assert.That(s3.GetReader().ReadAll(), Is.EqualTo("g"));
        
        Assert.That(reader.TryReadChars(c => true, out _), Is.False);
    }
    
    [Test]
    public void CanCheckpointRestore()
    {
        var readable = R("wo") + (R("of ") + (R("say") + R("s ") + R("the")) + (R(" ") + R("d"))) + R("og sometimes");
        var reader = new ReadableReader(readable);
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r0), Is.True);
        Assert.That(r0.ReadAll(), Is.EqualTo("woof"));
        
        Assert.That(reader.TryReadChars(c => c == ' ', out var r1), Is.True);
        Assert.That(r1.ReadAll(), Is.EqualTo(" "));
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r2), Is.True);
        Assert.That(r2.ReadAll(), Is.EqualTo("says"));
        
        Assert.That(reader.TryReadChars(c => c == ' ', out var r3), Is.True);
        Assert.That(r3.ReadAll(), Is.EqualTo(" "));

        var c0 = reader.Checkpoint();
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r4), Is.True);
        Assert.That(r4.ReadAll(), Is.EqualTo("the"));
        
        Assert.That(reader.TryReadChars(c => c == ' ', out var r5), Is.True);
        Assert.That(r5.ReadAll(), Is.EqualTo(" "));
        
        var c1 = reader.Checkpoint();
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r6), Is.True);
        Assert.That(r6.ReadAll(), Is.EqualTo("dog"));
        
        reader.ResetTo(c1);
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r7), Is.True);
        Assert.That(r7.ReadAll(), Is.EqualTo("dog"));
        
        reader.ResetTo(c0);
        
        Assert.That(reader.TryReadChars(c => c != ' ', out var r8), Is.True);
        Assert.That(r8.ReadAll(), Is.EqualTo("the"));
    }
    
    static Readable R(string s) => Readable.From(s);
}