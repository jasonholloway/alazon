namespace Bomolochus.Text;

public abstract class Extent
{
    internal ParentLink Parent { get; set; }

    private object[] _linked = [];
    public IEnumerable<object> Linked => _linked;

    public void BackLink(object linked)
    {
        _linked = [.._linked, linked];
    }
    
    public string ReadAll() => Readable.ReadAll();

    public static readonly Extent Empty = ExtentEmpty.Instance; 

    public static Extent From(Readable readable)
        => new ExtentLeaf(readable);


    public static Extent Combine(params Extent[] extents)
        => extents.Aggregate(Empty, Combine);
    

    public static Extent Combine(Extent left, Extent right)
    {
        if (left is ExtentEmpty) return right;
        if (right is ExtentEmpty) return left;

        var node = new ExtentNode(left, right);
        left.Parent = new ParentLink(ParentLinkType.Left, node);
        right.Parent = new ParentLink(ParentLinkType.Right, node);
        return node;
    }


    public static Extent operator +(Extent left, Extent right)
        => Combine(left, right);
    
    

    public (TextVec From, TextVec To) GetBoundsOf(Extent target)
    {
        var pos = GetOffsetTo(target);
        return (pos, pos.Append(target.Readable.Size));
    }

    public TextVec GetOffsetTo(Extent target)
    {
        var pos = TextVec.Empty;

        while (!ReferenceEquals(target, this))
        {
            if (!target.Parent.TryResolve(out var parent, out var type))
            {
                throw new Exception("Not in subtree");
            }

            if (type == ParentLinkType.Right)
            {
                pos = parent.Left.Readable.Size.Append(pos);
            }

            target = parent;
        }

        return pos;
    }
    
    
    //
    // public TextVec GetAbsolutePosition()
    // {
    //     var pos = TextVec.Empty;
    //     var curr = this;
    //
    //     while (curr is { Parent: var link } 
    //            && link.TryResolve(out var parent, out var type))
    //     {
    //         if (type == ParentLinkType.Right)
    //         {
    //             pos = parent.Left.Readable.Size.Append(pos);
    //         }
    //
    //         curr = parent;
    //     }
    //
    //     return pos;
    // }

    public Extent FindDescendent(int line, int col)
    {
        var curr = this;

        while (curr is ExtentNode { Left: var l, Right: var r })
        {
            var x = l.Readable.Size;
            
            if (line < x.Lines)
            {
                curr = l;
            }
            else if (line == x.Lines)
            {
                line -= x.Lines;
                
                if (col < x.Cols)
                {
                    curr = l;
                }
                else
                {
                    curr = r;
                    col -= x.Cols;
                }
            }
            else
            {
                curr = r;
                line -= x.Lines;
            }
        }
        
        return curr;
    }
    
    public static Extent Group(Extent left, Extent right)
    {
        if (ReferenceEquals(left, right)) return left;
        
        if (FindApex(left, right) is not ExtentNode apex)
        {
            throw new Exception("Can't find apex");
        }
        
        var seek = left;
        var acc = left;
        var surplus = default(Extent);
        
        while (!ReferenceEquals(seek, apex))
        {
            switch (GetParentNode(seek))
            {
                case (var n, ParentLinkType.Left):
                    n.SwapLeft(acc);
                    acc = n;
                    seek = n;
                    break;
                
                case (var n, ParentLinkType.Right):
                    surplus = Node(n.Left, surplus);
                    seek = n;
                    break;
            }
        }
        
        if (surplus is not null)
        {
            var (oldLeft, _) = apex.SwapLeft(surplus);
            (_, apex) = apex.SwapRight(e => (ExtentNode)Combine(oldLeft, e));
        } 

        acc = seek = right;
        surplus = default;

        while (!ReferenceEquals(seek, apex))
        {
            switch (GetParentNode(seek))
            {
                case (var n, ParentLinkType.Right):
                    n.SwapRight(acc);
                    acc = n;
                    seek = n;
                    break;
                
                case (var n, ParentLinkType.Left):
                    surplus = Node(surplus, n.Right);
                    seek = n;
                    break;
            }
        }
        
        if (surplus is not null)
        {
            var (oldRight, _) = apex.SwapRight(surplus);
            (_, apex) = apex.SwapLeft(e => (ExtentNode)Combine(e, oldRight));
        }

        return apex;

        Extent? Node(Extent? l, Extent? r)
            => (l, r) switch
            {
                (null, null) => null,
                (not null, null) => l,
                (null, not null) => r,
                _ => Combine(l, r)
            };

        (ExtentNode, ParentLinkType) GetParentNode(Extent e)
        {
            if (e.Parent.TryResolve(out var parent, out var type))
            {
                return (parent, type);
            }

            throw new Exception("");
        }
        
        Extent? FindApex(Extent l, Extent r)
            => LastCommonNode(l.LineageFromRoot, r.LineageFromRoot);

        Extent? LastCommonNode(IEnumerable<Extent> lineage0, IEnumerable<Extent> lineage1)
        {
            using var e0 = lineage0.GetEnumerator();
            using var e1 = lineage1.GetEnumerator();

            var last = default(Extent);

            while (e0.MoveNext() && e1.MoveNext() && ReferenceEquals(e0.Current, e1.Current))
            {
                last = e0.Current;
            }

            return last;
        }
    }

    public IEnumerable<Extent> LineageFromRoot
    {
        get
        {
            var ancestors = Parent.TryResolve(out var parent, out _)
                ? parent.LineageFromRoot
                : Enumerable.Empty<Extent>();

            return ancestors.Concat([this]);
        }
    }
    
    public IEnumerable<Extent> LineageToRoot
    {
        get
        {
            var curr = this;

            while (true)
            {
                yield return curr;

                if (!curr.Parent.TryResolve(out var parent, out _))
                {
                    yield break;
                }

                curr = parent;
            }
        }
    }

    public abstract Readable Readable { get; }

    public enum ParentLinkType 
    {
        None,
        Left,
        Right
    }

    internal readonly struct ParentLink(ParentLinkType _type, ExtentNode? _parent)
    {
        private readonly WeakReference<ExtentNode>? _ref
            = _parent is not null 
                ? new WeakReference<ExtentNode>(_parent) 
                : null;

        public bool TryResolve(out ExtentNode parent, out ParentLinkType type)
        {
            type = _type;
            
            if (_ref?.TryGetTarget(out parent!) ?? false)
            {
                return true;
            }

            parent = default!;
            return false;
        }
    }
}

public class ExtentEmpty : Extent
{
    private ExtentEmpty()
    {}

    internal static ExtentEmpty Instance = new();
    
    public override Readable Readable => Readable.Empty;

    public override string ToString()
        => "EMPTY";
};

public class ExtentNode(Extent left, Extent right) : Extent
{
    public Extent Left { get; private set; } = left;
    public Extent Right { get; private set; } = right;

    private Readable _readable = left.Readable + right.Readable;
    public override Readable Readable => _readable;

    public (Extent Old, TNew New) SwapLeft<TNew>(Func<Extent, TNew> getNew)
        where TNew : Extent
    {
        var old = Left;
        var @new = getNew(old);
        @new.Parent = new ParentLink(ParentLinkType.Left, this);
        Left = @new;
        
        Update();
        return (old, @new);
    }
    
    public (Extent Old, TNew New) SwapRight<TNew>(Func<Extent, TNew> getNew)
        where TNew : Extent
    {
        var old = Right;
        var @new = getNew(old);
        @new.Parent = new ParentLink(ParentLinkType.Right, this);
        Right = @new;
        
        Update();
        return (old, @new);
    }
    
    public (Extent Old, TNew New) SwapLeft<TNew>(TNew @new)
        where TNew : Extent
        => SwapLeft(_ => @new);

    public (Extent Old, TNew New) SwapRight<TNew>(TNew @new)
        where TNew : Extent
        => SwapRight(_ => @new);
    
    void Update()
    {
        //todo - updates should be done once!
        //like a transaction of updates
        //otherwise we'll be repeating ourselves with out readditions
        //could do with a marker mechanism to support this
        
        _readable = Left.Readable + Right.Readable;

        if (Parent.TryResolve(out var parent, out _))
        {
            parent.Update();
        }
    }

    public override bool Equals(object? obj)
        => obj switch
        {
            ExtentNode n when n.Left.Equals(Left) && n.Right.Equals(Right)  => true,
            _ => false
        };
    
    public override int GetHashCode()
        => (left.GetHashCode() * 3) + (right.GetHashCode());

    public override string ToString() 
        => $"({Left}, {Right})";
}

public class ExtentLeaf(Readable readable) : Extent
{
    public override Readable Readable => readable;

    public override string ToString() 
        => $"\"{readable.ReadAll()}\"";

    public override bool Equals(object? obj)
        => obj switch
        {
            ExtentLeaf l when l.ReadAll().Equals(ReadAll()) => true,
            _ => false
        };

    public override int GetHashCode()
        => ReadAll().GetHashCode();
}