using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DemoWebHook.Configuration;
using DemoWebHook.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoWebHook.Services;

public class WebhookClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookClient> _logger;

    public WebhookClient(IOptions<WebhookOptions> options, ILogger<WebhookClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        // Agregar cabeceras adicionales
        foreach (var header in _options.AdditionalHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    public async Task<bool> SendEventAsync(FileEventPayload payload, CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        int delay = _options.RetryBaseDelaySeconds;

        while (attempt <= _options.MaxRetries)
        {
            try
            {
                attempt++;
                _logger.LogDebug("Enviando evento al webhook (intento {Attempt}/{MaxRetries}): {EventType} - {FileName}", 
                    attempt, _options.MaxRetries + 1, payload.EventType, payload.Name);

                var response = await _httpClient.PostAsJsonAsync(_options.Url, payload, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("? Evento enviado exitosamente: {EventType} - {FileName} ({StatusCode})", 
                        payload.EventType, payload.Name, (int)response.StatusCode);
                    return true;
                }

                _logger.LogWarning("? Webhook respondió con código no exitoso: {StatusCode} para {EventType} - {FileName}", 
                    (int)response.StatusCode, payload.EventType, payload.Name);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout al enviar evento {EventType} - {FileName} (intento {Attempt}): {Message}", 
                    payload.EventType, payload.Name, attempt, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error HTTP al enviar evento {EventType} - {FileName} (intento {Attempt}): {Message}", 
                    payload.EventType, payload.Name, attempt, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al enviar evento {EventType} - {FileName} (intento {Attempt})", 
                    payload.EventType, payload.Name, attempt);
            }

            // Si no es el último intento, esperar antes de reintentar
            if (attempt <= _options.MaxRetries)
            {
                _logger.LogDebug("Esperando {Delay} segundos antes del siguiente reintento...", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                delay *= 2; // Backoff exponencial
            }
        }

        var maskedUrl = MaskSensitiveUrl(_options.Url);
        _logger.LogError("? Falló el envío del evento después de {Attempts} intentos: {EventType} - {FileName} al webhook {Url}", 
            attempt, payload.EventType, payload.Name, maskedUrl);
        
        return false;
    }

    private static string MaskSensitiveUrl(string url)
    {
        // Enmascarar querystring para seguridad
        var regex = new Regex(@"(\?.*)", RegexOptions.IgnoreCase);
        return regex.Replace(url, "?***");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
