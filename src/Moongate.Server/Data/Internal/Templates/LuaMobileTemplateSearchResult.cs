namespace Moongate.Server.Data.Internal.Templates;

internal sealed class LuaMobileTemplateSearchResult
{
    public LuaMobileTemplateSearchResult(string templateId, string displayName)
    {
        TemplateId = templateId;
        DisplayName = displayName;
    }

    public string TemplateId { get; }

    public string DisplayName { get; }
}
