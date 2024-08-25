using System.Collections.Immutable;
using Alazon.Text;

namespace Alazon;

public abstract record Node : Parsable, Annotatable
{
    public record Ref(Readable Readable) : Node;
    public record Call(Ref Name, Node[] Args) : Node;
    public record Incr(Node Left, Node Right) : Node;
    public record Value : Node;
    public record String(Readable Readable) : Value;
    public record Regex(Readable Readable) : Value;
    public record Number(int Val) : Value;
    public record BinaryExpression(Node Left, Node Right) : Node;
    public record Prop(Node Left, Node Right) : BinaryExpression(Left, Right);
    public record Is(Node Left, Node Right) : BinaryExpression(Left, Right);
    public record And(Node Left, Node Right) : BinaryExpression(Left, Right);
    public record Or(Node Left, Node Right) : BinaryExpression(Left, Right);
    
    public record Rule(Node? Condition, Node Statement) : Node;

    public record List(ImmutableArray<Node> Nodes) : Node
    {
        public static List Cons(Node head, List tail) => new(
            ImmutableArray<Node>.Empty
                .Add(head)
                .AddRange(tail.Nodes)
            );
    }

    public record Noise : Node;
    public record Syntax : Node;
    public record Delimiter : Syntax;

    public record Expect : Node;

    #region ParsedNode

    private Parsed? _parsed;

    Parsed? Parsable.Parsed => _parsed;

    public void BackLink(Parsed p)
    {
        _parsed ??= p;
    }

    #endregion

    #region Annotatable
    
    private Addenda _addenda = Addenda.Empty;

    void Annotatable.Add(Addenda addenda)
    {
        _addenda += addenda;
    }

    Addenda Annotatable.Extract()
    {
        var addenda = _addenda;
        _addenda = Addenda.Empty;
        return addenda;
    }

    #endregion
}

public interface Parsable
{
    Parsed? Parsed { get; }
    void BackLink(Parsed p);
}
