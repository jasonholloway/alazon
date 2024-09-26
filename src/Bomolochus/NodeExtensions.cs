namespace Bomolochus;

using static ParserOps;

public static class NodeExtensions
{
    public static N WithError<N>(this N node, string message)
        where N : Annotatable
    {
        node.Add(new Addenda(0.5, [message]));
        return node;
    }

    public static Parser<N> WithError<N>(this IParser<N> fn, string message)
        where N : Node =>
        Parser.Create(x =>
        {
            var result = fn.Run(x);

            if (result?.Parsing is Parsing<N> parsing)
            {
                return new Result<N>(
                    result.Context,
                    new ParsingGroup<N>(
                        parsing.Val, 
                        [parsing], 
                        new Addenda(0.5, [message]))
                    );
            }

            return result;
        });
        
}