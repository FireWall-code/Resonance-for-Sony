using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Resonance.Core.Device;

namespace Resonance.Core.Transport;

/// <summary>
/// Local channel between the command line and the running app: the headphones accept a
/// single control connection per PC, so the CLI forwards its arguments to the app when it runs.
/// One request per connection: a JSON array of arguments, answered with "0" (ok), "1" (error) or "2" (app not connected yet)
/// followed by the text to print.
/// </summary>
public static class ControlPipe
{
    public const string Name = "Resonance.Control";

    /// <summary>Serves requests until cancelled. The handler returns the text to print.</summary>
    public static async Task ServeAsync(Func<string[], Task<CommandResult>> handler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try { await server.WaitForConnectionAsync(ct); }
            catch (OperationCanceledException) { await server.DisposeAsync(); return; }
            _ = Task.Run(async () =>
            {
                await using (server)
                {
                    try
                    {
                        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                        var args = JsonSerializer.Deserialize<string[]>(await reader.ReadLineAsync(ct) ?? "[]") ?? [];
                        CommandResult reply;
                        try { reply = await handler(args); }
                        catch (Exception ex) { reply = CommandResult.Fail(ex.Message); }
                        await using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true);
                        await writer.WriteAsync((reply.Ok ? "0" : reply.NotReady ? "2" : "1") + reply.Text);
                        await writer.FlushAsync(ct);
                    }
                    catch (IOException) { /* client went away */ }
                }
            }, ct);
        }
    }

    /// <summary>Sends <paramref name="args"/> to the running app. Returns null when the app is not running.</summary>
    public static async Task<CommandResult?> TrySendAsync(string[] args, TimeSpan timeout)
    {
        using var client = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try { await client.ConnectAsync(300); }
        catch (TimeoutException) { return null; }
        using var cts = new CancellationTokenSource(timeout);
        await using (var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(args));
            await writer.FlushAsync(cts.Token);
        }
        using var reader = new StreamReader(client, Encoding.UTF8);
        var reply = await reader.ReadToEndAsync(cts.Token);
        return reply.Length == 0 ? null : new CommandResult(reply[0] == '0', reply[1..], reply[0] == '2');
    }
}
