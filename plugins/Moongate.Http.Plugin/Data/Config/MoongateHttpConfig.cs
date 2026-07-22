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

    /// <summary>
    /// Where the built portal lives. Empty means "look in the usual places": <c>$MOONGATE_UI_DIST</c>, then
    /// <c>ui/dist</c> under the working directory, then <c>ui/dist</c> beside the executable.
    /// </summary>
    public string UiDistPath { get; set; } = string.Empty;

    public HttpJwtConfig Jwt { get; set; } = new();

    /// <summary>Maximum size, in bytes, of an uploaded server asset (logo/favicon/banner). Default 2 MB.</summary>
    public long MaxAssetUploadBytes { get; set; } = 2_097_152;

    /// <summary>How many web-registration attempts one caller may make per window. Default 5.</summary>
    public int RegistrationRateLimitPermits { get; set; } = 5;

    /// <summary>The rate-limit window for web registration, in minutes. Default 10.</summary>
    public int RegistrationRateLimitWindowMinutes { get; set; } = 10;
}
