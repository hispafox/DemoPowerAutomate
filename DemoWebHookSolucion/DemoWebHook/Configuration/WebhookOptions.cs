namespace DemoWebHook.Configuration;

public class WebhookOptions
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public Dictionary<string, string> AdditionalHeaders { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 5;
}
