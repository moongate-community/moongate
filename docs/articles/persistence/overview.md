# Persistence System

Moongate v2 uses a file-based persistence model with **snapshot + append-only journal**.

## Overview

The persistence layer provides:

- Full world checkpoint in one snapshot file
- Incremental operations in one journal file
- MessagePack-CSharp source-generated serialization for compact binary payloads
- Per-entry journal checksum validation
- Thread-safe repositories over shared in-memory state
- Typed snapshot storage for item combat/modifier state and mobile status/skill state

## Storage Structure

```
save/
├── world.snapshot.bin
└── world.journal.bin
```

Notes:

- There is currently **no** separate `world.journal.bin.checksum` file.
- There is currently **no** snapshot rotation folder (`save/snapshots/*`).
- File lock mode is supported via `PersistenceOptions.EnableFileLock` (default enabled).

## Runtime Flow

1. On startup, the server loads `world.snapshot.bin` if present.
2. Then it replays valid entries from `world.journal.bin` in sequence order.
3. During runtime, repository mutations append operations to the journal.
4. On autosave/shutdown, a fresh snapshot is written and journal is reset.
5. With file lock mode enabled, snapshot/journal handles remain open for process lifetime.

## Snapshot

The persisted snapshot model is `WorldSnapshot` in `Moongate.Persistence.Data.Persistence`.

Implemented behavior:

- Written by `MessagePackSnapshotService`
- Uses `MessagePackSerializer.Serialize(...)`
- Saved by rewriting the snapshot file on a lock-held stream.

## Journal

The journal is handled by `BinaryJournalService` and stores records as:

- `int32 payloadLength` (little-endian)
- `payload` (MessagePack serialized `JournalEntry`)
- `uint32 checksum` (little-endian, computed over payload)

On replay:

- Length is validated
- Payload and checksum are read
- Checksum mismatch stops replay at first invalid entry

## Autosave

Autosave is controlled by:

- `MoongatePersistenceConfig.SaveIntervalSeconds` (default: `30`)

The `PersistenceService` registers timer `db_save` and periodically calls snapshot save.

## Repositories

Current repositories:

- `IAccountRepository`
- `IMobileRepository`
- `IItemRepository`

They append journal entries on mutation and query from in-memory state.

## Domain Snapshot Notes

Current persistence is no longer just flat account/mobile/item records.

Important runtime-facing data now persisted in snapshots includes:

- mobile base stats and resources
- mobile aggregated equipment and runtime modifiers
- mobile modifier caps used by modern status packets
- persisted mobile skill tables used by skill window responses
- item combat stats and item modifiers used by equip logic, tooltips, and mobile aggregation

## What Is Not Implemented Yet

- Snapshot history retention/rotation
- Dedicated external checksum sidecar files
- Snapshot compression toggle in persistence config

---

**Previous**: [Persistence Repositories](repositories.md) | **Next**: [Data Format](format.md)
