namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Disk PNG cache over hair styles rendered on a reference body.</summary>
public interface IHairImageService
{
    /// <summary>The cached PNG's path, generating it on miss; null when nothing decodes.</summary>
    Task<string?> GetOrCreateAsync(
        int style,
        int hue,
        int referenceBody,
        bool facial,
        CancellationToken cancellationToken = default
    );
}
