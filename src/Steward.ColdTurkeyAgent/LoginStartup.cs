using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Steward.ColdTurkeyAgent;

public static class LoginStartup
{
    public static string Identity(string agentId) => "org.steward.coldturkey." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agentId)))[..16].ToLowerInvariant();
    private static (string Executable, List<string> Arguments) Invocation(string configPath)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot determine executable path.");
        var arguments = new List<string>();
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            arguments.Add(Path.Combine(AppContext.BaseDirectory, "Steward.ColdTurkeyAgent.dll"));
        arguments.AddRange(["background", "--log-directory", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "logs")]);
        return (executable, arguments);
    }
    public static XDocument MacDocument(string identity, string executable, IEnumerable<string> arguments) => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
        new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict",
            new XElement("key", "Label"), new XElement("string", identity),
            new XElement("key", "ProgramArguments"), new XElement("array", new[] { executable }.Concat(arguments).Select(a => new XElement("string", a))),
            new XElement("key", "AssociatedBundleIdentifiers"), new XElement("array", new XElement("string", ApplicationPaths.BundleId)),
            new XElement("key", "RunAtLoad"), new XElement("true"),
            new XElement("key", "KeepAlive"), new XElement("false"))));
    public static XDocument WindowsDocument(string userId, string executable, IEnumerable<string> arguments)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, params object[] content) => new(ns + name, content);
        return new(new XDeclaration("1.0", "UTF-8", null), E("Task", new XAttribute("version", "1.2"),
            E("Triggers", E("LogonTrigger", E("Enabled", "true"), E("UserId", userId))),
            E("Principals", E("Principal", new XAttribute("id", "User"), E("UserId", userId), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
            E("Settings", E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"), E("ExecutionTimeLimit", "PT0S")),
            E("Actions", new XAttribute("Context", "User"), E("Exec", E("Command", executable), E("Arguments", string.Join(' ', arguments.Select(QuoteWindows)))))));
    }
    // Windows CommandLineToArgvW quoting, not shell quoting.
    public static string QuoteWindows(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c); slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    private static string MacPath(string identity) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", identity + ".plist");
    private static async Task<string> MacDomainAsync(CancellationToken token) => "gui/" + (await RunAsync("/usr/bin/id", ["-u"], token)).Trim();
    private static void RequireAvailable(string path)
    {
        if (!File.Exists(path)) throw new IOException("Run connection setup first to create config.json.");
        using var lease = AgentConfiguration.AcquireLock(path);
    }
    public static async Task InstallAsync(AgentConfiguration configuration, string path, CancellationToken token)
    {
        RequireAvailable(path);
        var identity = Identity(configuration.AgentId);
        var invocation = Invocation(path);
        if (OperatingSystem.IsMacOS())
        {
            var target = MacPath(identity);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) MacDocument(identity, invocation.Executable, invocation.Arguments).Save(file);
            // StartAsync bootstraps this plist, which also starts the process through RunAtLoad.
        }
        else if (OperatingSystem.IsWindows())
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? throw new IOException("Cannot determine current user SID.");
            var temp = Path.GetTempFileName();
            try
            {
                WindowsDocument(user, invocation.Executable, invocation.Arguments).Save(temp);
                await RunAsync("schtasks.exe", ["/Create", "/TN", identity, "/XML", temp], token);
            }
            finally { File.Delete(temp); }
        }
        else throw new PlatformNotSupportedException("Login installation supports macOS and Windows only.");
        Log.Write("Login startup installed for this user. Keep this folder at its current location.");
        await StartAsync(path, token);
    }
    public static async Task StartAsync(string path, CancellationToken token)
    {
        var current = await BackgroundControl.RequestAsync(path, false, token);
        if (current is "running" or "starting") { Log.Write($"Background agent is already {current}."); return; }
        if (current == "stopping") throw new IOException("The agent is still stopping. Wait for shutdown before starting it.");
        RequireAvailable(path);
        var configuration = AgentConfiguration.Load(path);
        var identity = Identity(configuration.AgentId);
        await VerifyInstallationAsync(identity, path, requireCurrentArguments: true, token);
        if (OperatingSystem.IsMacOS())
        {
            var domain = await MacDomainAsync(token);
            var loaded = await ExecuteAsync("/bin/launchctl", ["print", domain + "/" + identity], token);
            if (loaded.ExitCode == 0) await RunAsync("/bin/launchctl", ["kickstart", domain + "/" + identity], token);
            else await RunAsync("/bin/launchctl", ["bootstrap", domain, MacPath(identity)], token);
        }
        else if (OperatingSystem.IsWindows()) await RunAsync("schtasks.exe", ["/Run", "/TN", identity], token);
        else throw new PlatformNotSupportedException("Background startup supports macOS and Windows only.");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await BackgroundControl.RequestAsync(path, false, token);
            if (status == "running") { Log.Write("Background agent started. Check the log for MQTT connection and grant activity."); Log.Detail(Path.Combine(Path.GetDirectoryName(path)!, "logs", "agent.log")); return; }
            if (status == "stopping") throw new IOException("The background agent stopped during startup. Check logs/agent.log.");
            await Task.Delay(200, token);
        }
        throw new IOException("Startup was requested but readiness was not confirmed within 20 seconds. Check logs/agent.log before trying again.");
    }
    public static async Task StopAsync(string path, CancellationToken token)
    {
        var status = await BackgroundControl.RequestAsync(path, true, token);
        if (status is null)
        {
            try { if (File.Exists(path)) RequireAvailable(path); }
            catch (IOException) { throw new IOException("No controllable background agent found, but config.json is in use. Stop a foreground run with Ctrl+C, or use the older build's login-uninstall before upgrading its startup entry."); }
            Log.Write("Background agent is already stopped."); return;
        }
        Log.Write("Stopping background agent; waiting for any active CT recovery to finish...");
        // Recovery is bounded independently of shutdown; never force-kill a partially applied grant.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(40);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await BackgroundControl.RequestAsync(path, false, token) is null)
            {
                try
                {
                    RequireAvailable(path);
                    Log.Write("Background agent stopped. Login startup remains installed."); return;
                }
                catch (IOException) { }
            }
            await Task.Delay(200, token);
        }
        throw new IOException("Shutdown did not finish within 40 seconds. No process was killed or restarted. Check logs/agent.log.");
    }
    public static async Task RestartAsync(string path, CancellationToken token)
    {
        await StopAsync(path, token);
        await StartAsync(path, token);
    }
    public static async Task UninstallAsync(string path, CancellationToken token)
    {
        await StopAsync(path, token);
        var configuration = AgentConfiguration.Load(path);
        var identity = Identity(configuration.AgentId);
        await VerifyInstallationAsync(identity, path, requireCurrentArguments: false, token);
        if (OperatingSystem.IsMacOS())
        {
            var domain = await MacDomainAsync(token);
            if ((await ExecuteAsync("/bin/launchctl", ["print", domain + "/" + identity], token)).ExitCode == 0)
                await RunAsync("/bin/launchctl", ["bootout", domain + "/" + identity], token);
            File.Delete(MacPath(identity));
        }
        else if (OperatingSystem.IsWindows()) await RunAsync("schtasks.exe", ["/Delete", "/TN", identity, "/F"], token);
        else throw new PlatformNotSupportedException("Login removal supports macOS and Windows only.");
        Log.Write("Login startup removed. Configuration and CT blocks were kept.");
    }
    private static async Task VerifyInstallationAsync(string identity, string path, bool requireCurrentArguments, CancellationToken token)
    {
        var expected = Invocation(path);
        if (OperatingSystem.IsMacOS())
        {
            var file = MacPath(identity);
            if (!File.Exists(file)) throw new IOException("Login startup is not installed. Choose Install login startup first.");
            var document = XDocument.Load(file);
            var key = document.Descendants("key").SingleOrDefault(k => k.Value == "ProgramArguments");
            var values = key?.ElementsAfterSelf().FirstOrDefault()?.Elements("string").Select(e => e.Value).ToArray() ?? [];
            if (values.Length == 0 || values[0] != expected.Executable ||
                (Path.GetFileNameWithoutExtension(expected.Executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase) && (values.Length < 2 || values[1] != expected.Arguments[0])))
                throw new IOException("Login startup points to another agent folder. Uninstall it from that folder before installing here.");
            var association = document.Descendants("key").SingleOrDefault(k => k.Value == "AssociatedBundleIdentifiers")
                ?.ElementsAfterSelf().FirstOrDefault()?.Elements("string").Select(e => e.Value).ToArray() ?? [];
            if (requireCurrentArguments && !association.SequenceEqual(new[] { ApplicationPaths.BundleId }))
                throw new IOException("Login startup lacks the current app association. Uninstall it, then install it again from this folder.");
            if (requireCurrentArguments && !values.Skip(1).SequenceEqual(expected.Arguments))
                throw new IOException("Login startup uses an older invocation. Uninstall it, then install it again from this folder.");
        }
        else if (OperatingSystem.IsWindows())
        {
            var query = await ExecuteAsync("schtasks.exe", ["/Query", "/TN", identity, "/XML"], token);
            if (query.ExitCode != 0) throw new IOException("Login startup could not be found. Choose Install login startup first.");
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            var exec = XDocument.Parse(query.Output).Descendants(ns + "Exec").Single();
            if (!string.Equals(exec.Element(ns + "Command")?.Value, expected.Executable, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Login startup points to another executable. Uninstall it from that folder first.");
            if (Path.GetFileNameWithoutExtension(expected.Executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                && !(exec.Element(ns + "Arguments")?.Value ?? "").StartsWith(QuoteWindows(expected.Arguments[0]) + " ", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Login startup points to another agent assembly. Uninstall it from that folder first.");
            if (requireCurrentArguments && exec.Element(ns + "Arguments")?.Value != string.Join(' ', expected.Arguments.Select(QuoteWindows)))
                throw new IOException("Login startup uses another folder or an older invocation. Uninstall it from its original folder, then reinstall here.");
        }
        else throw new PlatformNotSupportedException("Login management supports macOS and Windows only.");
    }
    private sealed record CommandResult(int ExitCode, string Output, string Error);
    private static async Task<string> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var result = await ExecuteAsync(executable, arguments, token);
        if (result.ExitCode != 0) throw new IOException($"Login management failed: {result.Error.Trim()} (exit {result.ExitCode}).");
        return result.Output;
    }
    private static async Task<CommandResult> ExecuteAsync(string executable, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start login management command.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token); }
        catch { if (!process.HasExited) process.Kill(); throw; }
        return new CommandResult(process.ExitCode, await output, await error);
    }
}
