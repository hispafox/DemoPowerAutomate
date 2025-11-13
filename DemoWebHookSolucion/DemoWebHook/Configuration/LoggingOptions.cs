namespace DemoWebHook.Configuration;

public class LoggingOptions
{
    public string Level { get; set; } = "Information";
    public bool LogToFile { get; set; } = true;
    public string LogFilePath { get; set; } = "logs\\watcher.log";
}
