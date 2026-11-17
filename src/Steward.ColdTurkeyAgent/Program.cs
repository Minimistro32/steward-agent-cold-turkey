using Steward.ColdTurkeyAgent;

try
{
    if (OperatingSystem.IsMacOS() && ApplicationPaths.BundleDirectory(AppContext.BaseDirectory) is not null
        && (args.Length == 0 || (args.Length == 1 && args[0].StartsWith("-psn_", StringComparison.Ordinal))))
    {
        await DesktopActions.OpenMenuAsync(CancellationToken.None);
        return;
    }
    var command = args.FirstOrDefault() ?? "menu";
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || !args[index].StartsWith("--") || !options.TryAdd(args[index], args[index + 1]))
            throw new ArgumentException("Use command followed by distinct --option value pairs.");
    }
    string[] allowed = command switch
    {
        "run" or "background" => ["--log-directory"],
        "generate-presets" => ["--manifest"],
        "import-config" => ["--file"],
        "menu" or "setup" or "configure" or "create-resource" or "finish-resource" or "add-allowances"
            or "open-data" or "list-resources" or "doctor" or "mqtt-check" or "mqtt-test" or "start" or "stop" or "restart"
            or "login-install" or "login-uninstall" or "help" or "--help" => [],
        "setup-complete" => throw new ArgumentException("Choose Finish an imported resource from the menu. Setup sessions are no longer used."),
        _ => throw new ArgumentException("Unknown command; use help.")
    };
    if (options.Keys.Except(allowed).Any())
        throw new ArgumentException("Unknown option. Use help to find the configuration path. Use help.");
    if (command is "help" or "--help")
    {
        Console.WriteLine("Steward Cold Turkey Agent 0.1.0-alpha.8\n\n  menu (or no arguments)\n  setup\n  configure\n  create-resource\n  add-allowances\n  finish-resource\n  start | stop | restart\n  login-install | login-uninstall\n  run [--log-directory PATH]\n  open-data\n  import-config [--file PATH]\n  list-resources\n  doctor\n  mqtt-check (alias: mqtt-test)\n  generate-presets --manifest PATH\n\nConfiguration: " + AgentConfiguration.DefaultPath);
        return;
    }
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
    using var termination = OperatingSystem.IsWindows() ? null : System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); });
    await AgentCommands.ExecuteAsync(command, options, Console.In, Console.Out, shutdown.Token);
}
catch (OperationCanceledException) { }
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}
