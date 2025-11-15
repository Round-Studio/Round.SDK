namespace Round.SDK.Entry.Helper;


public class MonitorEntry
{
    public string Path { get; set; }
    public Action<long, long> BackCall { get; set; }
    public FileSystemWatcher Watcher { get; set; }
}