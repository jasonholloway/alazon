using System.Reactive.Linq;
using Bomolochus.Example;

namespace Bomolochus.LanguageServer;

public static class Diagnoser
{
    public static IObservable<Document> Diagnose(Uri uri, IObservable<string> docs) =>
        docs
            .Select(ExampleParser.ParseRules.Run)   
            .Select((parsed, version) => 
                new Document(
                    uri,
                    version,
                    parsed,
                    parsed?
                        .EnumerateAll()
                        .SelectMany(p => p.Addenda.Notes.Select(n => (Parsed: p, Note: n)))
                        .Select(tup => new Diagnostic(tup.Parsed.Extent.GetAbsoluteRange().ToRange(), tup.Note))
                        .ToArray() ?? []
                )
            );
}