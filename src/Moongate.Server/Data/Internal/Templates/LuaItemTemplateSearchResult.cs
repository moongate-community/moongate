namespace Moongate.Server.Data.Internal.Templates;

internal sealed class LuaItemTemplateSearchResult
{
    public LuaItemTemplateSearchResult(string templateId, string displayName, int itemId)
    {
        TemplateId = templateId;
        DisplayName = displayName;
        ItemId = itemId;
    }

    public string TemplateId { get; }

    public string DisplayName { get; }

    public int ItemId { get; }
}
