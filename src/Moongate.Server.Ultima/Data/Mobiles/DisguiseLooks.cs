namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     What a disguise changes of a mobile: the body, the skin hue and the name; each unset part stays as it is.
/// </summary>
/// <param name="Body">The body graphic, or null to keep the mobile's own.</param>
/// <param name="Hue">The skin hue, or null to keep the mobile's own.</param>
/// <param name="Name">The name, or null to keep the mobile's own.</param>
public sealed record DisguiseLooks(int? Body = null, int? Hue = null, string? Name = null);
