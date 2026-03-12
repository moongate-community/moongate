# Data Format

This page documents the **actual** binary persistence format currently implemented in Moongate v2.

## Serialization Technology

- Serializer: `MessagePack-CSharp` (source-generated contracts)
- Snapshot container: `WorldSnapshot`
- Journal payload item: `JournalEntry`

## Snapshot File

Path:

- `save/world.snapshot.bin`

Write behavior:

1. Truncate snapshot stream (`SetLength(0)`)
2. Serialize `WorldSnapshot` directly into `world.snapshot.bin`
3. Flush stream

Read behavior:

- If snapshot stream is empty: returns `null`
- Otherwise: deserializes `WorldSnapshot` with MessagePack

There is no trailing checksum block in the snapshot file.

## Journal File

Path:

- `save/world.journal.bin`

Each record layout in the file:

```
[int32 payloadLength LE]
[byte[payloadLength] payload]
[uint32 checksum LE]
```

Where:

- `payload` is `MessagePackSerializer.Serialize(entry)`
- `checksum` is computed from `payload`

## Journal Validation Rules

During replay (`ReadAllAsync`):

- Record length must be present and valid
- Length must be in allowed bounds
- Payload must be fully readable
- Checksum must match
- Payload must deserialize to `JournalEntry`

Replay stops at first invalid/truncated record.

## Operational Lifecycle

- Repositories append operations to journal
- Unit of work builds fresh `WorldSnapshot` from state store
- Snapshot save completes
- Journal is reset (`FileMode.Create`)

## Persistence Options

`PersistenceOptions` currently includes:

- `SnapshotFilePath`
- `JournalFilePath`
- `EnableFileLock` (default: `true`)

No extra sidecar/checksum/history paths are currently configured.

## Current Entity Snapshot Shape

The world snapshot stores typed mobile and item substructures rather than only flat scalar entities.

`UOMobileEntity` currently persists:

- `BaseStats`
- `BaseResistances`
- `Resources`
- `EquipmentModifiers`
- `RuntimeModifiers`
- `ModifierCaps`
- `Skills`
- status-oriented scalars such as `StatCap`, `Followers`, `FollowersMax`, `Weight`, `MaxWeight`, `MinWeaponDamage`, `MaxWeaponDamage`, and `Tithing`

`Skills` are stored as explicit entries containing:

- `SkillId`
- `Value`
- `Base`
- `Cap`
- `Lock`

`UOItemEntity` currently persists:

- `CombatStats`
- `Modifiers`

This means snapshot payloads now preserve:

- item combat requirements and AoS-style item modifiers
- mobile aggregated equipment/runtime modifiers
- mobile skill tables used by `0x3A`
- mobile modern status data used by `0x11`

---

**Previous**: [Persistence Overview](overview.md) | **Next**: [Persistence Repositories](repositories.md)
