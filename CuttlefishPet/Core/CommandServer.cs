using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;

namespace CuttlefishPet.Core;

/// <summary>
/// Lets a second launch of the exe drive the running instance:
/// `CuttlefishPet.exe shrimp` tosses a treat, `add` / `remove` change the crew, etc.
/// Handy for scripting and remote control; also keeps the app single-instance.
/// </summary>
public sealed class CommandServer : IDisposable
{
    private const string PipeName = "CuttlefishPet.commands";

    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _handle;
    private readonly CancellationTokenSource _cts = new();

    public CommandServer(Dispatcher dispatcher, Action<string> handle)
    {
        _dispatcher = dispatcher;
        _handle = handle;
        Task.Run(ListenLoop);
    }

    /// <summary>Send a command to an already-running instance. False if none answered.</summary>
    public static bool TrySend(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(700);
            using var w = new StreamWriter(client) { AutoFlush = true };
            w.WriteLine(command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);
                using var r = new StreamReader(server);
                string? line = await r.ReadLineAsync(_cts.Token);
                if (!string.IsNullOrWhiteSpace(line))
                    _dispatcher.Invoke(() => _handle(line.Trim().ToLowerInvariant()));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await Task.Delay(250); // pipe hiccup: back off and keep serving
            }
        }
    }

    public void Dispose() => _cts.Cancel();
}
