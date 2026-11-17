using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Steward.ColdTurkeyAgent;

public sealed record BlockCatalog(IReadOnlyList<string> Names, IReadOnlyList<ResourceDefinition> Resources, IReadOnlyList<string> Warnings)
{
    public void Report()
    {
        foreach (var warning in Warnings) Log.Write(warning);
        foreach (var resource in Resources)
            Log.Write($"Discovered {Log.Value(resource.Name)}: {string.Join(", ", resource.Presets.Select(p => p.Minutes))} minute allowances.");
    }
    public void Display(TextWriter output)
    {
        SetupPrompts.Section(output, "Recognized Cold Turkey resources");
        output.WriteLine("Fresh scan of Cold Turkey; no blocks are changed.");
        output.WriteLine();
        if (Resources.Count == 0) output.WriteLine("No complete, unambiguous Steward resources found.");
        foreach (var resource in Resources)
        {
            output.WriteLine($"  {resource.Name}");
            output.WriteLine($"    Baseline: {resource.Baseline}");
            output.WriteLine($"    Allowances: {string.Join(", ", resource.Presets.Select(p => p.Minutes))} minutes");
            foreach (var preset in resource.Presets)
                output.WriteLine($"      {preset.Minutes,4} min  {preset.Block}");
            output.WriteLine();
        }
        if (Warnings.Count > 0)
        {
            output.WriteLine("Groups not recognized");
            foreach (var warning in Warnings) output.WriteLine($"  {warning}");
            output.WriteLine();
        }
        output.WriteLine("Restart the agent after CT changes to update its running inventory.");
    }
    public void RequireResources()
    {
        if (Resources.Count == 0) throw new IOException("No complete, unambiguous Steward resources found in Cold Turkey. Run setup. No registration was published and no blocks were changed.");
    }
}

public static class BlockDiscovery
{
    public const string Prefix = "§teward-";
    public static string ResourceId(string name)
    {
        var id = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9_-]+", "-").Trim('-');
        return id.Length > 0 ? id : "resource-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..12].ToLowerInvariant();
    }
    public static BlockCatalog Parse(string output)
    {
        var names = new List<string>();
        var section = 0;
        var sawWeb = false;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // CT emits ANSI SGR color/bold sequences even when stdout is redirected.
            // Normalize only headings; preserve exact block names for later CLI commands.
            var heading = Regex.Replace(line, @"\x1B\[[0-9;]*m", "", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)).Trim('*');
            if (heading == "Website & App Blocks")
            {
                if (section != 0) throw new IOException("Unexpected duplicate/out-of-order CT block-list heading.");
                section = 1; sawWeb = true; continue;
            }
            if (heading == "Device Blocks")
            {
                if (section != 1) throw new IOException("Unexpected CT device-block heading.");
                section = 2; continue;
            }
            if (section == 0) throw new IOException("Unknown CT block-list format; expected Website & App Blocks heading. Run doctor to inspect the list.");
            if (section == 1 && line.StartsWith(Prefix, StringComparison.Ordinal)) names.Add(line);
        }
        if (!sawWeb) throw new IOException("CT did not return a recognized block list.");
        return FromNames(names);
    }
    public static BlockCatalog FromNames(IEnumerable<string> blockNames)
    {
        var names = blockNames.ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(n => n.Any(char.IsControl)))
            throw new IOException("Duplicate or invalid Steward block names; discovery cannot safely identify blocks.");
        var all = names.ToHashSet(StringComparer.Ordinal);
        var children = names.ToDictionary(n => n, _ => new List<(string Name, int Minutes, bool Valid)>(), StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var dash = name.LastIndexOf('-');
            if (dash < Prefix.Length) continue;
            var suffix = name[(dash + 1)..];
            if (suffix.Length == 0 || !suffix.All(char.IsAsciiDigit) || !all.Contains(name[..dash])) continue;
            var valid = int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
                && minutes is >= 1 and <= 1440 && (suffix == minutes.ToString(CultureInfo.InvariantCulture) || suffix == minutes.ToString("D4", CultureInfo.InvariantCulture));
            parents.Add(name, name[..dash]);
            children[name[..dash]].Add((name, minutes, valid));
        }
        var resources = new List<ResourceDefinition>();
        var warnings = new List<string>();
        foreach (var baseline in names.Where(n => !parents.ContainsKey(n)).Order(StringComparer.Ordinal))
        {
            var presets = children[baseline];
            if (presets.Count == 0)
            { warnings.Add($"Skipped incomplete Steward group {Log.Value(baseline)}: no matching allowance presets (or missing baseline)."); continue; }
            if (baseline.Length == Prefix.Length || presets.Any(p => !p.Valid || children[p.Name].Count > 0) || presets.Select(p => p.Minutes).Distinct().Count() != presets.Count)
            { warnings.Add($"Skipped ambiguous/invalid Steward group {Log.Value(baseline)}: overlapping baseline/preset names, duplicate minutes, or invalid minutes (use 1–1440, unpadded or four digits)."); continue; }
            var name = baseline[Prefix.Length..];
            resources.Add(new ResourceDefinition { Id = ResourceId(name), Name = name, Baseline = baseline,
                Presets = presets.OrderBy(p => p.Minutes).Select(p => new PresetDefinition(p.Minutes, p.Name)).ToList() });
        }
        var collisions = resources.GroupBy(r => r.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).ToArray();
        foreach (var group in collisions)
        {
            warnings.Add($"Skipped resource-ID collision: {string.Join(", ", group.Select(r => Log.Value(r.Baseline)))}. Rename the conflicting groups in CT.");
            resources.RemoveAll(r => r.Id == group.Key);
        }
        return new BlockCatalog(names, resources, warnings);
    }
}
