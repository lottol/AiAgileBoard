using AiAgileBoard.Api;
using AiAgileBoard.Application;
using AiAgileBoard.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiAgileBoard.Hosting;

public static class BoardHost
{
    public static WebApplication Build(WebApplicationBuilder builder, bool projectAvailable = true)
    {
        builder.Services.AddProblemDetails();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddDbContext<AgileBoardDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddScoped<TicketService>();
        builder.Services.TryAddSingleton<IProjectPersistence, DatabasePersistence>();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (ProjectSavePendingException)
            {
                await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Project save required", detail: "Retry saving the project before making more changes.")
                    .ExecuteAsync(context);
            }
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        var api = app.MapGroup("/api/v1");
        api.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithName("GetHealth");
        if (projectAvailable) api.MapTicketEndpoints();
        else api.Map("/tickets/{**path}", () => Results.Conflict(new { error = "No project is open." }));
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
