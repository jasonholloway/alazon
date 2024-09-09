using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;
using NUnit.Framework;

namespace Bomolochus.Text.Tests;

public class TransactionalStackTests
{
    private TransactionalStack<int> _stack;
    
    [SetUp]
    public void Setup()
    {
        _stack = new TransactionalStack<int>(2);
    }
    
    [Test]
    public void PushPops()
    {
        _stack.Push(1);
        _stack.Push(2);
        _stack.Push(3);

        Assert.That(_stack.Pop(), Is.EqualTo(3));
        Assert.That(_stack.Pop(), Is.EqualTo(2));
        Assert.That(_stack.Pop(), Is.EqualTo(1));
    }
    
    [Test]
    public void PushesInIsolation()
    {
        _stack.Push(1);

        var t0 = _stack.StartTransaction(0, 32);
        
        t0.Push(2);
        
        Assert.That(_stack.Pop(), Is.EqualTo(1));
    }
    
    [Test]
    public void PopsFromParent()
    {
        _stack.Push(1);

        var t0 = _stack.StartTransaction(0, 32);
        
        t0.Push(2);
        Assert.That(t0.Pop(), Is.EqualTo(2));
        Assert.That(t0.Pop(), Is.EqualTo(1));
        
        Assert.That(_stack.Pop(), Is.EqualTo(1));
    }
    
    [Test]
    public void PopsThroughParent()
    {
        var t0 = _stack.StartTransaction(0, 4);
        t0.Push(1);

        var t1 = t0.StartTransaction(0, 4);
        t1.Push(2);
        
        var t2 = t1.StartTransaction(0, 4);
        t2.Push(3);
        
        Assert.That(t2.Pop(), Is.EqualTo(3));
        Assert.That(t2.Pop(), Is.EqualTo(2));
        Assert.That(t2.Pop(), Is.EqualTo(1));
    }

    [Test]
    public void PushPopPeek_FromParents()
    {
        var t0 = _stack.StartTransaction(0, 4);
        t0.Push(1);

        var t1 = t0.StartTransaction(0, 4);
        t1.Push(2);

        var t2 = t1.StartTransaction(0, 4);
        t2.Push(3);

        Assert.That(t2.Peek(), Is.EqualTo(3));
        Assert.That(t2.Pop(), Is.EqualTo(3));
        Assert.That(t2.Peek(), Is.EqualTo(2));
        Assert.That(t2.Pop(), Is.EqualTo(2));
        Assert.That(t2.Peek(), Is.EqualTo(1));
        Assert.That(t2.Pop(), Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => t2.Peek());
    }
}