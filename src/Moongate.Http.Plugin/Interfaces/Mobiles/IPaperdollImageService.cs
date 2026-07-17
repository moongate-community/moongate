namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Disk PNG cache over mobile-template paperdolls.</summary>
public interface IPaperdollImageService
{
    /// <summary>The cached PNG's path, generating it on miss; null on unknown template or missing body gump.</summary>
    Task<string?> GetOrCreateAsync(string templateId, bool includeBackground, CancellationToken cancellationToken = default);
}
