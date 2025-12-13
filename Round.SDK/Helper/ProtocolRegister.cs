using Microsoft.Win32;

namespace Round.SDK.Helper;

public class ProtocolRegister
{
    public string ProtocolName = "RoundStudio";
    public string ProtocolDescription = "Round Studio 系列产品";
    
    public void RegisterProtocol(string applicationPath)
    {
        // 注册表路径
        string protocolPath = $@"Software\Classes\{ProtocolName}";
        
        try
        {
            // 创建协议根键
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(protocolPath))
            {
                key.SetValue("", ProtocolDescription);
                key.SetValue("URL Protocol", "");
                
                // 创建默认图标
                using (RegistryKey defaultIcon = key.CreateSubKey("DefaultIcon"))
                {
                    defaultIcon.SetValue("", $"{applicationPath},1");
                }
                
                // 创建命令
                using (RegistryKey shell = key.CreateSubKey(@"shell\open\command"))
                {
                    shell.SetValue("", $"\"{applicationPath}\" -shell \"%1\"");
                }
            }
            
            // 对于 Windows 11/10 需要额外的注册
            RegisterForWindows10(applicationPath);
            
            Console.WriteLine($"协议 {ProtocolName}:// 注册成功！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"注册失败: {ex.Message}");
        }
    }
    
    private void RegisterForWindows10(string applicationPath)
    {
        // Windows 10/11 需要注册到 Capabilities
        string capabilitiesPath = @"Software\Clients\StartMenuInternet\" + 
                                  System.IO.Path.GetFileNameWithoutExtension(applicationPath);
        
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                $@"Software\{System.IO.Path.GetFileNameWithoutExtension(applicationPath)}\Capabilities"))
            {
                key.SetValue("ApplicationDescription", ProtocolDescription);
                key.SetValue("ApplicationName", ProtocolDescription);
                
                using (RegistryKey urlAssociations = key.CreateSubKey("URLAssociations"))
                {
                    urlAssociations.SetValue(ProtocolName + "://", ProtocolDescription);
                }
            }
            
            // 注册到已注册应用
            using (RegistryKey registeredApps = Registry.CurrentUser.CreateSubKey(
                @"Software\RegisteredApplications"))
            {
                registeredApps.SetValue(ProtocolDescription,
                    $@"Software\{System.IO.Path.GetFileNameWithoutExtension(applicationPath)}\Capabilities");
            }
        }
        catch { /* 忽略错误，这不是必需的 */ }
    }
    
    public void UnregisterProtocol()
    {
        try
        {
            // 删除协议注册
            string protocolPath = $@"Software\Classes\{ProtocolName}";
            Registry.CurrentUser.DeleteSubKeyTree(protocolPath, false);
            
            // 清理其他注册
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\{ProtocolName}Capabilities", false);
            
            Console.WriteLine($"协议 {ProtocolName}:// 已卸载！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"卸载失败: {ex.Message}");
        }
    }
}