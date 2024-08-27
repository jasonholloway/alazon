namespace Bomolochus;

public static class Printer
{
    public static string Print(object? val, Flags flags = Flags.Default)
        => val switch
        {
            Parsed p => PrintParsed(p, flags),

            _ => "NULL"
        };

    [Flags]
    public enum Flags
    {
        Default,
        WithSizes,
        WithExtents
    }

    static string PrintParsed(Parsed? parsed, Flags flags = Flags.Default)
        => parsed switch
        {
            Parsed<Parsable> { Value: var v } => PrintNode(v, flags),
            null => "NULL",
            _ => "",
        };

    static string PrintNode(Parsable node, Flags flags)
    {
        var parsed = node?.Parsed;

        return
            (flags.HasFlag(Flags.WithExtents)
             && parsed is { Extent: var extent }
             && extent.GetAbsoluteRange() is ({ } @from, { } to)
                ? $"<{@from.Lines},{@from.Cols}-{to.Lines},{to.Cols}>"
                : "") +
            (flags.HasFlag(Flags.WithSizes)
             && parsed is not null
             && parsed.Extent.Readable.Size is { } vec
                ? $"[{vec.Lines},{vec.Cols}]"
                : "") +
            (parsed?.Certainty < 1 ? "!" : "") +
            (node switch
            {
                Node.Ref(var s) => $"Ref({s.ReadAll()})",
                Node.Number(var n) => $"Number({n})",
                Node.String(var s) => $"String({s.ReadAll()})",
                Node.Regex(var s) => $"Regex({s.ReadAll()})",
                Node.Is(var nodes) => $"Is[{string.Join(", ", nodes.Select(n => PrintNode(n, flags)))}]",
                Node.And(var nodes) => $"And[{string.Join(", ", nodes.Select(n => PrintNode(n, flags)))}]",
                Node.Or(var nodes) => $"Or[{string.Join(", ", nodes.Select(n => PrintNode(n, flags)))}]",
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
}
