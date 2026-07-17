namespace Moongate.Http.Plugin.Types;

/// <summary>How a map facet is rendered into tiles.</summary>
public enum MapRenderStyleType
{
    /// <summary>The flat radar map: one colour per map tile.</summary>
    Flat,

    /// <summary>The radar map with altitude-based relief shading — hills and valleys read as light and shadow.</summary>
    Relief,
}
