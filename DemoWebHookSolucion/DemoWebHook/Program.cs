using DemoWebHook.Configuration;
using DemoWebHook.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DemoWebHook;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Configuración
            var configuration = BuildConfiguration(args);

            // Configurar servicios
            var services = new ServiceCollection();
            ConfigureServices(services, configuration);

            // Construir el contenedor de DI
            using var serviceProvider = services.BuildServiceProvider();

            // Configurar logger inicial
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
            logger.LogInformation("║      FileSystemWatcher → Power Automate Webhook           ║");
            logger.LogInformation("║                    v1.0.0                                 ║");
            logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
            logger.LogInformation("");

            // Mostrar configuración efectiva
            var watcherOptions = configuration.GetSection("Watcher").Get<WatcherOptions>();
            var webhookOptions = configuration.GetSection("Webhook").Get<WebhookOptions>();

            logger.LogInformation("Configuración cargada:");
            logger.LogInformation("  - Ruta: {Path}", watcherOptions?.Path);
            logger.LogInformation("  - Filtro: {Filter}", watcherOptions?.Filter);
            logger.LogInformation("  - Webhook URL: {Url}", MaskWebhookUrl(webhookOptions?.Url ?? ""));
            logger.LogInformation("");

            // Configurar manejo de Ctrl+C
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                logger.LogWarning("Ctrl+C detectado. Deteniendo aplicación...");
                e.Cancel = true;
                cts.Cancel();
            };

            // Iniciar el servicio de vigilancia
            var watcherService = serviceProvider.GetRequiredService<WatcherService>();
            
            logger.LogInformation("Presione Ctrl+C para detener la aplicación");
            logger.LogInformation("");

            await watcherService.StartAsync(cts.Token);

            // Detener servicios
            await watcherService.StopAsync();

            logger.LogInformation("");
            logger.LogInformation("Aplicación finalizada correctamente");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fatal: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static IConfiguration BuildConfiguration(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "FILEWATCHER_")
            .AddCommandLine(args, new Dictionary<string, string>
            {
                { "--path", "Watcher:Path" },
                { "--filter", "Watcher:Filter" },
                { "--includeSubdirectories", "Watcher:IncludeSubdirectories" },
                { "--webhookUrl", "Webhook:Url" }
            })
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configurar opciones
        services.Configure<WatcherOptions>(configuration.GetSection("Watcher"));
        services.Configure<WebhookOptions>(configuration.GetSection("Webhook"));
        services.Configure<LoggingOptions>(configuration.GetSection("Logging"));

        // Configurar logging
        var loggingOptions = configuration.GetSection("Logging").Get<LoggingOptions>();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(ParseLogLevel(loggingOptions?.Level ?? "Information"));
            builder.AddConsole();

            if (loggingOptions?.LogToFile == true)
            {
                var logPath = loggingOptions.LogFilePath;
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                builder.AddFile(logPath);
            }
        });

        // Registrar servicios
        services.AddSingleton<WebhookClient>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<WatcherService>();
    }

    private static LogLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    private static string MaskWebhookUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        
        var questionMarkIndex = url.IndexOf('?');
        if (questionMarkIndex > 0)
        {
            return url.Substring(0, questionMarkIndex) + "?***";
        }
        return url;
    }
}
