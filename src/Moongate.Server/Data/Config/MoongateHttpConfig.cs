namespace Moongate.Server.Data.Config;

/// <summary>
/// Represents MoongateHttpConfig.
/// </summary>
public class MoongateHttpConfig
{
    public bool IsEnabled { get; set; } = true;

    public int Port { get; set; } = 8088;

    public string WebsiteUrl { get; set; } = "http://localhost";

    public string AdminLoginLogoPath { get; set; } = "images/moongate_logo_admin.png";

    public string PlayerLoginLogoPath { get; set; } = "images/moongate_logo_players.png";

    public bool IsOpenApiEnabled { get; set; } = true;

    public MoongateHttpJwtConfig Jwt { get; set; } = new();
}
