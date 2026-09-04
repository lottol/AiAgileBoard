using AiAgileBoard.Api;
using AiAgileBoard.Application;
using AiAgileBoard.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AgileBoardDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<TicketService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api/v1");
api.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("GetHealth");
api.MapTicketEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AgileBoardDbContext>();
    var databasePath = dbContext.Database.GetDbConnection().DataSource;
    var databaseDirectory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrEmpty(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }

    dbContext.Database.Migrate();
}

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
