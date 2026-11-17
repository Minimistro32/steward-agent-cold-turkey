using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Steward.ColdTurkeyAgent;

// Transient, current-user-only IPC. No PID, session, or control file is persisted.
public sealed class BackgroundControl : IAsyncDisposable
{
    private readonly string pipeName;
    private readonly Action stop;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task listener;
    private int state = 1; // starting, running, stopping
    public BackgroundControl(string configPath, Action stop)
    {
        pipeName = PipeName(configPath); this.stop = stop;
        listener = ListenAsync();
    }
    public void Ready() => Interlocked.CompareExchange(ref state, 2, 1);
    public void Stopping() => Interlocked.Exchange(ref state, 3);
    public static string PipeName(string configPath)
    {
        var path = Path.GetFullPath(configPath);
        if (OperatingSystem.IsWindows()) path = path.ToUpperInvariant();
        return "steward-ct-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..24].ToLowerInvariant();
    }
    private async Task ListenAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    var command = new byte[1];
                    if (await pipe.ReadAsync(command, timeout.Token) != 1 || command[0] is not (1 or 2)) continue;
                    if (command[0] == 2) { Stopping(); stop(); }
                    await pipe.WriteAsync(new byte[] { (byte)Volatile.Read(ref state) }, timeout.Token);
                    await pipe.FlushAsync(timeout.Token);
                }
                catch (IOException) { }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log.Write($"Background control failed ({ex.GetType().Name}); stopping the agent.");
            Stopping(); stop(); throw;
        }
    }
    public static async Task<string?> RequestAsync(string path, bool requestStop, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(path), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(400, timeout.Token);
            await pipe.WriteAsync(new byte[] { requestStop ? (byte)2 : (byte)1 }, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            var response = new byte[1];
            if (await pipe.ReadAsync(response, timeout.Token) != 1) return null;
            return response[0] switch { 1 => "starting", 2 => "running", 3 => "stopping", _ => null };
        }
        catch (TimeoutException) { return null; }
        catch (IOException) { return null; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        try { await listener; }
        finally { lifetime.Dispose(); }
    }
}
