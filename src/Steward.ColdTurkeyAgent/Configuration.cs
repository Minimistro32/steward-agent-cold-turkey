using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Steward.ColdTurkeyAgent;

public sealed class AgentConfiguration
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public string AgentId { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public bool Tls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public static string DefaultPath => Path.Combine(ApplicationPaths.DataDirectory, "config.json");
    public const string DeviceId = "computer";
    public static string DeviceName => Environment.MachineName;
    public static string DefaultAgentId(string deviceName)
    {
        var name = Regex.Replace(deviceName, "[^a-zA-Z0-9-]+", "-").Trim('-');
        if (name.Length == 0) name = "Computer";
        return "Cold-Turkey-" + name[..Math.Min(name.Length, 116)];
    }
    public static AgentConfiguration Load(string path)
    {
        if (!File.Exists(path)) throw new IOException("No config.json found in the data folder. Use Connection settings or Import existing config.");
        string contents;
        try { contents = File.ReadAllText(path); }
        catch (IOException ex) { throw new IOException("Cannot read config.json. Stop the background agent before changing settings or resources (Ctrl+C for a foreground run), and check file access.", ex); }
        var configuration = JsonSerializer.Deserialize<AgentConfiguration>(contents, Json)
            ?? throw new ArgumentException("Empty config.json. Run setup to create a fresh configuration.");
        configuration.Validate();
        return configuration;
    }
    public static void ValidateHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Any(char.IsWhiteSpace) ||
            !(IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) == UriHostNameType.Dns))
            throw new ArgumentException("MQTT host must be a hostname or IP address only (for example 192.168.50.60). Put 1883 in port; do not include mqtt://, a path, or :port in host.");
    }
    public void Validate()
    {
        foreach (var id in new[] { AgentId, InstanceId })
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsControl) || id.IndexOfAny(['/', '+', '#']) >= 0)
                throw new ArgumentException("agentId and instanceId must be nonempty, at most 128 characters, without MQTT separators or control characters.");
        ValidateHost(Host);
        if (Port is < 1 or > 65535) throw new ArgumentException("MQTT port must be between 1 and 65535.");
        if (Username is not null && string.IsNullOrWhiteSpace(Username)) throw new ArgumentException("Remove username and password when the broker does not require authentication.");
        if (Password is not null && Username is null) throw new ArgumentException("MQTT password requires a username.");
    }
    public void SaveNew(string path)
    {
        Validate();
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var file = new FileStream(path, options);
        JsonSerializer.Serialize(file, this, Json);
    }
    public void Save(FileStream file)
    {
        Validate();
        // Serialize before touching the file, and retain the same locked file handle.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, Json);
        file.Position = 0;
        file.Write(bytes);
        file.SetLength(bytes.Length);
        file.Flush(flushToDisk: true);
    }
    public static FileStream AcquireLock(string path)
    {
        try { return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("Cannot open config.json exclusively. Stop the running agent or other setup first, and check file access.", ex); }
    }
}

// Runtime data discovered from CT; never serialized into config.json.
public sealed class ResourceDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Baseline { get; init; } = "";
    public List<PresetDefinition> Presets { get; init; } = [];
}
public sealed record PresetDefinition(int Minutes, string Block);
public static class Log
{
    private static readonly JsonSerializerOptions Display = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static void Write(string message) => Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    public static void Detail(string message) => Console.WriteLine("    " + message);
    public static void Break() => Console.WriteLine();
    public static string Value(string? value) => JsonSerializer.Serialize(value, Display);
}
