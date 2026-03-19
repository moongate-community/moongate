# Persistence System

Moongate v2 uses a file-based persistence model with **snapshot + append-only journal**.

## Overview

The persistence layer provides:

- Full world checkpoint in one snapshot file
- Incremental operations in one journal file
- MessagePack-CSharp source-generated serialization for compact binary payloads
- Per-entry journal checksum validation
- Thread-safe repositories over shared in-memory state
- Registry-driven entity descriptors with stable manual `TypeId` values
- Bucket-based snapshots so new persisted entity kinds do not require `WorldSnapshot` changes
- Generic journal records using `TypeId + Operation + Payload`

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
4. On autosave/shutdown, a fresh snapshot is captured from in-memory state.
5. The snapshot file write can run off the game loop on the background job service.
6. After a successful snapshot write, journal entries included in that snapshot are trimmed by sequence id instead of blindly resetting the whole file.
7. With file lock mode enabled, snapshot/journal handles remain open for process lifetime.

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

- `MoongatePersistenceConfig.SaveIntervalSeconds` (default: `300`)

The `PersistenceService` registers timer `db_save`.

Current behavior:

- the timer still fires on the game loop cadence
- the timer only schedules autosave work
- the expensive snapshot file write is dispatched via `IBackgroundJobService`
- overlapping autosaves are skipped while one is already in flight

## Repositories

Current repositories:

- `IAccountRepository`
- `IMobileRepository`
- `IItemRepository`
- `IBulletinBoardMessageRepository`
- `IHelpTicketRepository`

They append generic journal entries on mutation and query from in-memory state. Shared CRUD behavior is implemented once in a generic repository core, while domain repositories keep only domain-specific queries.

## Domain Snapshot Notes

Current persistence is no longer just flat account/mobile/item records.

Important runtime-facing data now persisted in snapshots includes:

- mobile base stats and resources
- mobile aggregated equipment and runtime modifiers
- mobile modifier caps used by modern status packets
- persisted mobile skill tables used by skill window responses
- item combat stats and item modifiers used by equip logic, tooltips, and mobile aggregation
- bulletin board posts/replies used by packet `0x71`

`BulletinBoardMessageEntity` is stored separately from `UOItemEntity`. The board item serial is the logical `BoardId`, while each post/reply has its own persisted `MessageId`, `ParentId`, owner character serial, posting timestamp, and body lines.

## What Is Not Implemented Yet

- Snapshot history retention/rotation
- Dedicated external checksum sidecar files
- Snapshot compression toggle in persistence config

---

**Previous**: [Persistence Repositories](repositories.md) | **Next**: [Data Format](format.md)
