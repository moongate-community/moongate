namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>
/// A rendered image and the fingerprint it was rendered from. The hash travels with the path
/// because the route needs it as an ETag, and recomputing it there would be doing the work twice.
/// </summary>
/// <param name="Path">Where the PNG sits in the cache.</param>
/// <param name="Hash">The appearance fingerprint: the file's name, and the ETag.</param>
public sealed record MobileImage(string Path, string Hash);
