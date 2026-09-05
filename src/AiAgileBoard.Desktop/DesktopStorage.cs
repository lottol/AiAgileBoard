using System.IO;

namespace AiAgileBoard.Desktop;

internal static class DesktopStorage
{
    public static string PrepareBrowserProfile(string applicationDirectory)
    {
        var path = Path.Combine(applicationDirectory, "browser-profile");
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, Guid.NewGuid().ToString("N") + ".tmp");
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1, FileOptions.DeleteOnClose)) { }
        return path;
    }
}
