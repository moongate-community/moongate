namespace Moongate.Http.Plugin.Data.Config;

/// <summary>JWT settings for the REST API, nested under <c>http.Jwt</c> in moongate.yaml.</summary>
public sealed class HttpJwtConfig
{
    /// <summary>
    /// The HS256 signing key. It has no usable default on purpose: a key regenerated per restart would
    /// silently invalidate every issued token on every restart and read as a random bug. At least 32
    /// bytes; the server refuses to start without one.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long an issued token stays valid.</summary>
    public int LifetimeMinutes { get; set; } = 60;

    /// <summary>The <c>iss</c> claim, and the issuer tokens are validated against.</summary>
    public string Issuer { get; set; } = "moongate";
}
