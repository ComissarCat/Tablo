using System.Text;
using Serilog;
using Tablo.Server;
using Tablo.Shared;

// Без этого имена кодировок вроде WIN1251 (кодировка боевой БД) не
// распознаются FirebirdClient и соединение падает с
// "Invalid character set specified" ещё до попытки достучаться до сети.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/tablo-server-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Возможность запускаться как служба Windows или systemd-сервис на Linux
    // без изменения кода — оба хоста добавляются безусловно, лишний no-op
    // на "неродной" платформе.
    builder.Host.UseWindowsService();
    builder.Host.UseSystemd();

    var connectionString = builder.Configuration.GetConnectionString("Firebird")
        ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Firebird");

    builder.Services.AddSingleton(new FirebirdCaseRepository(connectionString));
    builder.Services.AddSingleton<CaseDataCache>();
    builder.Services.AddHostedService<CaseDataPollerService>();
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();
    app.UseCors();

    app.MapGet("/api/cases", (CaseDataCache cache) => Results.Ok(cache.GetCasesResponse()));
    app.MapGet("/api/halls", (CaseDataCache cache) => Results.Ok(cache.GetHalls()));
    app.MapGet("/health", (CaseDataCache cache) =>
    {
        var response = cache.GetCasesResponse();
        return Results.Ok(new { status = response.HasEverSucceeded ? "ok" : "starting", response.IsStale, response.GeneratedAtUtc });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Tablo.Server остановлен из-за необработанной ошибки при старте");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
