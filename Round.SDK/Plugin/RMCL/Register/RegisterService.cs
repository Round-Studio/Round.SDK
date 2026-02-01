using Round.SDK.Entry.RMCL;

namespace Round.SDK.Plugin.RMCL.Register;

public class RegisterService
{
    public static void RegisterBottomBarItem(BottomBarItemInfo info)
    {
        Console.WriteLine($@"注册导航项 {info.Tag}");
        API.RegisterBottomBarItem.Invoke(info);
    }

    public class API
    {
        public static Action<BottomBarItemInfo>? RegisterBottomBarItem { get; set; }
    }
}