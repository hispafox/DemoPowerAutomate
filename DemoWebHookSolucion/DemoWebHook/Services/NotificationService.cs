using System.Threading.Channels;
using DemoWebHook.Configuration;
using DemoWebHook.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoWebHook.Services;

public class NotificationService : IDisposable
{
    private readonly Channel<FileEventPayload> _eventChannel;
    private readonly WebhookClient _webhookClient;
    private readonly WatcherOptions _watcherOptions;
    private readonly ILogger<NotificationService> _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;

    public NotificationService(
        WebhookClient webhookClient,
        IOptions<WatcherOptions> watcherOptions,
        ILogger<NotificationService> logger)
    {
        _webhookClient = webhookClient;
        _watcherOptions = watcherOptions.Value;
        _logger = logger;
        _cts = new CancellationTokenSource();

        // Canal con capacidad limitada para evitar consumo excesivo de memoria
        _eventChannel = Channel.CreateBounded<FileEventPayload>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // Iniciar tarea de procesamiento en background
        _processingTask = Task.Run(() => ProcessEventsAsync(_cts.Token));
    }

    public async Task EnqueueEventAsync(FileSystemEventArgs e, string eventType)
    {
        var payload = new FileEventPayload
        {
            EventType = eventType,
            FullPath = e.FullPath,
            Name = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeTimeUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            WatcherConfig = new WatcherConfigInfo
            {
                Path = _watcherOptions.Path,
                IncludeSubdirectories = _watcherOptions.IncludeSubdirectories,
                Filter = _watcherOptions.Filter
            }
        };

        await _eventChannel.Writer.WriteAsync(payload);
    }

    public async Task EnqueueRenamedEventAsync(RenamedEventArgs e)
    {
        var payload = new FileEventPayload
        {
            EventType = "Renamed",
            FullPath = e.FullPath,
            Name = e.Name ?? Path.GetFileName(e.FullPath),
            OldFullPath = e.OldFullPath,
            OldName = e.OldName,
            ChangeTimeUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            WatcherConfig = new WatcherConfigInfo
            {
                Path = _watcherOptions.Path,
                IncludeSubdirectories = _watcherOptions.IncludeSubdirectories,
                Filter = _watcherOptions.Filter
            }
        };

        await _eventChannel.Writer.WriteAsync(payload);
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("NotificationService: Iniciando procesamiento de eventos...");

        try
        {
            await foreach (var payload in _eventChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await _webhookClient.SendEventAsync(payload, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar evento: {EventType} - {FileName}", 
                        payload.EventType, payload.Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NotificationService: Procesamiento de eventos cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en el procesamiento de eventos");
        }
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("NotificationService: Deteniendo...");
        _eventChannel.Writer.Complete();
        _cts.Cancel();
        
        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException)
        {
            // Esperado al cancelar
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _webhookClient?.Dispose();
    }
}
