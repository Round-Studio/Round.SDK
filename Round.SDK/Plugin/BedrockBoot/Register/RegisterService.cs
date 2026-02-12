using Round.SDK.Entry.BedrockBoot;

namespace Round.SDK.Plugin.BedrockBoot.Register;

public class RegisterService
{
    public static void RegisterTopBarItem(TopBarItemInfo info)
    {
        Console.WriteLine($@"注册导航项 {info.Tag}");
        API.RegisterNavigationBarItem?.Invoke(info);
    }

    public class API
    {
        public static Action<TopBarItemInfo>? RegisterNavigationBarItem { get; set; }
    }
}