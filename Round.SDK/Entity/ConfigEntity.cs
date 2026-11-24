using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Round.SDK.Entry;
using Round.SDK.Enum;
using Round.SDK.Global;

namespace Round.SDK.Entity;

public class ConfigEntity<T> where T : new()
{
    // 保存后的回调列表
    private readonly List<Action<ConfigEntity<T>>> _afterSaveCallbacks = new();

    // 保存前的回调列表
    private readonly List<Action<ConfigEntity<T>>> _beforeSaveCallbacks = new();
    private readonly JsonTypeInfo<T>? TypeInfo;

    public ConfigEntity(string configFile, bool isSave = true, JsonTypeInfo<T>? typeInfo = default)
    {
        Path = configFile;
        TypeInfo = typeInfo;
        IsSave = isSave;
        Load();
    }

    public T Data { get; set; }
    public string Path { get; }
    public bool IsSave { get; set; } = true;

    public static T JsonDeserialize(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOption.Options);
    }

    // 保存事件
    public event EventHandler<ConfigSaveEventArgs<T>>? BeforeSave;
    public event EventHandler<ConfigSaveEventArgs<T>>? AfterSave;

    // 添加保存前回调
    public void AddBeforeSaveCallback(Action<ConfigEntity<T>> callback)
    {
        _beforeSaveCallbacks.Add(callback);
    }

    // 添加保存后回调
    public void AddAfterSaveCallback(Action<ConfigEntity<T>> callback)
    {
        _afterSaveCallbacks.Add(callback);
    }

    // 移除回调
    public bool RemoveBeforeSaveCallback(Action<ConfigEntity<T>> callback)
    {
        return _beforeSaveCallbacks.Remove(callback);
    }

    public bool RemoveAfterSaveCallback(Action<ConfigEntity<T>> callback)
    {
        return _afterSaveCallbacks.Remove(callback);
    }

    // 清空所有回调
    public void ClearAllCallbacks()
    {
        _beforeSaveCallbacks.Clear();
        _afterSaveCallbacks.Clear();
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (IsSave)
                Save();
            else
                Data = new T();
            return;
        }

        var json = TryReadAllText(Path);
        if (json == null)
        {
            // 文件存在但此刻读不到（多半是别的线程正在原子替换它）。
            // 先用默认值顶着，既不抛异常也不回写，免得把用户配置覆盖成空白。
            Console.WriteLine($@"读取配置失败，暂用默认值：{Path}");
            Data = new T();
            return;
        }

        if (string.IsNullOrEmpty(json))
        {
            Data = new T();
            Save();
            return;
        }

        try
        {
            Data = TypeInfo != null
                ? JsonSerializer.Deserialize<T>(json, TypeInfo)
                : JsonSerializer.Deserialize<T>(json, JsonSerializerOption.Options);
        }
        catch
        {
            Data = new T();
            Save();
        }
    }

    /// <summary>
    /// 读取文件内容，遇到共享冲突（别的线程正在替换文件）时短暂重试。
    /// 重试用尽仍失败则返回 null，由调用方决定降级策略。
    /// </summary>
    private static string? TryReadAllText(string path)
    {
        const int maxAttempts = 3;
        for (var attempt = 1;; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       && attempt < maxAttempts)
            {
                Thread.Sleep(20 * attempt);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($@"读取 {path} 失败：{ex.Message}");
                return null;
            }
        }
    }

    public void Save()
    {
        if (!IsSave)
            return;

        // 触发保存前回调
        TriggerBeforeSave();

        Console.WriteLine($@"触发保存配置项：{Path}");
        if (Data == null) Data = new T();

        var jsresult = TypeInfo != null
            ? JsonSerializer.Serialize(Data, TypeInfo)
            : JsonSerializer.Serialize(Data, JsonSerializerOption.Options);

        WriteAllTextAtomic(Path, jsresult);

        // 触发保存后回调
        TriggerAfterSave();
    }

    // 同一份配置的写入串行化：并行读取目录（GetVersionConfigs 会多线程跑）
    // 触发的重复保存不会互相踩到临时文件或替换动作
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> SaveGates =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 原子写入：先写同目录临时文件，再整体替换目标文件。
    /// 直接 WriteAllText 会先截断目标文件，别的线程（缓存刷新、FileSystemWatcher）
    /// 一旦读到空文件或半个 JSON，轻则解析失败丢配置，重则在业务代码里 NRE。
    /// 替换偶尔会因为目标文件正被读取（FileShare.Read 不带 Delete 共享）而失败，短重试即可。
    /// </summary>
    private static void WriteAllTextAtomic(string path, string content)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var gate = SaveGates.GetOrAdd(path, static _ => new object());
        lock (gate)
        {
            const int maxAttempts = 5;
            for (var attempt = 1;; attempt++)
            {
                var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(tempPath, content);
                    File.Move(tempPath, path, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           && attempt < maxAttempts)
                {
                    TryDelete(tempPath);
                    Thread.Sleep(20 * attempt);
                }
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 临时文件清理失败不影响主流程
        }
    }

    private void TriggerBeforeSave()
    {
        try
        {
            // 触发回调函数
            foreach (var callback in _beforeSaveCallbacks) callback?.Invoke(this);

            // 触发事件
            BeforeSave?.Invoke(this, new ConfigSaveEventArgs<T>(this, SavePhase.Before));
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"保存前回调执行失败: {ex.Message}");
        }
    }

    private void TriggerAfterSave()
    {
        try
        {
            // 触发回调函数
            foreach (var callback in _afterSaveCallbacks) callback?.Invoke(this);

            // 触发事件
            AfterSave?.Invoke(this, new ConfigSaveEventArgs<T>(this, SavePhase.After));
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"保存后回调执行失败: {ex.Message}");
        }
    }
}