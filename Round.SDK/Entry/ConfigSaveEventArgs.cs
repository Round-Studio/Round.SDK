using Round.SDK.Entity;
using Round.SDK.Enum;

namespace Round.SDK.Entry;

// 保存事件参数
public class ConfigSaveEventArgs<T> : EventArgs where T : new()
{
    public ConfigSaveEventArgs(ConfigEntity<T> config, SavePhase phase)
    {
        Config = config;
        Phase = phase;
        Timestamp = DateTime.Now;
    }

    public ConfigEntity<T> Config { get; }
    public SavePhase Phase { get; }
    public DateTime Timestamp { get; }
}