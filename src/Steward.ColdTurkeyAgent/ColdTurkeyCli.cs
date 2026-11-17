using System.Diagnostics;
using System.Text;

namespace Steward.ColdTurkeyAgent;

public interface IBlockController
{
    Task SetAsync(string block, bool enabled, CancellationToken token);
}

public interface IColdTurkeyCli : IBlockController
{
    Task<BlockCatalog> DiscoverAsync(CancellationToken token);
    Task<bool> StatusAsync(string block, CancellationToken token);
}

public sealed class ColdTurkeyCli(string? executable = null) : IColdTurkeyCli
{
    public const int CommandTimeoutSeconds = 10;
    private string ExecutablePath => executable ?? (OperatingSystem.IsMacOS()
        ? "/Applications/Cold Turkey Blocker.app/Contents/MacOS/Cold Turkey Blocker"
        : OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cold Turkey", "Cold Turkey Blocker.exe")
            : throw new PlatformNotSupportedException("Cold Turkey supports macOS and Windows."));
    public async Task<(int ExitCode, string Output, string Error)> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(ExecutablePath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(CommandTimeoutSeconds));
        if (!process.Start()) throw new IOException("Could not start CT CLI.");
        var stdout = ReadBoundedAsync(process.StandardOutput, deadline.Token);
        var stderr = ReadBoundedAsync(process.StandardError, deadline.Token);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr);
            return (process.ExitCode, await stdout, await stderr);
        }
        catch (Exception ex)
        {
            var timedOut = deadline.IsCancellationRequested && !token.IsCancellationRequested;
            // Terminate only this invocation, never CT's background enforcement components.
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
            deadline.Cancel();
            try { await Task.WhenAll(stdout, stderr); } catch { }
            if (timedOut) throw new IOException("Cold Turkey CLI timed out after 10 seconds. Check that CT is installed and responding; on Windows, close its UI/tray app before CLI use.", ex);
            throw;
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var output = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (output.Length + count > 16384) throw new IOException("CT output exceeded 16 KiB.");
            output.Append(buffer, 0, count);
        }
        return output.ToString().Trim();
    }
    public bool ParseStatus(string output)
    {
        var enabled = output.Equals("enabled", StringComparison.OrdinalIgnoreCase) || output.Equals("true", StringComparison.OrdinalIgnoreCase);
        var disabled = output.Equals("disabled", StringComparison.OrdinalIgnoreCase) || output.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (enabled == disabled) throw new IOException("Unknown CT status response. Run doctor to inspect CLI output.");
        return enabled;
    }
    public async Task SetAsync(string block, bool enabled, CancellationToken token)
    {
        Log.Write($"{(enabled ? "Enable" : "Disable")}: {Log.Value(block)}");
        var args = enabled ? new[] { "-start", block, "-as-is" } : new[] { "-stop", block };
        var result = await ExecuteAsync(args, token);
        if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.Error)) throw new IOException("CT command failed; baseline recovery required. Use doctor for CLI diagnostics.");
        // A zero exit code does not establish success (CT may report errors on stdout).
        if (await StatusAsync(block, token) != enabled)
            throw new IOException("CT did not confirm the requested enabled state.");
    }
    public async Task<bool> StatusAsync(string block, CancellationToken token)
    {
        var status = await ExecuteAsync(["-status", block], token);
        if (status.ExitCode != 0 || !string.IsNullOrWhiteSpace(status.Error)) throw new IOException("CT status command failed. Run doctor for details.");
        return ParseStatus(status.Output);
    }
    public async Task<BlockCatalog> DiscoverAsync(CancellationToken token)
    {
        var result = await ExecuteAsync(["-list-blocks"], token);
        if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.Error))
            throw new IOException("CT block-list command failed. Run doctor for diagnostics.");
        return BlockDiscovery.Parse(result.Output);
    }
    public async Task DoctorAsync(CancellationToken token)
    {
        Log.Write($"Executable: {Log.Value(ExecutablePath)}; platform={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        var list = await ExecuteAsync(["-list-blocks"], token);
        Log.Write($"CT list exit={list.ExitCode} stdout={Log.Value(list.Output)} stderr={Log.Value(list.Error)}");
        if (list.ExitCode != 0 || !string.IsNullOrWhiteSpace(list.Error)) throw new IOException("CT block-list invocation failed.");
        var catalog = BlockDiscovery.Parse(list.Output);
        catalog.Report();
        foreach (var block in catalog.Resources.SelectMany(r => r.Presets.Select(p => p.Block).Prepend(r.Baseline)))
        {
            var result = await ExecuteAsync(["-status", block], token);
            Log.Write($"block={Log.Value(block)} exit={result.ExitCode} stdout={Log.Value(result.Output)} stderr={Log.Value(result.Error)}");
            if (result.ExitCode != 0 || !string.IsNullOrEmpty(result.Error)) throw new IOException("Status invocation failed.");
            Log.Write($"Parsed enabled={ParseStatus(result.Output)}");
        }
        Log.Write("Read-only checks finished. Status confirms enabled state only. Confirm 03:30 daily baseline autostart and 03:45 allowance refill in CT. Restart the agent after changing Steward-managed blocks.");
    }
}
