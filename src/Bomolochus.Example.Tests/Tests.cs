using NUnit.Framework;

namespace Bomolochus.Example.Tests;

using static Printer;

public class Tests
{
    [TestCase("123", "Number(123)")]
    [TestCase("Hello", "Ref(Hello)")]
    [TestCase("\"Hello\"", "String(Hello)")]
    [TestCase("Age = 5", "Is[Ref(Age), Number(5)]")]
    [TestCase("Name = /^blah.*/", "Is[Ref(Name), Regex(^blah.*)]")]
    [TestCase("Cat = Dog = Ape", "Is[Ref(Cat), Ref(Dog), Ref(Ape)]")]
    [TestCase("A & B", "And[Ref(A), Ref(B)]")]
    [TestCase("A | B", "Or[Ref(A), Ref(B)]")]
    [TestCase("A & B | C", "Or[And[Ref(A), Ref(B)], Ref(C)]")]
    [TestCase("A | B & C", "Or[Ref(A), And[Ref(B), Ref(C)]]")]
    [TestCase("A = 1 & B = 2", "And[Is[Ref(A), Number(1)], Is[Ref(B), Number(2)]]")]
    [TestCase("A = B = 3", "Is[Ref(A), Ref(B), Number(3)]")]
    [TestCase("A = 1 & B = 2 | C", "Or[And[Is[Ref(A), Number(1)], Is[Ref(B), Number(2)]], Ref(C)]")]
    [TestCase("(A | B) & C", "And[Or[Ref(A), Ref(B)], Ref(C)]")]
    [TestCase("((A) | (B = C))", "Or[Ref(A), Is[Ref(B), Ref(C)]]")]
    [TestCase("A = (1 & B)", "Is[Ref(A), And[Number(1), Ref(B)]]")]
    [TestCase("A.B", "Prop(Ref(A), Ref(B))")]
    [TestCase("A.B.C", "Prop(Prop(Ref(A), Ref(B)), Ref(C))")]
    [TestCase("A.B = C.D", "Is[Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D))]")]
    [TestCase("A.B = 1", "Is[Prop(Ref(A), Ref(B)), Number(1)]")]
    [TestCase("A.B = C.D & E.F", "And[Is[Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D))], Prop(Ref(E), Ref(F))]")]
    [TestCase("A = (1|2|3)", "Is[Ref(A), Or[Number(1), Number(2), Number(3)]]")]
    [TestCase("***", "!Noise")]
    [TestCase("A = ", "!Is[Ref(A), !?]")]
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
    [TestCase("A = 3www", "Is(Ref(A), !Noise)")]
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
        Assert.That(Print(tree, Flags.WithSizes), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase(
        """
        Z = (
          1 & 2
          )
        """, 
        "<0,0-2,3>Is[<0,0-0,1>Ref(Z), <0,4-2,3>And[<1,2-1,3>Number(1), <1,6-1,7>Number(2)]]")]
    [TestCase(
        """
        
         1
          
        """, 
        "<1,1-1,2>Number(1)", Description = "space should be gutterised")]
    [TestCase(" 13  ", "<0,1-0,3>Number(13)")]
    [TestCase("A = (1 & B)", "<0,0-0,11>Is[<0,0-0,1>Ref(A), <0,4-0,11>And[<0,5-0,6>Number(1), <0,9-0,10>Ref(B)]]")]
    [TestCase("Z = 1  ", "<0,0-0,5>Is[<0,0-0,1>Ref(Z), <0,4-0,5>Number(1)]")]
    public void ParsesExpressionsWithExtents(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Run(text);
        Assert.That(Print(tree, Flags.WithExtents), Is.EqualTo(PrepNodeString(expected)));
    }
    
    // todo
    // OuterExtent should really have Before and After to allow bubbling up of the excess
    // excess space at the outside of a Parsed should be absorbed into the parent, if the parsed is first or last within the parent
    
    
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

    

    static string PrepNodeString(string raw) =>
        string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}