using System.Reactive.Linq;
using Bomolochus.Example;
using Bomolochus.Text;

namespace Bomolochus.LanguageServer;

public static class Diagnoser
{
    public static IObservable<Document> Diagnose(Uri uri, IObservable<string> texts) =>
        texts
            .Select(ExampleParser.ParseRules.Run)   
            .Select((parsed, version) =>
            {
                var doc = new ParsedDoc(Extent.Combine(parsed.Left, Extent.Combine(parsed.Centre, parsed.Right)), parsed);
                
                return new Document(
                    uri,
                    version,
                    doc,
                    parsed?
                        .EnumerateAll()
                        .SelectMany(p => p.Addenda.Notes.Select(n => (Parsed: p, Note: n)))
                        .Select(tup => new Diagnostic(doc.Extent.GetBoundsOf(tup.Parsed.Centre).ToRange(), tup.Note))
                        .ToArray() ?? []
                );
            });
}