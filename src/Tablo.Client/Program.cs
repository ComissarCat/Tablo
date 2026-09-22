using Avalonia;
using System;
using Serilog;

namespace Tablo.Client;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/tablo-client-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        // Ловим всё, что можно поймать, и логируем перед тем, как процесс
        // упадёт — реальный перезапуск после краша делает внешний watchdog
        // (Планировщик заданий / systemd, см. deploy/).
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Необработанное исключение в AppDomain");

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Необработанное исключение в фоновой задаче");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Tablo.Client остановлен из-за необработанной ошибки");
            // Ненулевой код выхода — чтобы внешний watchdog (Планировщик
            // заданий/systemd) понял, что это сбой, и перезапустил процесс.
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
