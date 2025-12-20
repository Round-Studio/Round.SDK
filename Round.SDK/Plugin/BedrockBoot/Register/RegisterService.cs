using Round.SDK.Entry.BedrockBoot;

namespace Round.SDK.Plugin.BedrockBoot.Register;

public class RegisterService
{
    public class API
    {
        public static Action<TopBarItemInfo>? RegisterTopBarItem { get; set; }
    }

    public static void RegisterTopBarItem(TopBarItemInfo info)
    {
        Console.WriteLine($@"注册导航项 {info.Tag}");
        API.RegisterTopBarItem.Invoke(info);
    }
}