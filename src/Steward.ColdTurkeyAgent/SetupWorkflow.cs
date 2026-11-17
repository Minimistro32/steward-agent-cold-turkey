namespace Steward.ColdTurkeyAgent;

public static class SetupWorkflow
{
    public const string RestartNotice = "Restart the agent after changing Steward-managed blocks for Steward to recognize the changes.";
    public static AgentConfiguration EnsureConfiguration(string path, TextReader input, TextWriter output) =>
        File.Exists(path) ? AgentConfiguration.Load(path) : Configure(path, input, output);

    public static AgentConfiguration Configure(string path, TextReader input, TextWriter output)
    {
        var existing = File.Exists(path) ? AgentConfiguration.Load(path) : null;
        using var editLock = existing is null ? null : AgentConfiguration.AcquireLock(path);
        SetupPrompts.Section(output, "Connection settings");
        output.WriteLine("Enter the MQTT host and port separately. Press Enter to keep a displayed value.");
        string host;
        while (true)
        {
            host = SetupPrompts.Read(input, output, existing is null ? "Host / IP (example 192.168.50.60): " : $"Host / IP [{existing.Host}]: ");
            if (host.Length == 0 && existing is not null) host = existing.Host;
            try { AgentConfiguration.ValidateHost(host); break; }
            catch (ArgumentException ex) { output.WriteLine(ex.Message); }
        }
        var tls = existing?.Tls ?? false;
        while (true)
        {
            var answer = SetupPrompts.Read(input, output, tls ? "Use TLS? [Y/n] " : "Use TLS? [y/N] ");
            if (answer.Length == 0) break;
            if (answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase)) { tls = true; break; }
            if (answer.Equals("n", StringComparison.OrdinalIgnoreCase) || answer.Equals("no", StringComparison.OrdinalIgnoreCase)) { tls = false; break; }
            output.WriteLine("Enter y or n.");
        }
        var port = existing?.Port ?? (tls ? 8883 : 1883);
        while (true)
        {
            var answer = SetupPrompts.Read(input, output, $"Port [{port}]: ");
            if (answer.Length == 0) break;
            if (int.TryParse(answer, out var entered) && entered is >= 1 and <= 65535) { port = entered; break; }
            output.WriteLine("Enter a port between 1 and 65535.");
        }
        var username = SetupPrompts.Read(input, output, existing?.Username is null
            ? "Username (Enter for no authentication): " : $"Username [{existing.Username}] (type - to remove authentication): ");
        if (username.Length == 0) username = existing?.Username ?? "";
        if (username == "-") username = "";
        string? password = null;
        if (username.Length > 0)
        {
            if (existing?.Username == username) output.WriteLine("Press Enter at the password prompt to keep the saved password.");
            password = SetupPrompts.Password(input, output);
            if (password.Length == 0 && existing?.Username == username) password = existing.Password;
        }
        var configuration = new AgentConfiguration
        {
            AgentId = existing?.AgentId ?? AgentConfiguration.DefaultAgentId(AgentConfiguration.DeviceName),
            InstanceId = existing?.InstanceId ?? Guid.NewGuid().ToString(),
            Host = host, Port = port, Tls = tls, Username = username.Length == 0 ? null : username, Password = password
        };
        if (editLock is null) configuration.SaveNew(path); else configuration.Save(editLock);
        output.WriteLine();
        output.WriteLine($"Saved: {path}\nAgent: {configuration.AgentId}");
        return configuration;
    }
    public static async Task BeginAsync(string configPath, IColdTurkeyCli cli, TextReader input, TextWriter output, CancellationToken token)
    {
        EnsureConfiguration(configPath, input, output);
        SetupPrompts.Section(output, "Resource setup");
        output.WriteLine("1) Create a resource import\n2) Finish an existing imported resource\n3) Add allowance options\nEnter) Done");
        var choice = SetupPrompts.Read(input, output, "Choice: ");
        if (choice.Length == 0) return;
        await ResourceAsync(choice switch { "1" => "create", "2" => "finish", "3" => "add", _ => throw new ArgumentException("Choose 1, 2, 3, or Enter.") },
            configPath, cli, input, output, token);
    }
    public static async Task ResourceAsync(string action, string configPath, IColdTurkeyCli cli, TextReader input, TextWriter output, CancellationToken token)
    {
        EnsureConfiguration(configPath, input, output);
        using var instanceLock = AgentConfiguration.AcquireLock(configPath);
        var catalog = await cli.DiscoverAsync(token);
        foreach (var warning in catalog.Warnings) output.WriteLine(warning);
        if (action == "create")
        {
            var generated = PresetGenerator.Generate(SetupPrompts.Resource(input, output), ApplicationPaths.ImportsFor(configPath), catalog);
            SetupPrompts.Section(output, "Import into Cold Turkey");
            output.WriteLine(generated.Path); output.WriteLine();
            await FinishAsync(generated.Resource, cli, input, output, token);
            return;
        }
        catalog.RequireResources();
        SetupPrompts.Section(output, "Choose a resource");
        for (var i = 0; i < catalog.Resources.Count; i++)
            output.WriteLine($"{i + 1}) {catalog.Resources[i].Name} — {string.Join(", ", catalog.Resources[i].Presets.Select(p => p.Minutes))} minutes");
        var selected = SetupPrompts.Read(input, output, "Resource number: ");
        if (!int.TryParse(selected, out var number) || number < 1 || number > catalog.Resources.Count)
            throw new ArgumentException("Choose a resource number from the list.");
        var resource = catalog.Resources[number - 1];
        if (action == "finish") { await FinishAsync(resource, cli, input, output, token); return; }
        if (action != "add") throw new ArgumentException("Unknown resource action.");
        SetupPrompts.Section(output, "Add allowance options");
        output.WriteLine("Enter only new minute options; existing allowances stay unchanged.");
        var minutes = SetupPrompts.Minutes(input, output);
        if (minutes.Any(m => resource.Presets.Any(p => p.Minutes == m))) throw new ArgumentException("An entered option already exists. Enter only new minutes.");
        output.WriteLine();
        output.WriteLine($"Export the current {resource.Baseline} baseline from Cold Turkey.\nThe export supplies its websites, apps and exceptions. Select a fresh export if you have edited the resource.");
        var exportPath = SetupPrompts.Read(input, output, "CT export path (.ctbbl, without quotes): ");
        var generatedAdditional = PresetGenerator.GenerateAdditional(resource, minutes, exportPath, ApplicationPaths.ImportsFor(configPath), catalog);
        SetupPrompts.Section(output, "Import additional allowances");
        output.WriteLine(generatedAdditional.Path); output.WriteLine();
        output.WriteLine($"This file contains only new presets: {string.Join(", ", minutes.Order())} minutes.\nImport it in CT and leave the new presets disabled with 03:45 daily refill.");
        if (!SetupPrompts.Confirm(input, output, "Imported with new presets disabled? [y/N] "))
        { output.WriteLine("Import the file when ready, then restart the agent. Existing blocks were not changed."); return; }
        var current = await cli.DiscoverAsync(token);
        var completed = current.Resources.SingleOrDefault(r => r.Baseline == resource.Baseline);
        if (completed is null || !completed.Presets.SequenceEqual(generatedAdditional.Resource.Presets))
            throw new IOException("The imported allowance list does not match. Check the import in CT before restarting the agent.");
        foreach (var preset in generatedAdditional.Added)
            if (await cli.StatusAsync(preset.Block, token)) throw new IOException("A new allowance preset is enabled. Disable it in CT before restarting the agent.");
        output.WriteLine(); output.WriteLine("New allowance options are ready. Existing block states were preserved.");
        output.WriteLine(RestartNotice);
    }
    public static async Task FinishAsync(ResourceDefinition expected, IColdTurkeyCli cli, TextReader input, TextWriter output, CancellationToken token)
    {
        output.WriteLine(PresetGenerator.ImportInstructions(expected));
        if (!SetupPrompts.Confirm(input, output, "Import and settings above confirmed? [y/N] "))
        { output.WriteLine("No blocks changed. Choose 'Finish an imported resource' when ready."); return; }
        var catalog = await cli.DiscoverAsync(token);
        var resource = catalog.Resources.SingleOrDefault(r => r.Baseline == expected.Baseline);
        if (resource is null || !resource.Presets.SequenceEqual(expected.Presets))
            throw new IOException("The imported blocks do not match the selected resource. Check the import in CT. No blocks changed.");
        foreach (var preset in resource.Presets)
            if (await cli.StatusAsync(preset.Block, token)) throw new IOException("An allowance preset is enabled. Disable this resource's presets in CT before finishing. No blocks changed.");
        if (!await cli.StatusAsync(resource.Baseline, token)) await cli.SetAsync(resource.Baseline, true, token);
        SetupPrompts.Section(output, "Ready");
        output.WriteLine($"{resource.Name}: baseline enabled. You may delete the import file.");
        output.WriteLine(RestartNotice);
    }
}
