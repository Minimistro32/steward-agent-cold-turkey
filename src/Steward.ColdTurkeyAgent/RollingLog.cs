using System.Text;

namespace Steward.ColdTurkeyAgent;

public sealed class RollingLog : TextWriter
{
    private readonly TextWriter original;
    private readonly string path;
    private readonly object gate = new();
    private StreamWriter writer;
    public override Encoding Encoding => Encoding.UTF8;
    public RollingLog(string directory, TextWriter original)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "agent.log");
        this.original = original;
        writer = Open();
    }
    private StreamWriter Open() => new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    public override void WriteLine() => WriteLine("");
    public override void WriteLine(string? value)
    {
        lock (gate)
        {
            if (writer.BaseStream.Length >= 2 * 1024 * 1024)
            {
                writer.Dispose();
                if (File.Exists(path + ".1")) File.Move(path + ".1", path + ".2", true);
                File.Move(path, path + ".1", true);
                writer = Open();
            }
            original.WriteLine(value); writer.WriteLine(value);
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) writer.Dispose(); base.Dispose(disposing); }
}
