namespace Steward.ColdTurkeyAgent;

public static class AgentCommands
{
    public static async Task ExecuteAsync(string command, Dictionary<string, string> options, TextReader input, TextWriter output, CancellationToken token)
    {
        var configPath = AgentConfiguration.DefaultPath;
        var cli = new ColdTurkeyCli();
        if (command == "open-data") { await DesktopActions.OpenDataAsync(token); return; }
        if (command == "import-config")
        {
            var source = options.GetValueOrDefault("--file") ?? SetupPrompts.Read(input, output, "Existing config.json path (without surrounding quotes): ");
            DesktopActions.ImportConfiguration(source, configPath, output); return;
        }
        if (command == "menu") { await MainMenu.RunAsync(input, output, token); return; }
        if (command == "list-resources")
        {
            (await cli.DiscoverAsync(token)).Display(output);
            return;
        }
        if (command == "doctor") { await cli.DoctorAsync(token); return; }
        if (command == "configure") { SetupWorkflow.Configure(configPath, input, output); return; }
        if (command == "setup") { await SetupWorkflow.BeginAsync(configPath, cli, input, output, token); return; }
        if (command is "create-resource" or "finish-resource" or "add-allowances")
        {
            await SetupWorkflow.ResourceAsync(command switch { "create-resource" => "create", "finish-resource" => "finish", _ => "add" }, configPath, cli, input, output, token);
            return;
        }
        // Controls address the process by config path; they do not read its exclusively locked configuration.
        if (command == "stop") { await LoginStartup.StopAsync(configPath, token); return; }
        if (command == "start") { await LoginStartup.StartAsync(configPath, token); return; }
        if (command == "restart") { await LoginStartup.RestartAsync(configPath, token); return; }
        if (command == "login-uninstall") { await LoginStartup.UninstallAsync(configPath, token); return; }
        if (command is "run" or "background") { await RunAgentAsync(command, options, configPath, cli, token); return; }
        var configuration = AgentConfiguration.Load(configPath);
        if (command == "login-install")
        {
            using (AgentConfiguration.AcquireLock(configPath))
            {
                var discovered = await cli.DiscoverAsync(token);
                discovered.Report(); discovered.RequireResources();
            }
            await LoginStartup.InstallAsync(configuration, configPath, token); return;
        }
        using var instanceLock = AgentConfiguration.AcquireLock(configPath);
        if (command is "mqtt-check" or "mqtt-test") { await MqttConnection.CheckAsync(configuration, token); return; }
        if (command == "generate-presets")
        {
            if (!options.TryGetValue("--manifest", out var manifest)) throw new ArgumentException("Missing --manifest PATH.");
            var generated = PresetGenerator.Generate(PresetGenerator.ReadManifest(manifest), ApplicationPaths.ImportsFor(configPath), await cli.DiscoverAsync(token));
            SetupPrompts.Section(output, "Import into Cold Turkey");
            output.WriteLine(generated.Path); output.WriteLine(); output.WriteLine(PresetGenerator.ImportInstructions(generated.Resource));
            output.WriteLine("Choose Finish an imported resource from the menu after import."); return;
        }
        throw new ArgumentException("Unknown command.");
    }
    private static async Task RunAgentAsync(string command, Dictionary<string, string> options, string configPath, IColdTurkeyCli cli, CancellationToken token)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var console = Console.Out; var errorConsole = Console.Error;
        var logDirectory = options.GetValueOrDefault("--log-directory") ?? (command == "background" ? Path.Combine(ApplicationPaths.DataDirectory, "logs") : null);
        using var log = logDirectory is null ? null : new RollingLog(logDirectory, console);
        if (log is not null) { Console.SetOut(log); Console.SetError(log); }
        try
        {
            var configuration = AgentConfiguration.Load(configPath);
            using var instanceLock = AgentConfiguration.AcquireLock(configPath);
            await using var control = command == "background" ? new BackgroundControl(configPath, stop.Cancel) : null;
            using var stopping = stop.Token.Register(() => control?.Stopping());
            Log.Break(); Log.Write($"Steward Cold Turkey — {configuration.AgentId}");
            Log.Detail($"Version 0.1.0-alpha.8 · mode: {command}");
            Log.Detail($"Executable: {Log.Value(Environment.ProcessPath)}");
            Log.Detail($"Configuration: {Log.Value(configPath)}");
            Log.Detail($"Started {DateTimeOffset.Now:yyyy-MM-dd zzz}. CT state is preserved on ordinary startup/shutdown.");
            if (OperatingSystem.IsWindows()) Log.Detail("Windows runtime is still awaiting validation; close the CT UI/tray app before CLI use.");
            var catalog = await cli.DiscoverAsync(stop.Token);
            catalog.Report(); catalog.RequireResources();
            foreach (var block in catalog.Resources.SelectMany(r => r.Presets.Select(p => p.Block).Prepend(r.Baseline)))
                await cli.StatusAsync(block, stop.Token);
            Log.Detail(SetupWorkflow.RestartNotice); Log.Break();
            control?.Ready();
            await using var agent = new MqttAgent(configuration, catalog.Resources, new GrantProcessor(catalog.Resources, cli));
            await agent.RunAsync(stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Log.Write($"Agent error: {ex.Message}"); throw; }
        finally
        {
            Log.Write("Agent stopped.");
            Console.SetOut(console); Console.SetError(errorConsole);
        }
    }
}
