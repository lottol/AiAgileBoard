using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AiAgileBoard.Data.Migrations;

[DbContext(typeof(AgileBoardDbContext))]
[Migration("20260904120000_AddTicketType")]
public sealed class AddTicketType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Type",
            table: "Tickets",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "Story");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Type", table: "Tickets");
    }
}
