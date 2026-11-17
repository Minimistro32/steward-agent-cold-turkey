using System.Diagnostics;

namespace Steward.ColdTurkeyAgent;

public static class DesktopActions
{
    public static async Task OpenMenuAsync(CancellationToken token)
    {
        var bundle = ApplicationPaths.BundleDirectory(AppContext.BaseDirectory)
            ?? throw new IOException("This command requires the macOS app bundle.");
        await RunAsync("/usr/bin/open", ["-a", "Terminal", Path.Combine(bundle, "Contents", "Resources", "Open Menu.command")], token);
    }
    public static async Task OpenDataAsync(CancellationToken token)
    {
        var path = ApplicationPaths.DataDirectory;
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsMacOS()) await RunAsync("/usr/bin/open", [path], token);
        else if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });
        else throw new PlatformNotSupportedException("Open data folder supports macOS and Windows.");
    }
    public static void ImportConfiguration(string source, string destination, TextWriter output)
    {
        if (File.Exists(destination)) throw new IOException("A config already exists in the data folder. It was not replaced. Use Connection settings to edit it.");
        var config = AgentConfiguration.Load(Path.GetFullPath(source));
        config.SaveNew(destination);
        output.WriteLine("Imported settings and existing agent/instance IDs. Original file was kept.");
        output.WriteLine($"Saved: {destination}");
    }
    private static async Task RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not open the requested application.");
        await process.WaitForExitAsync(token);
        if (process.ExitCode != 0) throw new IOException($"{Path.GetFileName(executable)} failed (exit {process.ExitCode}).");
    }
}
