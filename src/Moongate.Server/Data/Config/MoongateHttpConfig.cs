namespace Moongate.Server.Data.Config;

/// <summary>
/// Represents MoongateHttpConfig.
/// </summary>
public class MoongateHttpConfig
{
    public bool IsEnabled { get; set; } = true;

    public int Port { get; set; } = 8088;

    public string WebsiteUrl { get; set; } = "http://localhost";

    public bool IsOpenApiEnabled { get; set; } = true;

    public MoongateHttpJwtConfig Jwt { get; set; } = new();
}
