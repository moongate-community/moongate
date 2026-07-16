# 0x3A — Skill Lock Change

<span class="mg-dir mg-dir-in">Client → Server</span>

Skill lock change (0x3A): the client sets the up/down/lock arrow on one skill. Variable length: 3-byte header + skill id (ushort) + lock (byte). A lock value the client should not have sent is clamped to `Up`. Shares its opcode with the outgoing skill list.

- **Class:** [`SkillLockChangePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/SkillLockChangePacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `SkillId` | `ushort` |
| `Lock` | `SkillLockType` |
