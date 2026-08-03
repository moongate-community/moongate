namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>
/// Where a template's pictures live. In one place so the row and the detail cannot disagree, and
/// escaped because template ids come from YAML and are not guaranteed to be URL-safe.
/// </summary>
internal static class MobileTemplateUrls
{
    public static string Figure(string id)
        => $"/api/v1/images/mobiles/templates/{Uri.EscapeDataString(id)}.png";

    public static string Paperdoll(string id)
        => $"/api/v1/images/mobiles/templates/{Uri.EscapeDataString(id)}/paperdoll.png";
}
