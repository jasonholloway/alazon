using System.Reactive.Linq;
using Alazon.Example;
using Alazon.Text;

namespace Alazon.LanguageServer;

using static EnumerableEx;

public static class Diagnoser
{
    public static IObservable<Document> Diagnose(Uri uri, IObservable<string> docs) =>
        docs
            .Select(ExampleParser.ParseExpression.Run)   
            .Select((parsed, version) => 
                new Document(
                    uri,
                    version,
                    parsed,
                    parsed?
                        .Enumerate()
                        .SelectMany(p => p.Addenda.Notes.Select(n => (Parsed: p, Note: n)))
                        .Select(tup => new Diagnostic(tup.Parsed.Extent.GetAbsoluteRange().ToRange(), tup.Note))
                        .ToArray() ?? []
                )
            );
}


public static class Extensions
{
    public static IEnumerable<Parsed> Enumerate(this Parsed parsed) 
        => Return(parsed)
            .Concat(parsed.Upstreams.SelectMany(Enumerate));
    
    public static Range ToRange(this (TextVec From, TextVec To) tup) 
        => new(
            new Position(tup.From.Lines, tup.From.Cols), 
            new Position(tup.To.Lines, tup.To.Cols)
            );
    
}