using System.Diagnostics;
using System.Reactive.Subjects;
using StreamJsonRpc;

namespace Bomolochus.LanguageServer;

public record Diagnostic(Range Range, string Message);

public record Document(Uri Uri, int Version, Parsed? Parsed, Diagnostic[] Diagnostics);

public class LspServer
{
    public static async Task RunSession(IJsonRpcMessageHandler handler, CancellationToken cancel)
    {
        using var jsonRpc = new JsonRpc(handler);

        using var docs = new Subject<string>();
        
        jsonRpc.AddLocalRpcTarget(
            new RpcReceiver(jsonRpc, Diagnoser.Diagnose), 
            new JsonRpcTargetOptions
            {
                ClientRequiresNamedArguments = true,
            });

        jsonRpc.TraceSource = new TraceSource("LSP", SourceLevels.All);
        jsonRpc.TraceSource.Listeners.Add(new ConsoleTraceListener());
        
        jsonRpc.StartListening();
        
        await jsonRpc.Completion;
    }
}