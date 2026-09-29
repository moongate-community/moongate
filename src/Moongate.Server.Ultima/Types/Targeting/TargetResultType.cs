namespace Moongate.Server.Ultima.Types.Targeting;

/// <summary>
///     What a target ended with: an item or a mobile, a location, or nothing.
/// </summary>
public enum TargetResultType : byte
{
    Object,
    Location,
    Canceled
}
