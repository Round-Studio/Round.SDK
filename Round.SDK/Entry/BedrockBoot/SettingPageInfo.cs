namespace Round.SDK.Entry.BedrockBoot;

public class SettingPageInfo
{
    public string Header { get; set; } = "My Setting";
    public string Description { get; set; } = "My description";
    public object? Page { get; set; } = "";
    public bool IsUseFontIcon { get; set; } = true;
    public string IconSource { get; set; } = "\uE713";
}