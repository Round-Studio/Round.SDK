namespace Round.SDK.Entry.BedrockBoot;

public class InstanceControlItemInfo
{
    public required string Header { get; set; }
    public required string Description { get; set; }
    public string ItemGlyph { get; set; } = "&#xE80F;";
    public Action<string>? Callback { get; set; } = null;
}