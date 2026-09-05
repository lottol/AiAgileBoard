using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiAgileBoard.Application;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiAgileBoard.Data.Projects;

/// <summary>Owns one recoverable working copy. Dispose preserves recovery; CompleteAsync removes it.</summary>
public sealed class ProjectSession : IProjectPersistence, IDisposable
{
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;
    private const int MaximumSettingsBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ArchiveEntries = ["manifest.json", "settings.json", "data/aiagileboard.db"];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FileStream _projectLock;
    private bool _closed;
    private string? _archiveHash;
    private string? _pendingArchiveHash;
    private JsonElement _settings;

    public string ArchivePath { get; }
    public string WorkingDirectory { get; }
    public string DatabasePath => Path.Combine(WorkingDirectory, "data", "aiagileboard.db");
    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath, Pooling = false, ForeignKeys = true
    }.ToString();
    public string SaveStatus { get; private set; } = "saving";
    public string? SaveError { get; private set; }
    public JsonElement Settings => _settings.Clone();
    public event EventHandler? Changed;

    private ProjectSession(string archivePath, string workingDirectory, string? archiveHash)
    {
        ArchivePath = Path.GetFullPath(archivePath);
        WorkingDirectory = workingDirectory;
        _archiveHash = archiveHash;
        _projectLock = new FileStream(ArchivePath + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    public static async Task<ProjectSession> CreateAsync(string archivePath, string recoveryRoot)
    {
        ValidateExtension(archivePath);
        return await PrepareAsync(archivePath, recoveryRoot, create: true);
    }

    public static async Task<ProjectSession> OpenAsync(string archivePath, string recoveryRoot)
    {
        ValidateExtension(archivePath);
        return await PrepareAsync(archivePath, recoveryRoot, create: false);
    }

    private static async Task<ProjectSession> PrepareAsync(string archivePath, string recoveryRoot, bool create)
    {
        var directory = Path.Combine(Path.GetFullPath(recoveryRoot), Guid.NewGuid().ToString("N"));
        var session = new ProjectSession(archivePath, directory, HashFile(archivePath));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            if (create)
            {
                File.WriteAllText(Path.Combine(directory, "manifest.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(directory, "settings.json"), "{}");
            }
            else session.Extract();
            session.ReadMetadata();
            await session.InitializeDatabaseAsync(existing: !create);
            session.WriteRecoveryRecord();
            // Failed first save is still a usable, recoverable session.
            session.TrySave();
            return session;
        }
        catch
        {
            session.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            throw;
        }
    }

    public static IReadOnlyList<string> FindRecoveryDirectories(string recoveryRoot) =>
        Directory.Exists(recoveryRoot)
            ? Directory.GetDirectories(recoveryRoot)
                .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _) &&
                    File.Exists(Path.Combine(path, "session.json")))
                .Order(StringComparer.Ordinal).ToArray()
            : [];

    public static async Task<ProjectSession> RecoverAsync(string recoveryDirectory)
    {
        var record = JsonSerializer.Deserialize<RecoveryRecord>(
            File.ReadAllText(Path.Combine(recoveryDirectory, "session.json")), JsonOptions)
            ?? throw new InvalidDataException("Recovery information is missing.");
        var session = new ProjectSession(record.ArchivePath, Path.GetFullPath(recoveryDirectory), record.ArchiveHash);
        try
        {
            if (record.PendingArchiveHash is not null && HashFile(record.ArchivePath) == record.PendingArchiveHash)
                session._archiveHash = record.PendingArchiveHash;
            session.ReadMetadata();
            await session.InitializeDatabaseAsync(existing: true);
            // Do not overwrite a project changed outside this session.
            session.TrySave();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    private void Extract()
    {
        using var archive = ZipFile.OpenRead(ArchivePath);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            // V1 has a deliberately closed layout: no traversal, aliases, links, or unknown payloads.
            if (!ArchiveEntries.Contains(entry.FullName, StringComparer.Ordinal) || !seen.Add(entry.FullName))
                throw new InvalidDataException("The project contains an unsupported or duplicate archive entry.");
            if (entry.Length > MaximumExpandedBytes - expanded ||
                (entry.FullName != "data/aiagileboard.db" && entry.Length > MaximumSettingsBytes))
                throw new InvalidDataException("The project exceeds the supported expanded size.");
            expanded += entry.Length;
            using var source = entry.Open();
            using var destination = File.Create(Path.Combine(WorkingDirectory, entry.FullName));
            var buffer = new byte[81920];
            long copied = 0;
            int count;
            while ((count = source.Read(buffer)) != 0)
            {
                copied += count;
                if (copied > entry.Length) throw new InvalidDataException("Invalid archive entry length.");
                destination.Write(buffer, 0, count);
            }
            if (copied != entry.Length) throw new InvalidDataException("The project archive is incomplete.");
        }
        if (seen.Count != ArchiveEntries.Length) throw new InvalidDataException("Required project files are missing.");
    }

    private void ReadMetadata()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkingDirectory, "manifest.json")));
        if (manifest.RootElement.ValueKind != JsonValueKind.Object ||
            !manifest.RootElement.TryGetProperty("formatVersion", out var version) ||
            !version.TryGetInt32(out var number) || number != 1)
            throw new InvalidDataException("This project format is not supported by this version of AI Agile Board.");
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkingDirectory, "settings.json")));
        ValidateSettings(settings.RootElement);
        _settings = settings.RootElement.Clone();
    }

    private async Task InitializeDatabaseAsync(bool existing)
    {
        if (existing)
        {
            if (!File.Exists(DatabasePath)) throw new InvalidDataException("Project database is missing.");
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            if (!Equals(await check.ExecuteScalarAsync(), "ok")) throw new InvalidDataException("Project database is damaged.");
            check.CommandText = "PRAGMA foreign_key_check;";
            using var reader = await check.ExecuteReaderAsync();
            if (await reader.ReadAsync()) throw new InvalidDataException("Project database has invalid references.");
        }
        var options = new DbContextOptionsBuilder<AgileBoardDbContext>().UseSqlite(ConnectionString).Options;
        await using var db = new AgileBoardDbContext(options);
        if (existing)
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            if (applied.Length == 0 || applied.Except(db.Database.GetMigrations(), StringComparer.Ordinal).Any())
                throw new InvalidDataException("The project database schema is unknown or newer than this application.");
        }
        await db.Database.MigrateAsync();
        // Verify the complete current model can be read before activation.
        _ = await db.Tickets.Include(ticket => ticket.State).Include(ticket => ticket.Comments).Take(1).ToListAsync();
    }

    public async Task<T> MutateAsync<T>(Func<Task<T>> mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_closed || SaveStatus == "failed") throw new ProjectSavePendingException();
            var result = await mutation();
            // A committed mutation must be snapshotted even if the HTTP client disconnects.
            TrySave();
            return result;
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateSettingsAsync(JsonElement settings)
    {
        ValidateSettings(settings);
        await MutateAsync(() =>
        {
            var temporary = Path.Combine(WorkingDirectory, "settings.tmp");
            WriteDurable(temporary, settings.GetRawText());
            File.Move(temporary, Path.Combine(WorkingDirectory, "settings.json"), overwrite: true);
            _settings = settings.Clone();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    public async Task<bool> RetrySaveAsync()
    {
        await _gate.WaitAsync();
        try { return !_closed && TrySave(); }
        finally { _gate.Release(); }
    }

    public async Task<bool> CompleteAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!TrySave()) return false;
            File.Move(Path.Combine(WorkingDirectory, "session.json"), Path.Combine(WorkingDirectory, "completed.json"), overwrite: true);
            _closed = true;
            _projectLock.Dispose();
            try { Directory.Delete(WorkingDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return true;
        }
        finally { _gate.Release(); }
    }

    private bool TrySave()
    {
        SaveStatus = "saving";
        Changed?.Invoke(this, EventArgs.Empty);
        var temporary = ArchivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var snapshot = Path.Combine(WorkingDirectory, "snapshot.db");
        try
        {
            if (!string.Equals(HashFile(ArchivePath), _archiveHash, StringComparison.Ordinal))
                throw new IOException("The project file changed outside this session. Your working copy is retained for recovery.");
            using (var source = new SqliteConnection(ConnectionString))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = snapshot, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
                using var checkpoint = destination.CreateCommand();
                checkpoint.CommandText = "PRAGMA journal_mode=DELETE;";
                checkpoint.ExecuteNonQuery();
            }
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (new FileInfo(snapshot).Length + new FileInfo(Path.Combine(WorkingDirectory, "settings.json")).Length +
                    new FileInfo(Path.Combine(WorkingDirectory, "manifest.json")).Length > MaximumExpandedBytes)
                    throw new IOException("The project exceeds the supported 512 MiB expanded size.");
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    archive.CreateEntryFromFile(Path.Combine(WorkingDirectory, "manifest.json"), "manifest.json");
                    archive.CreateEntryFromFile(Path.Combine(WorkingDirectory, "settings.json"), "settings.json");
                    archive.CreateEntryFromFile(snapshot, "data/aiagileboard.db");
                }
                file.Flush(flushToDisk: true);
            }
            _pendingArchiveHash = HashFile(temporary);
            // Record both generations before replacement so a crash on either side remains recoverable.
            WriteRecoveryRecord();
            if (File.Exists(ArchivePath)) File.Replace(temporary, ArchivePath, ArchivePath + ".bak");
            else File.Move(temporary, ArchivePath);
            _archiveHash = HashFile(ArchivePath);
            _pendingArchiveHash = null;
            WriteRecoveryRecord();
            SaveError = null;
            SaveStatus = "saved";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            SaveError = exception.Message;
            SaveStatus = "failed";
            return false;
        }
        finally
        {
            DeleteTemporary(temporary);
            DeleteTemporary(snapshot);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WriteRecoveryRecord()
    {
        var temporary = Path.Combine(WorkingDirectory, "session.tmp");
        WriteDurable(temporary, JsonSerializer.Serialize(new RecoveryRecord(ArchivePath, _archiveHash, _pendingArchiveHash), JsonOptions));
        File.Move(temporary, Path.Combine(WorkingDirectory, "session.json"), overwrite: true);
    }

    private static void WriteDurable(string path, string text)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        file.Write(Encoding.UTF8.GetBytes(text));
        file.Flush(flushToDisk: true);
    }

    private static void ValidateSettings(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(settings.GetRawText()) > MaximumSettingsBytes)
            throw new InvalidDataException("Project settings must be a JSON object no larger than 1 MiB.");
    }

    private static void ValidateExtension(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".aiab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select an .aiab project file.");
    }

    private static string? HashFile(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void DeleteTemporary(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _projectLock.Dispose();
        _gate.Dispose();
    }

    private sealed record RecoveryRecord(string ArchivePath, string? ArchiveHash, string? PendingArchiveHash);
}
