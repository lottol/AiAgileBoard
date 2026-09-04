using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAgileBoard.Data.Migrations;

[DbContext(typeof(AgileBoardDbContext))]
[Migration("20260904023000_InitialTicketSchema")]
public sealed class InitialTicketSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "States",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                HumanNeeded = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_States", item => item.Id));

        migrationBuilder.CreateTable(
            name: "Tickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                StoryPoints = table.Column<int>(type: "INTEGER", nullable: false),
                Assignee = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                StateId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tickets", item => item.Id);
                table.ForeignKey(
                    name: "FK_Tickets_States_StateId",
                    column: item => item.StateId,
                    principalTable: "States",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TicketComments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TicketId = table.Column<Guid>(type: "TEXT", nullable: false),
                Body = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TicketComments", item => item.Id);
                table.ForeignKey(
                    name: "FK_TicketComments_Tickets_TicketId",
                    column: item => item.TicketId,
                    principalTable: "Tickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.InsertData(
            table: "States",
            columns: ["Id", "HumanNeeded", "Name"],
            columnTypes: ["INTEGER", "INTEGER", "TEXT"],
            values: new object[,]
            {
                { 1, true, "Backlog" },
                { 2, true, "Ready for Human" },
                { 3, true, "Human In Progress" },
                { 4, false, "Waiting for Agent" },
                { 5, false, "Agent In Progress" },
                { 6, true, "Human Review" },
                { 7, false, "Changes Requested" },
                { 8, true, "Blocked" },
                { 9, false, "Done" },
                { 10, false, "Canceled" }
            });

        migrationBuilder.CreateIndex(
            name: "IX_States_Name",
            table: "States",
            column: "Name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_TicketComments_TicketId",
            table: "TicketComments",
            column: "TicketId");
        migrationBuilder.CreateIndex(
            name: "IX_Tickets_StateId",
            table: "Tickets",
            column: "StateId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TicketComments");
        migrationBuilder.DropTable(name: "Tickets");
        migrationBuilder.DropTable(name: "States");
    }
}
