namespace Moongate.Network.Types;

/// <summary>
/// Functional family a packet belongs to, used to group the generated packet
/// documentation (docs/packets). Every packet record must declare one via
/// <c>[PacketDocumentation]</c>; the docs generator fails when one is missing.
/// </summary>
public enum PacketFamilyType : byte
{
    LoginShardSelect,
    Characters,
    EnterWorld,
    WorldState,
    Movement,
    StatusSkills,
    ItemsContainers,
    InteractionKeepalive,
    Tooltips,
    Chat
}
