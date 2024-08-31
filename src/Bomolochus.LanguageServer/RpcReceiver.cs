using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Bomolochus.Text;
using StreamJsonRpc;

namespace Bomolochus.LanguageServer;

public class RpcReceiver(JsonRpc rpc, Func<Uri, IObservable<string>, IObservable<Document>> diagnose) : IDisposable
{
    private readonly ConcurrentBag<IDisposable> _disposables = [];
    private readonly ConcurrentDictionary<Uri, (IObserver<string> Texts, BehaviorSubject<Document> Documents)> _map = [];
    
    [JsonRpcMethod("initialize", ClientRequiresNamedArguments = true)]
    public InitializeResult Initialize(
        int? processId, 
        object clientInfo, 
        string locale, 
        string rootPath, 
        string rootUri, 
        object capabilities,
        string trace,
        object[] workspaceFolders)
    {
        return new InitializeResult(
            new ServerCapabilities(
                new TextDocumentSyncOptions(true, TextDocumentSyncKind.Full),
                true
            ),
            new ServerInfo("minfilterlang", "0.0.1")
        );
    }

    [JsonRpcMethod("initialized", ClientRequiresNamedArguments = true)]
    public void Initialized()
    {
    }
    
    [JsonRpcMethod("textDocument/hover", ClientRequiresNamedArguments = true)]
    public object? Hover(TextDocument textDocument, Position position)
    {
        if(_map.TryGetValue(new Uri(textDocument.uri), out var doc)  
           && doc.Documents.Value is var parsed
           && parsed.Doc.Extent.FindParseds(position.line, position.character).FirstOrDefault() is Parsed<Parsable> found)
        {
            var range = parsed.Doc.Extent.GetBoundsOf(found.Centre);
            
            return new
            {
                contents = Printer.Print(parsed.Doc, found),
                range = new {
                    start = new
                    {
                        line = range.From.Lines,
                        character = range.From.Cols
                    },
                    end = new
                    {
                        line = range.To.Lines,
                        character = range.To.Cols
                    }
                }
            };
        }
        
        return null;
    }

    [JsonRpcMethod("textDocument/didOpen", ClientRequiresNamedArguments = true)]
    public void DidOpen(TextDocument textDocument)
    {
        var uri = new Uri(textDocument.uri);
        
        var docSink = new Subject<string>();
        _disposables.Add(docSink);
        
        var diagnoses = new BehaviorSubject<Document>(new Document(uri, 0, new ParsedDoc(Extent.Empty, null), []));
        _disposables.Add(diagnose(uri, docSink).Subscribe(diagnoses));
        
        _map[uri] = (docSink, diagnoses);
        
        _disposables.Add(diagnoses
            .SelectMany(d => Observable.FromAsync(_ =>
                rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", new
                {
                    uri = d.Uri,
                    version = d.Version,
                    diagnostics = d.Diagnostics
                        .Select(m => new
                        {
                            range = m.Range,
                            message = m.Message
                        }).ToArray()
                }))
            ).Subscribe());
        
        docSink.OnNext(textDocument.text ?? "");
    }
    
    [JsonRpcMethod("textDocument/didChange", ClientRequiresNamedArguments = true)]
    public void DidChange(TextDocument textDocument, ContentChange[] contentChanges)
    {
        if(_map.TryGetValue(new Uri(textDocument.uri), out var found) && found is var (sink, _))
        {
            foreach (var contentChange in contentChanges)
            {
                sink.OnNext(contentChange.text ?? "");
            }
        }
    }
    
    [JsonRpcMethod("$/cancelRequest", ClientRequiresNamedArguments = true)]
    public void CancelRequest(int id)
    {
        //??????
    }

    [JsonRpcMethod("$/setTrace", ClientRequiresNamedArguments = true)]
    public void SetTrace(string value)
    {
        //??????
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}

public record InitializeResult(ServerCapabilities capabilities, ServerInfo serverInfo);

public record ServerInfo(string name, string version);

public record ServerCapabilities(TextDocumentSyncOptions textDocumentSync, bool hoverProvider);

public record TextDocumentSyncOptions(bool openClose, TextDocumentSyncKind change);

public record TextDocument(string uri, string languageId, int version, string? text);

public record Position(int line, int character);
public record Range(Position start, Position end);
public record ContentChange(Range range, int rangeLength, string text);

public enum TextDocumentSyncKind
{
    None = 0,
    Full = 1, //client will send full doc on each change - inefficient but easier to process
    Incremental = 2 //efficient diffs will be sent on change
}
    
