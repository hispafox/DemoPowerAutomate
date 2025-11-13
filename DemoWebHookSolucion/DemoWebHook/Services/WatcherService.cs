using DemoWebHook.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoWebHook.Services;

public class WatcherService : IDisposable
{
    private readonly WatcherOptions _options;
    private readonly NotificationService _notificationService;
    private readonly ILogger<WatcherService> _logger;
    private FileSystemWatcher? _watcher;
    private readonly CancellationTokenSource _cts;
    private bool _isRunning;

    public WatcherService(
        IOptions<WatcherOptions> options,
        NotificationService notificationService,
        ILogger<WatcherService> logger)
    {
        _options = options.Value;
        _notificationService = notificationService;
        _logger = logger;
        _cts = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("===========================================");
        _logger.LogInformation("Iniciando FileSystemWatcher");
        _logger.LogInformation("===========================================");
        _logger.LogInformation("Ruta: {Path}", _options.Path);
        _logger.LogInformation("Incluir subdirectorios: {IncludeSubdirectories}", _options.IncludeSubdirectories);
        _logger.LogInformation("Filtro: {Filter}", _options.Filter);
        _logger.LogInformation("Eventos activos: Created={Created}, Changed={Changed}, Deleted={Deleted}, Renamed={Renamed}",
            _options.NotifyOnCreated, _options.NotifyOnChanged, _options.NotifyOnDeleted, _options.NotifyOnRenamed);
        _logger.LogInformation("===========================================");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                InitializeWatcher();
                _isRunning = true;

                // Mantener el servicio en ejecución
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WatcherService: Cancelación solicitada");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar o ejecutar FileSystemWatcher");
                _isRunning = false;

                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Reintentando inicialización en 30 segundos...");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
        }

        _logger.LogInformation("WatcherService: Detenido");
    }

    private void InitializeWatcher()
    {
        // Validar que la ruta existe
        if (!Directory.Exists(_options.Path))
        {
            throw new DirectoryNotFoundException($"La ruta especificada no existe: {_options.Path}");
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(_options.Path)
        {
            Filter = _options.Filter,
            IncludeSubdirectories = _options.IncludeSubdirectories,
            NotifyFilter = NotifyFilters.FileName 
                         | NotifyFilters.DirectoryName 
                         | NotifyFilters.LastWrite 
                         | NotifyFilters.Size
        };

        // Suscribirse a eventos según configuración
        if (_options.NotifyOnCreated)
        {
            _watcher.Created += OnCreated;
        }

        if (_options.NotifyOnChanged)
        {
            _watcher.Changed += OnChanged;
        }

        if (_options.NotifyOnDeleted)
        {
            _watcher.Deleted += OnDeleted;
        }

        if (_options.NotifyOnRenamed)
        {
            _watcher.Renamed += OnRenamed;
        }

        _watcher.Error += OnError;

        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("? FileSystemWatcher inicializado y activo");
    }

    private async void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Evento detectado: Created - {FullPath}", e.FullPath);
        await _notificationService.EnqueueEventAsync(e, "Created");
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Evento detectado: Changed - {FullPath}", e.FullPath);
        await _notificationService.EnqueueEventAsync(e, "Changed");
    }

    private async void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Evento detectado: Deleted - {FullPath}", e.FullPath);
        await _notificationService.EnqueueEventAsync(e, "Deleted");
    }

    private async void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("Evento detectado: Renamed - {OldFullPath} -> {FullPath}", e.OldFullPath, e.FullPath);
        await _notificationService.EnqueueRenamedEventAsync(e);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex, "Error en FileSystemWatcher");
        _isRunning = false;
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("WatcherService: Deteniendo FileSystemWatcher...");
        
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _cts.Cancel();
        await _notificationService.StopAsync();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _cts?.Dispose();
        _notificationService?.Dispose();
    }
}
