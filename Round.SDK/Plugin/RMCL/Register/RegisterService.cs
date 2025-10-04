using Round.SDK.Entry.RMCL;
using Round.SDK.Logger;

namespace Round.SDK.Plugin.RMCL.Register;

public class RegisterService
{
    public class API
    {
        public static Action<BottomBarItemInfo>? RegisterBottomBarItem { get; set; }
    }

    public static void RegisterBottomBarItem(BottomBarItemInfo info)
    {
        Console.WriteLine($@"注册导航项 {info.Tag}");
        API.RegisterBottomBarItem.Invoke(info);
    }
}