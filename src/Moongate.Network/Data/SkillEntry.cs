using Moongate.UO.Data.Types;

namespace Moongate.Network.Data;

/// <summary>
/// One row of the skill list (0x3A). <paramref name="Value" /> is the effective value and
/// <paramref name="Base" /> the unmodified one — both in tenths, as is <paramref name="Cap" />
/// (1000 = 100.0). <paramref name="SkillId" /> is the internal, zero-based id; the packet writes it
/// one-based.
/// </summary>
public readonly record struct SkillEntry(ushort SkillId, ushort Value, ushort Base, SkillLockType Lock, ushort Cap);
