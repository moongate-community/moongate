namespace Moongate.Server.Http.Data;

/// <summary>
/// Represents server version metadata exposed by HTTP endpoints.
/// </summary>
public sealed class MoongateHttpServerVersion
{
    /// <summary>
    /// Gets or sets semantic version string.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets server codename.
    /// </summary>
    public string Codename { get; set; }
}
