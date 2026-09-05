using System.IO;
using System.Net;
using AiAgileBoard.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AiAgileBoard.Application;
using AiAgileBoard.Data.Projects;

namespace AiAgileBoard.Desktop;

internal static class DesktopHost
{
    public static WebApplication Build(string applicationDirectory, ProjectSession? session = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = applicationDirectory,
            WebRootPath = Path.Combine(applicationDirectory, "wwwroot"),
            EnvironmentName = "Production",
            ApplicationName = typeof(BoardHost).Assembly.GetName().Name
        });
        builder.Logging.ClearProviders();
        // Do not let server deployment settings expose the desktop API to the LAN.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Configure(new ConfigurationBuilder().Build());
            options.Listen(IPAddress.Loopback, 0);
        });
        builder.Configuration["ConnectionStrings:DefaultConnection"] =
            session?.ConnectionString ?? "Data Source=:memory:";
        if (session is not null) builder.Services.AddSingleton<IProjectPersistence>(session);
        return BoardHost.Build(builder, projectAvailable: session is not null);
    }

    public static Uri Address(WebApplication app) => new(
        app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single());
}
