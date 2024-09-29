using Bomolochus.Text;

namespace Bomolochus.Example;

using static ParserOps;

public static class ParserExtensions
{
    public static Parsed<N>? Run<N>(this IParser<N> parser, Readable text)
        where N : Node =>
        parser
            .Run(new Context(TextSplitter.Create(text), [' ', '\t', '\n']))?
            .Parsing?
            .Complete();
}