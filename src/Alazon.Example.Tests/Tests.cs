using NUnit.Framework;

namespace Alazon.Example.Tests;

public class Tests
{
    [TestCase("123", "Number(123)")]
    [TestCase("Hello", "Ref(Hello)")]
    [TestCase("\"Hello\"", "String(Hello)")]
    [TestCase("Age = 5", "Is(Ref(Age), Number(5))")]
    [TestCase("Name = /^blah.*/", "Is(Ref(Name), Regex(^blah.*))")]
    [TestCase("Cat = Dog = Ape", "Is(Ref(Cat), Is(Ref(Dog), Ref(Ape)))")]
    [TestCase("A & B", "And(Ref(A), Ref(B))")]
    [TestCase("A | B", "Or(Ref(A), Ref(B))")]
    [TestCase("A & B | C", "Or(And(Ref(A), Ref(B)), Ref(C))")]
    [TestCase("A | B & C", "Or(Ref(A), And(Ref(B), Ref(C)))")]
    [TestCase("A = 1 & B = 2", "And(Is(Ref(A), Number(1)), Is(Ref(B), Number(2)))")]
    [TestCase("A = B = 3", "Is(Ref(A), Is(Ref(B), Number(3)))")]
    [TestCase("A = 1 & B = 2 | C", "Or(And(Is(Ref(A), Number(1)), Is(Ref(B), Number(2))), Ref(C))")]
    [TestCase("(A | B) & C", "And(Or(Ref(A), Ref(B)), Ref(C))")]
    [TestCase("((A) | (B = C))", "Or(Ref(A), Is(Ref(B), Ref(C)))")]
    [TestCase("A = (1 & B)", "Is(Ref(A), And(Number(1), Ref(B)))")]
    [TestCase("A.B", "Prop(Ref(A), Ref(B))")]
    [TestCase("A.B.C", "Prop(Prop(Ref(A), Ref(B)), Ref(C))")]
    [TestCase("A.B = C.D", "Is(Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D)))")]
    [TestCase("A.B = 1", "Is(Prop(Ref(A), Ref(B)), Number(1))")]
    [TestCase("A.B = C.D & E.F", "And(Is(Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D))), Prop(Ref(E), Ref(F)))")]
    [TestCase("A = (1|2|3)", "Is(Ref(A), Or(Number(1), Or(Number(2), Number(3))))")]
    [TestCase("***", "!Noise")]
    [TestCase("A = ", "!Is(Ref(A), !?)")]
    [TestCase("", "NULL")]
    public void ParsesExpressions(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Run(text);
        Assert.That(Print(tree), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("Woof(1)", "[Call(Ref(Woof), Number(1))]")]
    [TestCase("Woof(1); Meeow(2)", "[Call(Ref(Woof), Number(1)), Call(Ref(Meeow), Number(2))]")]
    public void ParsesStatements(string text, string expected)
    {
        var tree = ExampleParser.ParseStatements.Run(text);
        Assert.That(Print(tree), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("{ Emit(123) }", "[Rule(NULL, [Call(Ref(Emit), Number(123))])]")]
    [TestCase("0 { Emit(123) }", "[Rule(Number(0), [Call(Ref(Emit), Number(123))])]")]
    [TestCase("A = 1 { Emit(123) }", "[Rule(Is(Ref(A), Number(1)), [Call(Ref(Emit), Number(123))])]")]
    [TestCase("{ Count += 1 }", "[Rule(NULL, [Incr(Ref(Count), Number(1))])]")]
    [TestCase(
        """
        A { 1 }
        B { 2 }
        """
        , "[Rule(Ref(A), [Number(1)]), Rule(Ref(B), [Number(2)])]"
        )]
    public void ParsesRules(string text, string expected)
    {
        var parsed = ExampleParser.ParseRules.Run(text);
        Assert.That(Print(parsed), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("13", "[0,2]Number(13)")]
    [TestCase("A = 7", "[0,5]Is([0,1]Ref(A), [0,1]Number(7))")]
    [TestCase("A = (1 & B)", "[0,11]Is([0,1]Ref(A), [0,7]And([0,1]Number(1), [0,1]Ref(B)))")]
    [TestCase("(1 & 20)", "[0,8]And([0,1]Number(1), [0,2]Number(20))")]
    public void ParsesExpressionsWithSizes(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Run(text);
        Assert.That(Print(tree, PrintFlags.WithSizes), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase(" 13  ", "<0,1-0,3>Number(13)")]
    [TestCase("A = (1 & B)", "<0,0-0,11>Is(<0,0-0,1>Ref(A), <0,4-0,11>And(<0,5-0,6>Number(1), <0,9-0,10>Ref(B)))")]
    [TestCase(
        """
        Z = (
          1 & 2
          )
        """, 
        "<0,0-2,3>Is(<0,0-0,1>Ref(Z), <0,4-2,3>And(<1,2-1,3>Number(1), <1,6-1,7>Number(2)))")]
    [TestCase(
        """
        
         1
          
        """, 
        "<1,1-1,2>Number(1)", Description = "space should be gutterised")]
    public void ParsesExpressionsWithExtents(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Run(text);
        Assert.That(Print(tree, PrintFlags.WithExtents), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("**", "!Noise")]
    [TestCase("Bob = **", "!Is(Ref(Bob), !Noise)")]
    [TestCase("(** = 3)", "!Is(!Noise, Number(3))")]
    [TestCase("(Bob = **) = **", "!Is(!Is(Ref(Bob), !Noise), !Noise)")]
    [TestCase("(Bob = **)", "!Is(Ref(Bob), !Noise)")]
    public void ParseUncertainties(string text, string expected)
    {
        var parsed = ExampleParser.ParseExpression.Run(text);
        Assert.That(Print(parsed), Is.EqualTo(PrepNodeString(expected)));
    }

    [TestCase("(Bob = 123)", 0, 2, "Ref(Bob)")]
    [TestCase("(Bob = 123)", 0, 9, "Number(123)")]
    [TestCase("(Bob = 123)", 0, 5, "Is(Ref(Bob), Number(123))")]
    [TestCase("""
              
              (Bob = 123)
              
              """, 1, 9, "Number(123)")]
    public void FindsNodes(string text, int line, int col, string expected)
    {
        var parsed = ExampleParser.ParseExpression.Run(text);

        var found = parsed.OuterExtent.FindParseds(line, col).FirstOrDefault();
        
        Assert.That(Print(found), Is.EqualTo(PrepNodeString(expected)));
    }

    
    static string Print(object? val, PrintFlags flags = PrintFlags.Default)
        => val switch
        {
            Parsed p => PrintParsed(p, flags),
            
            _ => "NULL"
        };

    [Flags]
    enum PrintFlags
    {
        Default,
        WithSizes,
        WithExtents
    }

    static string PrintParsed(Parsed? parsed, PrintFlags flags = PrintFlags.Default)
        => parsed switch
        {
            Parsed<Parsable> { Value: var v } => PrintNode(v, flags),
            null => "NULL",
            _ => "",
        };

    static string PrintNode(Parsable node, PrintFlags flags)
    {
        var parsed = node.Parsed;
        
        return 
            (flags.HasFlag(PrintFlags.WithExtents)
                && parsed is { Extent: var extent }
                && extent.GetAbsoluteRange() is ({} @from, {} to)
                 ? $"<{@from.Lines},{@from.Cols}-{to.Lines},{to.Cols}>"
                 : "") +
            (flags.HasFlag(PrintFlags.WithSizes)
             && parsed is not null
             && parsed.Extent.Readable.Size is { } vec
                ? $"[{vec.Lines},{vec.Cols}]"
                : "") +
            (parsed?.Certainty < 1 ? "!" : "" ) +
            (node switch
            {
                Node.Ref(var s) => $"Ref({s.ReadAll()})",
                Node.Number(var n) => $"Number({n})",
                Node.String(var s) => $"String({s.ReadAll()})",
                Node.Regex(var s) => $"Regex({s.ReadAll()})",
                Node.Is(var left, var right) => $"Is({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.And(var left, var right) => $"And({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.Or(var left, var right) => $"Or({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.Prop(var left, var right) => $"Prop({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.Rule(var left, var right) => $"Rule({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.Call(var left, var args) => $"Call({PrintNode(left, flags)}, {PrintNode(args[0], flags)})",
                Node.Incr(var left, var right) => $"Incr({PrintNode(left, flags)}, {PrintNode(right, flags)})",
                Node.List(var nodes) => $"[{string.Join(", ", nodes.Select(n => PrintNode(n, flags)))}]",
                Node.Expect => $"?",
                Node.Noise => "Noise",
                Node.Delimiter => "Delimiter",
                Node.Syntax => "Syntax",
                null => "NULL",
                _ => throw new Exception($"Bad value, can't print: {node}")
            });
    }

    static string PrepNodeString(string raw) =>
        string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}