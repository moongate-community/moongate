namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Disk PNG cache over body animation frames.</summary>
public interface IBodyImageService
{
    /// <summary>True once the client files behind the frames are loaded.</summary>
    bool IsReady { get; }

    /// <summary>The cached PNG's path, generating it on miss; null when the body has no animation.</summary>
    Task<string?> GetOrCreateAsync(int body, int hue, CancellationToken cancellationToken = default);
}
