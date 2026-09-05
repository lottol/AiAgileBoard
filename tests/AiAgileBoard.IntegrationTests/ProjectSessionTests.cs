using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiAgileBoard.Application;
using AiAgileBoard.Data;
using AiAgileBoard.Data.Projects;
using AiAgileBoard.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace AiAgileBoard.IntegrationTests;

public sealed class ProjectSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aiab-project-tests-" + Guid.NewGuid().ToString("N"));
    private string ArchivePath => Path.Combine(_root, "Board with spaces.aiab");
    private string RecoveryRoot => Path.Combine(_root, "recovery");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ProjectSessionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task RoundTripPreservesTicketsCommentsSettingsAndWalChanges()
    {
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot))
        {
            await using var db = Database(session);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", Token);
            var service = new TicketService(db, session);
            await service.SubmitTicketAsync(NewTicket(), Token);
            using var settings = JsonDocument.Parse("{\"theme\":\"dark\",\"board\":{\"zoom\":125}}");
            await session.UpdateSettingsAsync(settings.RootElement);
            Assert.Equal("saved", session.SaveStatus);
            using var zip = ZipFile.OpenRead(ArchivePath);
            Assert.Equal(["data/aiagileboard.db", "manifest.json", "settings.json"],
                zip.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray());
        }
        using var reopened = await ProjectSession.OpenAsync(ArchivePath, Path.Combine(_root, "another-recovery"));
        await using var restored = Database(reopened);
        var ticket = await restored.Tickets.Include(item => item.Comments).SingleAsync(Token);
        Assert.Equal("Portable ticket", ticket.Title);
        Assert.Equal("Initial note", Assert.Single(ticket.Comments).Body);
        Assert.Equal("dark", reopened.Settings.GetProperty("theme").GetString());
        Assert.Equal(125, reopened.Settings.GetProperty("board").GetProperty("zoom").GetInt32());
        Assert.True(File.Exists(ArchivePath + ".bak"));
    }

    [Fact]
    public async Task FailedAutosaveKeepsCommitBlocksWritesAndRetriesWithoutDuplicates()
    {
        using var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot);
        var original = await File.ReadAllBytesAsync(ArchivePath, Token);
        Directory.CreateDirectory(ArchivePath + ".bak");
        await using var db = Database(session);
        var service = new TicketService(db, session);
        var committed = await service.SubmitTicketAsync(NewTicket(), Token);
        Assert.NotEqual(Guid.Empty, committed.Id);
        Assert.Equal("failed", session.SaveStatus);
        Assert.Equal(original, await File.ReadAllBytesAsync(ArchivePath, Token));
        await Assert.ThrowsAsync<ProjectSavePendingException>(() => service.SubmitTicketAsync(NewTicket(), Token));
        Assert.False(await session.CompleteAsync());
        Assert.True(Directory.Exists(session.WorkingDirectory));
        Directory.Delete(ArchivePath + ".bak");
        Assert.True(await session.RetrySaveAsync());
        Assert.Equal(1, await db.Tickets.CountAsync(Token));
        Assert.True(await session.CompleteAsync());
        Assert.Empty(ProjectSession.FindRecoveryDirectories(RecoveryRoot));
        using var reopened = await ProjectSession.OpenAsync(ArchivePath, RecoveryRoot);
        await using var restored = Database(reopened);
        Assert.Equal(committed.Id, (await restored.Tickets.SingleAsync(Token)).Id);
    }

    [Fact]
    public async Task EndpointReturnsCommittedTicketOnSaveFailureAndRejectsFurtherWrites()
    {
        using var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", session.ConnectionString);
            builder.ConfigureServices(services => services.AddSingleton<IProjectPersistence>(session));
        });
        using var client = factory.CreateClient();
        Directory.CreateDirectory(ArchivePath + ".bak");
        using var first = await client.PostAsJsonAsync("/api/v1/tickets", new { title = "One commit", description = "Retain me" }, Token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var second = await client.PostAsJsonAsync("/api/v1/tickets", new { title = "Blocked", description = "Not committed" }, Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        var tickets = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/tickets", Token);
        Assert.Single(tickets!);
    }

    [Fact]
    public async Task RecoveryRestoresChangesThatNeverReachedTheArchive()
    {
        string workingDirectory;
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot))
        {
            workingDirectory = session.WorkingDirectory;
            Directory.CreateDirectory(ArchivePath + ".bak");
            await using var db = Database(session);
            await new TicketService(db, session).SubmitTicketAsync(NewTicket(), Token);
            Assert.Equal("failed", session.SaveStatus);
        }
        Directory.Delete(ArchivePath + ".bak");
        Assert.Equal(workingDirectory, Assert.Single(ProjectSession.FindRecoveryDirectories(RecoveryRoot)));
        using var recovered = await ProjectSession.RecoverAsync(workingDirectory);
        Assert.Equal("saved", recovered.SaveStatus);
        await using var restored = Database(recovered);
        Assert.Single(await restored.Tickets.ToListAsync(Token));
    }

    [Fact]
    public async Task RecoveryRecognizesArchiveReplacementBeforeRecoveryRecordFinalization()
    {
        string directory;
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot))
        {
            directory = session.WorkingDirectory;
        }
        var recordPath = Path.Combine(directory, "session.json");
        var record = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await File.ReadAllTextAsync(recordPath, Token))!;
        var hash = record["archiveHash"].GetString();
        await File.WriteAllTextAsync(recordPath, JsonSerializer.Serialize(new
        {
            archivePath = ArchivePath, archiveHash = "previous-generation", pendingArchiveHash = hash
        }), Token);
        using var recovered = await ProjectSession.RecoverAsync(directory);
        Assert.Equal("saved", recovered.SaveStatus);
    }

    [Fact]
    public async Task ExternalReplacementIsNeverOverwrittenByRecovery()
    {
        string directory;
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot)) directory = session.WorkingDirectory;
        await File.WriteAllTextAsync(ArchivePath, "external replacement", Token);
        using var recovered = await ProjectSession.RecoverAsync(directory);
        Assert.Equal("failed", recovered.SaveStatus);
        Assert.Equal("external replacement", await File.ReadAllTextAsync(ArchivePath, Token));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("data/../escape.txt")]
    [InlineData("settings.json")]
    [InlineData("Settings.json")]
    public async Task InvalidOrDuplicateEntriesAreRejectedWithoutChangingArchive(string name)
    {
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot)) { }
        using (var zip = ZipFile.Open(ArchivePath, ZipArchiveMode.Update)) zip.CreateEntry(name);
        var original = await File.ReadAllBytesAsync(ArchivePath, Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProjectSession.OpenAsync(ArchivePath, RecoveryRoot));
        Assert.Equal(original, await File.ReadAllBytesAsync(ArchivePath, Token));
        Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
    }

    [Theory]
    [InlineData("manifest.json", "{\"formatVersion\":999}")]
    [InlineData("settings.json", "[]")]
    [InlineData("data/aiagileboard.db", "not sqlite")]
    public async Task InvalidMetadataAndDatabaseAreRejected(string entry, string contents)
    {
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot)) { }
        ReplaceEntry(entry, contents);
        await Assert.ThrowsAnyAsync<Exception>(() => ProjectSession.OpenAsync(ArchivePath, RecoveryRoot));
    }

    [Fact]
    public async Task MissingAndOversizedPayloadsAreRejected()
    {
        using (var session = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot)) { }
        ReplaceEntry("settings.json", new string(' ', 1024 * 1024 + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => ProjectSession.OpenAsync(ArchivePath, RecoveryRoot));
        using (var zip = ZipFile.Open(ArchivePath, ZipArchiveMode.Update)) zip.GetEntry("settings.json")!.Delete();
        await Assert.ThrowsAsync<InvalidDataException>(() => ProjectSession.OpenAsync(ArchivePath, RecoveryRoot));
    }

    [Fact]
    public async Task ExistingSchemaIsMigratedAndOriginalArchiveBackedUp()
    {
        var dbPath = Path.Combine(_root, "old.db");
        await using (var db = new AgileBoardDbContext(new DbContextOptionsBuilder<AgileBoardDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False").Options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20260904023000_InitialTicketSchema", Token);
        }
        using (var zip = ZipFile.Open(ArchivePath, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open())) writer.Write("{\"formatVersion\":1}");
            using (var writer = new StreamWriter(zip.CreateEntry("settings.json").Open())) writer.Write("{}");
            zip.CreateEntryFromFile(dbPath, "data/aiagileboard.db");
        }
        var original = await File.ReadAllBytesAsync(ArchivePath, Token);
        using var opened = await ProjectSession.OpenAsync(ArchivePath, RecoveryRoot);
        await using var upgraded = Database(opened);
        Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync(Token));
        Assert.Equal(original, await File.ReadAllBytesAsync(ArchivePath + ".bak", Token));
    }

    [Fact]
    public async Task CloseWaitsForMutationAndProjectsStayIsolated()
    {
        using var first = await ProjectSession.CreateAsync(ArchivePath, RecoveryRoot);
        using var second = await ProjectSession.CreateAsync(Path.Combine(_root, "second.aiab"), RecoveryRoot);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mutation = first.MutateAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            await using var db = Database(first);
            db.Tickets.Add(NewTicket());
            await db.SaveChangesAsync(Token);
            return true;
        }, Token);
        await entered.Task;
        var closing = first.CompleteAsync();
        Assert.False(closing.IsCompleted);
        release.SetResult();
        await mutation;
        Assert.True(await closing);
        await using var otherDb = Database(second);
        Assert.Empty(await otherDb.Tickets.ToListAsync(Token));
    }

    private void ReplaceEntry(string name, string contents)
    {
        using var zip = ZipFile.Open(ArchivePath, ZipArchiveMode.Update);
        zip.GetEntry(name)!.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(contents);
    }

    private static AgileBoardDbContext Database(ProjectSession session) => new(
        new DbContextOptionsBuilder<AgileBoardDbContext>().UseSqlite(session.ConnectionString).Options);

    private static Ticket NewTicket() => new()
    {
        Title = "Portable ticket", Description = "Travels with the board", StateId = 1,
        Comments = { new TicketComment { Body = "Initial note" } }
    };

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
