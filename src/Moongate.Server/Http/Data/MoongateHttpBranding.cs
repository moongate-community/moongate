namespace Moongate.Server.Http.Data;

/// <summary>
/// Public branding payload used by login pages.
/// </summary>
public sealed class MoongateHttpBranding
{
    public string ShardName { get; set; } = "Moongate";

    public string? AdminLoginLogoUrl { get; set; }

    public string? PlayerLoginLogoUrl { get; set; }
}
