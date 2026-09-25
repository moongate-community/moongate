namespace Moongate.Sample.Plugin.Types;

/// <summary>
///     How warmly the greeter speaks. Published to Lua as the
///     <c>
///         Tone
///     </c>
///     table.
/// </summary>
public enum Tone
{
    /// <summary>
    ///     "Hello, name!"
    /// </summary>
    Plain = 0,

    /// <summary>
    ///     "Hello there, name!"
    /// </summary>
    Warm = 1,

    /// <summary>
    ///     "Good day, name."
    /// </summary>
    Formal = 2
}
