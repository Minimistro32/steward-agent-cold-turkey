using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Steward.ColdTurkeyAgent;

public sealed class GeneratorManifest
{
    public string Name { get; set; } = "";
    public List<int> Minutes { get; set; } = [];
    public List<string> Web { get; set; } = [];
    public List<string> Apps { get; set; } = [];
    public List<string> Exceptions { get; set; } = [];
}
public static class PresetGenerator
{
    public const string DefaultException = "file://*.*";
    public static string NormalizeWebsite(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new ArgumentException("Enter a website or pattern, for example example.com.");
        value = value.Trim();
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) value = value[8..];
        else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        else if (value.Contains("://", StringComparison.Ordinal)) return value; // Preserve CT special patterns such as file://*.*.
        value = value.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter a website after the URL prefix, for example example.com.");
        return value;
    }

    public static GeneratorManifest ReadManifest(string path) =>
        JsonSerializer.Deserialize<GeneratorManifest>(File.ReadAllText(path), AgentConfiguration.Json)
        ?? throw new ArgumentException("Empty resource definition.");

    public static (string Path, ResourceDefinition Resource) Generate(GeneratorManifest resource, string directory, BlockCatalog catalog)
    {
        if (resource is null || string.IsNullOrWhiteSpace(resource.Name) || resource.Name.Length > 100 || resource.Name.Any(char.IsControl))
            throw new ArgumentException("Each resource needs a name of 1–100 characters without control characters.");
        var name = resource.Name.Trim();
        // The block name is also the import filename; use the same portable rules on both platforms.
        if (name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 || name.EndsWith('.'))
            throw new ArgumentException("Resource names cannot contain filename characters < > : quotes / backslash | ? * or end with a period.");
        if (resource.Minutes is null || resource.Minutes.Count == 0 || resource.Minutes.Any(m => m is < 1 or > 1440)
            || resource.Minutes.Distinct().Count() != resource.Minutes.Count)
            throw new ArgumentException("Choose distinct allowance minutes from 1 to 1440.");
        ValidateEntries(resource.Web, "websites"); ValidateEntries(resource.Apps, "applications"); ValidateEntries(resource.Exceptions, "exceptions");
        resource = new GeneratorManifest
        {
            Name = resource.Name, Minutes = resource.Minutes, Apps = resource.Apps,
            Web = resource.Web.Select(NormalizeWebsite).ToList(),
            Exceptions = resource.Exceptions.Select(NormalizeWebsite).Prepend(DefaultException).Distinct(StringComparer.Ordinal).ToList()
        };
        if (resource.Web.Count + resource.Apps.Count == 0) throw new ArgumentException("Choose at least one website or application target.");
        if (resource.Apps.Any(app => !(app.StartsWith("app:", StringComparison.Ordinal) || app.StartsWith("file:", StringComparison.Ordinal) || app.StartsWith("folder:", StringComparison.Ordinal))
            || app[(app.IndexOf(':') + 1)..].Length == 0))
            throw new ArgumentException("Application entries must use CT's app:, file:, or folder: format with a nonempty target.");
        var baseline = BlockDiscovery.Prefix + name;
        var blocks = resource.Minutes.Select(m => baseline + "-" + m.ToString("D4", CultureInfo.InvariantCulture)).Prepend(baseline).ToArray();
        if (blocks.Any(b => catalog.Names.Contains(b, StringComparer.Ordinal)))
            throw new ArgumentException("These blocks already exist in Cold Turkey. Finish the existing resource in setup, or choose a new name.");
        var combined = BlockDiscovery.FromNames(catalog.Names.Concat(blocks));
        var mapping = combined.Resources.SingleOrDefault(r => r.Baseline == baseline);
        if (mapping is null || catalog.Resources.Any(r => !combined.Resources.Any(c => c.Baseline == r.Baseline && c.Presets.SequenceEqual(r.Presets))))
            throw new ArgumentException("This resource name would create an ambiguous block group or resource-ID collision. Choose another name.");
        var output = new JsonObject { [baseline] = Block(resource, null) };
        foreach (var preset in mapping.Presets) output.Add(preset.Block, Block(resource, preset.Minutes));
        var path = Path.Combine(directory, baseline + "-import.ctbbl");
        WriteNew(path, output.ToJsonString(AgentConfiguration.Json));
        return (path, mapping);
    }
    public static string ImportInstructions(ResourceDefinition resource) =>
        $"1. Import {resource.Baseline}-import.ctbbl into Cold Turkey.\n\n" +
        $"2. Baseline: {resource.Baseline}\n" +
        "   Set Autostart → Time of Day → 03:30 → every day manually.\n\n" +
        "3. Leave allowance presets disabled, with daily refill at 03:45.\n\n" +
        "4. Confirm below to enable the baseline.\n";

    public static (string Path, ResourceDefinition Resource, IReadOnlyList<PresetDefinition> Added) GenerateAdditional(
        ResourceDefinition resource, IReadOnlyList<int> minutes, string exportPath, string directory, BlockCatalog catalog)
    {
        if (minutes.Count == 0 || minutes.Any(m => m is < 1 or > 1440) || minutes.Distinct().Count() != minutes.Count)
            throw new ArgumentException("Choose distinct allowance minutes from 1 to 1440.");
        if (minutes.Any(m => resource.Presets.Any(p => p.Minutes == m)))
            throw new ArgumentException("One of these allowance options already exists. Enter only new minutes.");
        // Keep output paths safe even for resources created manually in CT.
        if (resource.Name.Length > 100 || resource.Name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0
            || resource.Name.Any(char.IsControl) || resource.Name.EndsWith('.'))
            throw new ArgumentException("This resource name cannot be used as a portable import filename.");
        if (new FileInfo(exportPath).Length > 16 * 1024 * 1024) throw new IOException("CT export exceeds 16 MiB.");
        using var document = JsonDocument.Parse(File.ReadAllText(exportPath));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected a CT export containing named blocks.");
        var properties = document.RootElement.EnumerateObject().ToArray();
        if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
            throw new ArgumentException("CT export contains duplicate block names.");
        if (!document.RootElement.TryGetProperty(resource.Baseline, out var baseline))
            throw new ArgumentException($"Export the current baseline {resource.Baseline} from CT, then select that file.");
        if (baseline.ValueKind != JsonValueKind.Object || baseline.EnumerateObject().Select(p => p.Name).Distinct().Count() != baseline.EnumerateObject().Count())
            throw new ArgumentException("Exported baseline must be a block object without duplicate fields.");
        string Text(string key) => baseline.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new ArgumentException($"Missing or invalid baseline field: {key}.");
        if (Text("type") != "continuous" || Text("break") != "none")
            throw new ArgumentException("Select the export of this resource's continuous baseline with no allowance.");
        // New managed resources currently use all-user scope; do not silently widen an edited resource.
        if (Text("users") != "all" || !baseline.TryGetProperty("customUsers", out var users) || users.ValueKind != JsonValueKind.Array || users.GetArrayLength() != 0)
            throw new ArgumentException("This resource uses custom user scope, which the allowance generator does not support.");
        List<string> EntriesFrom(JsonElement block, string key)
        {
            if (block.ValueKind != JsonValueKind.Object || !block.TryGetProperty(key, out var entries) || entries.ValueKind != JsonValueKind.Array || entries.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
                throw new ArgumentException($"Missing or invalid exported {key} list.");
            var result = entries.EnumerateArray().Select(e => e.GetString()!).ToList();
            ValidateEntries(result, key);
            return result;
        }
        var manifest = new GeneratorManifest
        {
            Name = resource.Name, Web = EntriesFrom(baseline, "web"), Apps = EntriesFrom(baseline, "apps"), Exceptions = EntriesFrom(baseline, "exceptions")
        };
        if (manifest.Web.Count + manifest.Apps.Count == 0) throw new ArgumentException("Exported baseline has no website or application targets.");
        foreach (var preset in resource.Presets)
            if (document.RootElement.TryGetProperty(preset.Block, out var sibling))
                foreach (var field in new[] { "web", "apps", "exceptions" })
                    if (!EntriesFrom(baseline, field).ToHashSet(StringComparer.Ordinal).SetEquals(EntriesFrom(sibling, field)))
                        throw new ArgumentException($"Exported targets differ between the baseline and {preset.Block}. Make the CT resource consistent before adding options.");
        var added = minutes.Order().Select(m => new PresetDefinition(m, resource.Baseline + "-" + m.ToString("D4", CultureInfo.InvariantCulture))).ToArray();
        if (added.Any(p => catalog.Names.Contains(p.Block, StringComparer.Ordinal))) throw new ArgumentException("A new preset name already exists in CT.");
        var combined = BlockDiscovery.FromNames(catalog.Names.Concat(added.Select(p => p.Block)));
        var expected = combined.Resources.SingleOrDefault(r => r.Baseline == resource.Baseline);
        if (expected is null || !expected.Presets.SequenceEqual(resource.Presets.Concat(added).OrderBy(p => p.Minutes))
            || catalog.Resources.Where(r => r.Baseline != resource.Baseline).Any(r => !combined.Resources.Any(c => c.Baseline == r.Baseline && c.Presets.SequenceEqual(r.Presets))))
            throw new ArgumentException("These new presets would create an ambiguous block group.");
        var output = new JsonObject();
        // Preserve the current export's target lists; default exceptions/URL cleanup apply only to new resources.
        foreach (var preset in added) output.Add(preset.Block, Block(manifest, preset.Minutes));
        var stem = resource.Baseline + "-allowances";
        var path = Path.Combine(directory, stem + "-import.ctbbl");
        for (var suffix = 2; File.Exists(path); suffix++) path = Path.Combine(directory, stem + "-" + suffix + "-import.ctbbl");
        WriteNew(path, output.ToJsonString(AgentConfiguration.Json));
        return (path, expected, added);
    }
    private static JsonObject Block(GeneratorManifest resource, int? minutes) => new()
    {
        ["apps"] = Entries(resource.Apps), ["lock"] = "none", ["customUsers"] = new JsonArray(), ["password"] = "",
        ["pomodoroTime"] = "", ["type"] = "continuous", ["exceptions"] = Entries(resource.Exceptions),
        ["lockUnblock"] = "true", ["users"] = "all", ["schedule"] = new JsonArray(), ["web"] = Entries(resource.Web),
        ["timer"] = "", ["startTime"] = DateTime.Now.ToString("yyyy,M,d,H,m", CultureInfo.InvariantCulture),
        ["restartUnblock"] = "true", ["autostart"] = minutes is null ? "window,3,30,0123456" : "none",
        ["randomTextLength"] = "", ["enabled"] = "false",
        ["break"] = minutes is null ? "none" : $"allowance,{minutes.Value.ToString(CultureInfo.InvariantCulture)},m,day,0,3,45",
        ["window"] = ""
    };
    private static JsonArray Entries(IEnumerable<string> entries) => new(entries.Distinct(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static void ValidateEntries(List<string>? entries, string kind)
    {
        if (entries is null || entries.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)))
            throw new ArgumentException($"Invalid {kind}: use nonempty entries without control characters.");
    }
    private static void WriteNew(string path, string contents)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(file);
        writer.Write(contents);
    }
}
