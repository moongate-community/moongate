using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Composites a UO paperdoll from gump art: background, body, hair/beard, worn equipment.</summary>
public interface IPaperdollRenderer
{
    /// <summary>The composited paperdoll the caller owns, or null when the body gump is missing.</summary>
    UltimaBitmap? Render(PaperdollRenderRequest request);
}
