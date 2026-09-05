using AiAgileBoard.Hosting;

var builder = WebApplication.CreateBuilder(args);

var app = BoardHost.Build(builder);
await BoardHost.InitializeDatabaseAsync(app);
app.Run();

public partial class Program { }
