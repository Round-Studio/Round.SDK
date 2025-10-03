namespace Round.SDK.Plugin.RMCL;

public interface IPluginRMCL
{
    string Name { get; set; }
    string Description { get; set; }
    string Version { get; set; }
    string Author { get; set; }
    
    void Initialize();
}