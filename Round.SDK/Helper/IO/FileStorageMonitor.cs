using Round.SDK.Entry.Helper;

namespace Round.SDK.Helper.IO;

public class FileStorageMonitor : IDisposable
{
    private readonly Timer _debounceTimer;
    private readonly object _lockObject = new();
    private readonly List<MonitorEntry> _monitorEntries = new();
    private readonly Action<long> _totalChangeCallback;
    private bool _isProcessing;
    private long _lastTotalSize;

    public FileStorageMonitor(Action<long> totalChangeCallback)
    {
        _totalChangeCallback = totalChangeCallback;
        // 创建去抖计时器，500ms 延迟
        _debounceTimer = new Timer(DebounceTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        foreach (var entry in _monitorEntries) entry.Watcher?.Dispose();
        _monitorEntries.Clear();
    }

    public void Add(MonitorEntry entry)
    {
        if (!Directory.Exists(entry.Path)) throw new DirectoryNotFoundException($"目录不存在: {entry.Path}");

        // 创建文件系统监视器
        var watcher = new FileSystemWatcher
        {
            Path = entry.Path,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size |
                           NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            InternalBufferSize = 65536 // 增加缓冲区大小
        };

        // 注册事件处理程序
        watcher.Changed += (sender, e) => OnFileSystemChange(entry, e);
        watcher.Created += (sender, e) => OnFileSystemChange(entry, e);
        watcher.Deleted += (sender, e) => OnFileSystemChange(entry, e);
        watcher.Renamed += (sender, e) => OnFileSystemChange(entry, e);
        watcher.Error += (sender, e) => OnWatcherError(entry, e);

        entry.Watcher = watcher;
        _monitorEntries.Add(entry);

        // 初始计算大小
        Task.Run(() => CalculateAndNotify());
    }

    private void OnFileSystemChange(MonitorEntry entry, FileSystemEventArgs e)
    {
        // 使用去抖机制，避免频繁触发
        lock (_lockObject)
        {
            _debounceTimer.Change(500, Timeout.Infinite);
        }
    }

    private void OnWatcherError(MonitorEntry entry, ErrorEventArgs e)
    {
        Console.WriteLine($"文件监视器错误 (路径: {entry.Path}): {e.GetException().Message}");
        // 可以考虑重新启动监视器
    }

    private void DebounceTimerCallback(object state)
    {
        lock (_lockObject)
        {
            if (_isProcessing) return;
            _isProcessing = true;
        }

        try
        {
            CalculateAndNotify();
        }
        finally
        {
            lock (_lockObject)
            {
                _isProcessing = false;
            }
        }
    }

    private void CalculateAndNotify()
    {
        try
        {
            var totalSize = GetTotalSize();

            if (totalSize != _lastTotalSize)
            {
                _lastTotalSize = totalSize;

                // 在主线程或线程池中调用回调
                Task.Run(() =>
                {
                    // 调用总变化回调
                    _totalChangeCallback?.Invoke(totalSize);

                    // 调用所有注册的文件夹回调
                    NotifyAllCallbacks(totalSize);
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"计算和通知时出错: {ex.Message}");
        }
    }

    private void NotifyAllCallbacks(long totalSize)
    {
        foreach (var entry in _monitorEntries)
            try
            {
                var currentSize = CalculateDirectorySize(entry.Path);
                entry.BackCall?.Invoke(totalSize, currentSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"调用回调时出错 (路径: {entry.Path}): {ex.Message}");
            }
    }

    public long GetTotalSize()
    {
        long totalSize = 0;
        foreach (var entry in _monitorEntries) totalSize += CalculateDirectorySize(entry.Path);
        return totalSize;
    }

    private long CalculateDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists) return 0;

            // 使用并行处理提高性能
            var files = directory.GetFiles("*.*", SearchOption.AllDirectories)
                .AsParallel()
                .Where(f =>
                {
                    try
                    {
                        return f.Exists;
                    }
                    catch
                    {
                        return false;
                    }
                });

            size = files.Sum(f =>
            {
                try
                {
                    return f.Length;
                }
                catch
                {
                    return 0;
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"计算目录大小时出错: {path}, 错误: {ex.Message}");
        }

        return size;
    }

    public void Remove(string path)
    {
        var entry = _monitorEntries.FirstOrDefault(e => e.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            entry.Watcher?.Dispose();
            _monitorEntries.Remove(entry);
            Task.Run(() => CalculateAndNotify());
        }
    }

    public void Clear()
    {
        foreach (var entry in _monitorEntries) entry.Watcher?.Dispose();
        _monitorEntries.Clear();
        _lastTotalSize = 0;
        _totalChangeCallback?.Invoke(0);
    }
}