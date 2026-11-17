namespace Steward.ColdTurkeyAgent;

public static class MainMenu
{
    public static async Task RunAsync(TextReader input, TextWriter output, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SetupPrompts.Section(output, "Steward Cold Turkey");
            output.WriteLine($"Data: {ApplicationPaths.DataDirectory}");
            output.WriteLine();
            output.WriteLine("Settings and resources\n  1) Connection settings\n  2) Create a managed resource\n  3) Add allowance options\n  4) Finish an imported resource");
            output.WriteLine();
            output.WriteLine("Background agent\n  5) Start\n  6) Stop\n  7) Restart\n  8) Install login startup\n  9) Uninstall login startup");
            output.WriteLine();
            output.WriteLine("Diagnostics\n  10) List resources\n  11) Test MQTT connection (mqtt-test)\n  12) Cold Turkey checks (doctor)\n  13) Open data folder\n  14) Import existing config");
            output.WriteLine(); output.WriteLine("  0) Exit menu (background agent keeps running)");
            output.Write("Choice: "); output.Flush();
            var answer = input.ReadLine()?.Trim();
            if (answer is null or "0" or "") return;
            var command = answer switch
            {
                "1" => "configure", "2" => "create-resource", "3" => "add-allowances", "4" => "finish-resource",
                "5" => "start", "6" => "stop", "7" => "restart", "8" => "login-install", "9" => "login-uninstall", "10" => "list-resources", "11" => "mqtt-test", "12" => "doctor", "13" => "open-data", "14" => "import-config", _ => ""
            };
            if (command.Length == 0) { output.WriteLine("Choose a number from the menu."); continue; }
            try { await AgentCommands.ExecuteAsync(command, new Dictionary<string, string>(), input, output, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (OperationCanceledException) { output.WriteLine("Input ended; returning to menu."); }
            catch (Exception ex) { output.WriteLine(); output.WriteLine($"Could not finish: {ex.Message}"); }
            output.WriteLine(); output.Write("Press Enter to return to the menu: "); output.Flush();
            if (input.ReadLine() is null) return;
        }
    }
}
