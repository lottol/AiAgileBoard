using System.IO;
using Microsoft.Data.Sqlite;

namespace AiAgileBoard.Desktop;

internal static class DesktopStorage
{
    public static string ResolveConnectionString(string? configured, string applicationDirectory)
    {
        var connection = new SqliteConnectionStringBuilder(
            configured ?? "Data Source=data/aiagileboard.db");
        if (!string.IsNullOrEmpty(connection.DataSource) && connection.DataSource != ":memory:")
        {
            connection.DataSource = Path.GetFullPath(connection.DataSource, applicationDirectory);
        }
        return connection.ToString();
    }

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
