namespace Moongate.Http.Plugin.Data.Config;

/// <summary>
/// The REST API's settings, the <c>http</c> section of moongate.yaml. Every field carries a default, so
/// the section may be omitted entirely — an operator writes one only to override, or to supply
/// <see cref="HttpJwtConfig.SigningKey" />, which is the one field with no usable default.
/// </summary>
public sealed class MoongateHttpConfig
{
    /// <summary>
    /// The interface to listen on. Defaults to every interface, matching the game port — which also
    /// means the admin API is reachable from outside unless an operator narrows it.
    /// </summary>
    public string Address { get; set; } = "0.0.0.0";

    public int Port { get; set; } = 8933;

    public HttpJwtConfig Jwt { get; set; } = new();
}
