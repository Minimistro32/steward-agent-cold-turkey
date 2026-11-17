namespace Steward.ColdTurkeyAgent;

public static class SetupPrompts
{
    public static GeneratorManifest Resource(TextReader input, TextWriter output)
    {
        Section(output, "Resource");
        var resource = new GeneratorManifest { Name = Read(input, output, "Resource name: ") };
        resource.Minutes = Minutes(input, output);
        Section(output, "Websites");
        resource.Web = Entries(input, output, "Enter websites or patterns, one per line (example.com).\nPasted HTTP(S) prefixes and trailing slashes are removed. Enter a blank line to finish.", true);
        Section(output, "Applications");
        resource.Apps = Applications(input, output);
        Section(output, "Exceptions");
        output.WriteLine($"Included by default: {PresetGenerator.DefaultException}");
        resource.Exceptions = Entries(input, output, "Additional exceptions, one per line. Enter a blank line to finish.", true)
            .Prepend(PresetGenerator.DefaultException).Distinct(StringComparer.Ordinal).ToList();
        return resource;
    }
    public static void Section(TextWriter output, string title)
    {
        output.WriteLine(); output.WriteLine(title); output.WriteLine(new string('-', title.Length));
    }
    public static List<int> Minutes(TextReader input, TextWriter output)
    {
        output.WriteLine("Allowance options in whole minutes (for example: 1,5,10,15,30,60).");
        while (true)
        {
            var tokens = Read(input, output, "Minutes: ").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && tokens.All(t => int.TryParse(t, out var n) && n is >= 1 and <= 1440))
                return tokens.Select(int.Parse).Distinct().Order().ToList();
            output.WriteLine("Enter whole minutes from 1 to 1440, separated by commas.");
        }
    }
    private static List<string> Applications(TextReader input, TextWriter output)
    {
        var values = new List<string>();
        while (true)
        {
            output.WriteLine("1) File / executable\n2) Folder\n3) Application identifier\nEnter) Done");
            var kind = Read(input, output, "Choice: ");
            if (kind.Length == 0) return values.Distinct(StringComparer.Ordinal).ToList();
            var prefix = kind switch { "1" => "file:", "2" => "folder:", "3" => "app:", _ => "" };
            if (prefix.Length == 0) { output.WriteLine("Choose 1, 2, 3 or press Enter."); continue; }
            if (kind == "3")
                output.WriteLine("CT requires the application name AND bundle identifier.\nExample input: Bfxr.app:com.increpare.bfxr\nThe agent adds app: to produce app:Bfxr.app:com.increpare.bfxr.\nEnter without quotes or the app: prefix.");
            var value = Read(input, output, kind switch
            {
                "1" => "Executable path or filename (without quotes): ",
                "2" => "Folder path (without quotes): ",
                _ => "Application name:bundle.identifier: "
            });
            if (value.Length == 0) { output.WriteLine("No target entered."); continue; }
            if (kind == "3" && (value.StartsWith("app:", StringComparison.OrdinalIgnoreCase) || value.StartsWith('"') || value.EndsWith('"')
                || value.IndexOf(':') <= 0 || value.EndsWith(':')))
            { output.WriteLine("Use Bfxr.app:com.increpare.bfxr, without quotes or app:."); continue; }
            values.Add(prefix + value);
            output.WriteLine();
        }
    }
    private static List<string> Entries(TextReader input, TextWriter output, string prompt, bool normalizeWebsite = false)
    {
        output.WriteLine(prompt);
        var values = new List<string>();
        while (true)
        {
            var value = Read(input, output, "> ");
            if (value.Length == 0) return values.Distinct(StringComparer.Ordinal).ToList();
            if (normalizeWebsite)
            {
                try
                {
                    var normalized = PresetGenerator.NormalizeWebsite(value);
                    if (normalized != value) output.WriteLine($"Using: {normalized}");
                    value = normalized;
                }
                catch (ArgumentException ex) { output.WriteLine(ex.Message); continue; }
            }
            values.Add(value);
        }
    }
    public static string Read(TextReader input, TextWriter output, string prompt)
    {
        output.Write(prompt); output.Flush();
        return input.ReadLine()?.Trim() ?? throw new OperationCanceledException("Setup input ended; no confirmation was supplied.");
    }
    public static string Password(TextReader input, TextWriter output)
    {
        output.Write("MQTT password (stored in config.json): "); output.Flush();
        if (input != Console.In || Console.IsInputRedirected)
            return input.ReadLine() ?? throw new OperationCanceledException("Setup input ended.");
        var value = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { output.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
    }
    public static bool Confirm(TextReader input, TextWriter output, string prompt)
    {
        var answer = Read(input, output, prompt);
        return answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
