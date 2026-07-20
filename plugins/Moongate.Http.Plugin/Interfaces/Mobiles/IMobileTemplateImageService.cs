namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Disk PNG cache over dressed mobile-template figures.</summary>
public interface IMobileTemplateImageService
{
    /// <summary>The cached PNG's path, generating it on miss; null on unknown template or bodiless figure.</summary>
    Task<string?> GetOrCreateAsync(string templateId, CancellationToken cancellationToken = default);
}
