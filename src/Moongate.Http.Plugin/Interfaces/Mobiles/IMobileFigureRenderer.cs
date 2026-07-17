using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Composites a dressed mobile figure (body + hair + facial hair + equipment).</summary>
public interface IMobileFigureRenderer
{
    /// <summary>The composited figure the caller owns, or null when the body has no animation.</summary>
    UltimaBitmap? Render(MobileFigureRequest request);
}
