namespace Moongate.Server.Ultima.Types.Effects;

/// <summary>
///     How the client blends the graphic of an effect with what is behind it (the render mode of 0xC0 and 0xC7), as
///     documented by POL.
/// </summary>
public enum EffectRenderModeType
{
    /// <summary>
    ///     Drawn as it is.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///     Darkens what is behind.
    /// </summary>
    Darken = 1,

    /// <summary>
    ///     Lightens what is behind.
    /// </summary>
    Lighten = 2,

    /// <summary>
    ///     Lightens more, and the dark parts become transparent.
    /// </summary>
    LightenTransparent = 3,

    /// <summary>
    ///     Translucent.
    /// </summary>
    Translucent = 4,

    /// <summary>
    ///     Translucent, close to its main colour.
    /// </summary>
    TranslucentColor = 5,

    /// <summary>
    ///     Negative colours.
    /// </summary>
    Negative = 6,

    /// <summary>
    ///     Negative colours on a transparent background.
    /// </summary>
    NegativeTransparent = 7
}
