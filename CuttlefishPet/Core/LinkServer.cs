using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;

namespace CuttlefishPet.Core;

/// <summary>
/// A two-way line protocol for other desktop creatures (Desktop Dude). Every line the
/// client sends is one JSON request -- its ops plus where it is -- and every request gets
/// exactly one JSON line back: the tank as it is right now plus anything that happened
/// since the last answer. Request/answer keeps it simple on both sides: no client ever has
/// to read and write the same pipe at the same time.
/// </summary>
public sealed class LinkServer : IDisposable
{
    public const string PipeName = "CuttlefishPet.link";

    private readonly Dispatcher _dispatcher;
    private readonly Func<string, string> _handle;
    private readonly CancellationTokenSource _cts = new();

    public LinkServer(Dispatcher dispatcher, Func<string, string> handle)
    {
        _dispatcher = dispatcher;
        _handle = handle;
        Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);
                using var r = new StreamReader(server);
                using var w = new StreamWriter(server) { AutoFlush = true, NewLine = "\n" };
                while (server.IsConnected && !_cts.IsCancellationRequested)
                {
                    string? line = await r.ReadLineAsync(_cts.Token);
                    if (line == null) break; // client went away
                    string reply = _dispatcher.Invoke(() => _handle(line));
                    await w.WriteLineAsync(reply);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await Task.Delay(250); // broken pipe: wait for the next client
            }
        }
    }

    public void Dispose() => _cts.Cancel();
}
