namespace Moongate.Server.Ultima.Types.Gumps;

/// <summary>
///     What a control with <c>bind</c> writes into the gump's arguments when the player answers.
/// </summary>
public enum GumpBindType : byte
{
    /// <summary>
    ///     A text entry: its text.
    /// </summary>
    Text,

    /// <summary>
    ///     A checkbox: whether it is on.
    /// </summary>
    Checkbox,

    /// <summary>
    ///     A radio button: its switch id when it is the one on.
    /// </summary>
    Radio
}
