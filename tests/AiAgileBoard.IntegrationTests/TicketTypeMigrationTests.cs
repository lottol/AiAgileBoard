using AiAgileBoard.Data;
using AiAgileBoard.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AiAgileBoard.IntegrationTests;

public sealed class TicketTypeMigrationTests
{
    [Fact]
    public async Task ExistingTicketsBecomeStoriesWhenDatabaseIsUpgraded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var context = new AgileBoardDbContext(
            new DbContextOptionsBuilder<AgileBoardDbContext>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20260904023000_InitialTicketSchema", cancellationToken);
        var id = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Tickets (Id, Title, Description, StoryPoints, Assignee, StateId)
            VALUES ({id}, 'Existing ticket', 'Preserve this ticket', 3, 'Human', 1)
            """, cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);

        var ticket = await context.Tickets.SingleAsync(cancellationToken);
        Assert.Equal(id, ticket.Id);
        Assert.Equal("Existing ticket", ticket.Title);
        Assert.Equal(TicketType.Story, ticket.Type);
    }
}
