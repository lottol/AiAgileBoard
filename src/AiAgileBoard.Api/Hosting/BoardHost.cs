using AiAgileBoard.Api;
using AiAgileBoard.Application;
using AiAgileBoard.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace AiAgileBoard.Hosting;

public static class BoardHost
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {
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
        api.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithName("GetHealth");
        api.MapTicketEndpoints();
        app.MapFallbackToFile("index.html");
        return app;
    }

    public static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgileBoardDbContext>();
        var directory = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await db.Database.MigrateAsync(cancellationToken);
    }
}
