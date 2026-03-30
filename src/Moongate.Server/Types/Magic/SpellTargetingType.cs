namespace Moongate.Server.Types.Magic;

/// <summary>
/// Describes how a spell resolves its target after the cast delay completes.
/// </summary>
public enum SpellTargetingType : byte
{
    None = 0,
    OptionalMobile = 1,
    RequiredMobile = 2,
    RequiredItem = 3,
    RequiredLocation = 4
}
