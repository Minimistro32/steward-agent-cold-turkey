namespace Steward.ColdTurkeyAgent;

public static class ApplicationPaths
{
    public const string BundleId = "org.steward.coldturkey.agent";
    public const string BundleName = "Steward Cold Turkey.app";
    public static string DataDirectory => Resolve(AppContext.BaseDirectory, OperatingSystem.IsMacOS(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    public static string Resolve(string baseDirectory, bool macOS, string? userHome = null) => macOS
        ? Path.Combine(userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steward Cold Turkey")
        : Path.GetFullPath(baseDirectory);
    public static string ImportsFor(string configPath)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var directory = OperatingSystem.IsMacOS() ? Path.Combine(parent, "imports") : parent;
        Directory.CreateDirectory(directory);
        return directory;
    }
    public static string? BundleDirectory(string baseDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        return directory.Name == "MacOS" && directory.Parent?.Name == "Contents"
            && directory.Parent.Parent?.Name == BundleName ? directory.Parent.Parent.FullName : null;
    }
}
