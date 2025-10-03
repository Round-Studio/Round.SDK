namespace Round.SDK.Entry.RMCL;

public class BottomBarItemInfo
{
    public string ItemText { get; set; } = "Item";
    public string ItemGlyph { get; set; } = "&#xE80F;";
    public string Tag { get; set; } = "Home";
    public Type? PageType { get; set; }
    public bool IsSelected { get; set; } = false;
}