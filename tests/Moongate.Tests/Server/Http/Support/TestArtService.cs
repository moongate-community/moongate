using Moongate.UO.Data.Interfaces.Art;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Server.Http.Support;

public sealed class TestArtService : IArtService
{
    public Func<int, bool, Image<Rgba32>?> GetArtImpl { get; init; } = (_, _) => null;

    public Func<int, bool> IsValidArtImpl { get; init; } = _ => false;

    public Image<Rgba32>? GetArt(int itemId, bool clone = true)
        => GetArtImpl(itemId, clone);

    public bool IsValidArt(int itemId)
        => IsValidArtImpl(itemId);
}
