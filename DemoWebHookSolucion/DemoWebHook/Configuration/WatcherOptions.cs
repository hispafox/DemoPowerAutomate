namespace DemoWebHook.Configuration;

public class WatcherOptions
{
    public string Path { get; set; } = string.Empty;
    public bool IncludeSubdirectories { get; set; } = true;
    public string Filter { get; set; } = "*.*";
    public bool NotifyOnCreated { get; set; } = true;
    public bool NotifyOnChanged { get; set; } = true;
    public bool NotifyOnDeleted { get; set; } = true;
    public bool NotifyOnRenamed { get; set; } = true;
}
