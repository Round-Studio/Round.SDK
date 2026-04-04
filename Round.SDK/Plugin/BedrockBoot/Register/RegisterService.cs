using Round.SDK.Entry.BedrockBoot;

namespace Round.SDK.Plugin.BedrockBoot.Register;

public class RegisterService
{
    public static void RegisterTopBarItem(TopBarItemInfo info)
    {
        Console.WriteLine($@"注册导航项 {info.Tag}");
        API.RegisterNavigationBarItem?.Invoke(info);
    }
    public static void RegisterInstanceControlItem(InstanceControlItemInfo info)
    {
        Console.WriteLine($@"注册实例操作项 {info.Header}");
        API.InstanceControlItems.Add(info);
    }
    public static void RegisterLaunchingEvent(Action<string> action)
    {
        Console.WriteLine($@"注册启动操作 {action.Method.Name}");
        API.LaunchingEvent.Add(action);
    }

    public class API
    {
        public static Action<TopBarItemInfo>? RegisterNavigationBarItem { get; set; }
        public static List<InstanceControlItemInfo> InstanceControlItems { get; set; } = new();
        public static List<Action<string>> LaunchingEvent { get; set; } = new();
    }
}