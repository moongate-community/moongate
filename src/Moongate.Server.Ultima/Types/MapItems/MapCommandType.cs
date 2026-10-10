namespace Moongate.Server.Ultima.Types.MapItems;

/// <summary>
///     The commands of the map packet (0x56), from the client and to it.
/// </summary>
public enum MapCommandType : byte
{
    None = 0,
    AddPin = 1,
    InsertPin = 2,
    ChangePin = 3,
    RemovePin = 4,

    /// <summary>
    ///     From the client, clears the course; to the client, shows the map.
    /// </summary>
    ClearPins = 5,
    ToggleEditable = 6,
    EditableAnswer = 7
}
