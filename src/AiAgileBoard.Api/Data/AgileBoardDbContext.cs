using AiAgileBoard.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgileBoard.Data;

public sealed class AgileBoardDbContext(DbContextOptions<AgileBoardDbContext> options)
    : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<State> States => Set<State>();

    public DbSet<TicketComment> Comments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<State>(state =>
        {
            state.ToTable("States");
            state.HasKey(item => item.Id);
            state.Property(item => item.Name).HasMaxLength(100).IsRequired();
            state.HasIndex(item => item.Name).IsUnique();
            state.HasData(
                new { Id = 1, Name = "Backlog", HumanNeeded = true },
                new { Id = 2, Name = "Ready for Human", HumanNeeded = true },
                new { Id = 3, Name = "Human In Progress", HumanNeeded = true },
                new { Id = 4, Name = "Waiting for Agent", HumanNeeded = false },
                new { Id = 5, Name = "Agent In Progress", HumanNeeded = false },
                new { Id = 6, Name = "Human Review", HumanNeeded = true },
                new { Id = 7, Name = "Changes Requested", HumanNeeded = false },
                new { Id = 8, Name = "Blocked", HumanNeeded = true },
                new { Id = 9, Name = "Done", HumanNeeded = false },
                new { Id = 10, Name = "Canceled", HumanNeeded = false });
        });

        modelBuilder.Entity<Ticket>(ticket =>
        {
            ticket.ToTable("Tickets");
            ticket.HasKey(item => item.Id);
            ticket.Property(item => item.Title).HasMaxLength(200).IsRequired();
            ticket.Property(item => item.Description).IsRequired();
            ticket.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
            ticket.Property(item => item.Assignee).HasConversion<string>().HasMaxLength(20);
            ticket.HasOne(item => item.State)
                .WithMany(state => state.Tickets)
                .HasForeignKey(item => item.StateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketComment>(comment =>
        {
            comment.ToTable("TicketComments");
            comment.HasKey(item => item.Id);
            comment.Property(item => item.Body).IsRequired();
            comment.HasOne(item => item.Ticket)
                .WithMany(ticket => ticket.Comments)
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
