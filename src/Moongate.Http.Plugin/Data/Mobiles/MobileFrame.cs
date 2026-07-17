using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>
/// One decoded animation frame for compositing: the bitmap and its anim.mul anchor point. The frame
/// owns its bitmap (catalog implementations hand out clones, never cache-owned surfaces).
/// </summary>
public sealed record MobileFrame(int CenterX, int CenterY, UltimaBitmap Bitmap) : IDisposable
{
    public void Dispose()
        => Bitmap.Dispose();
}
