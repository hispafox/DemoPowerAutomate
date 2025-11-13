using System.Text.Json.Serialization;

namespace DemoWebHook.Models;

public class FileEventPayload
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("oldFullPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldFullPath { get; set; }

    [JsonPropertyName("oldName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldName { get; set; }

    [JsonPropertyName("changeTimeUtc")]
    public DateTime ChangeTimeUtc { get; set; }

    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("watcherConfig")]
    public WatcherConfigInfo WatcherConfig { get; set; } = new();
}

public class WatcherConfigInfo
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("includeSubdirectories")]
    public bool IncludeSubdirectories { get; set; }

    [JsonPropertyName("filter")]
    public string Filter { get; set; } = string.Empty;
}
