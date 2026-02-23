using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Incoming.Login;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct SkillKeyValue(UOSkillName Skill, int Value);
