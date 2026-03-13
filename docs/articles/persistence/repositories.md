# Persistence Repositories

This page documents the repository APIs currently implemented in `Moongate.Persistence`.

## Repository Interfaces

## `IAccountRepository`

Supports:

- `AddAsync(UOAccountEntity)`
- `UpsertAsync(UOAccountEntity)`
- `RemoveAsync(Serial)`
- `GetByIdAsync(Serial)`
- `GetByUsernameAsync(string)`
- `GetAllAsync()`
- `CountAsync()`
- `ExistsAsync(predicate)`
- `QueryAsync(predicate, selector)`

## `IMobileRepository`

Supports:

- `UpsertAsync(UOMobileEntity)`
- `RemoveAsync(Serial)`
- `GetByIdAsync(Serial)`
- `GetAllAsync()`
- `CountAsync()`
- `QueryAsync(predicate, selector)`

## `IItemRepository`

Supports:

- `UpsertAsync(UOItemEntity)`
- `RemoveAsync(Serial)`
- `GetByIdAsync(Serial)`
- `GetAllAsync()`
- `CountAsync()`
- `QueryAsync(predicate, selector)`

## `IBulletinBoardMessageRepository`

Supports:

- `UpsertAsync(BulletinBoardMessageEntity)`
- `RemoveAsync(Serial messageId)`
- `GetByIdAsync(Serial messageId)`
- `GetByBoardIdAsync(Serial boardId)`
- `GetAllAsync()`

Notes:

- `BoardId` is the bulletin-board item serial
- `MessageId` is the persisted serial-like identifier for a post/reply
- replies are linked through `ParentId`
- owner checks use `OwnerCharacterId`

## Unit Of Work

`IPersistenceUnitOfWork` exposes:

- repositories (`Accounts`, `Mobiles`, `Items`, `BulletinBoardMessages`)
- id allocation (`AllocateNextAccountId`, `AllocateNextMobileId`, `AllocateNextItemId`)
- lifecycle (`InitializeAsync`, `SaveSnapshotAsync`)

## Runtime Behavior

- Repositories operate against in-memory `PersistenceStateStore`.
- Mutations append journal entries through `BinaryJournalService`.
- `PersistenceUnitOfWork.InitializeAsync` loads snapshot then replays journal.
- `SaveSnapshotAsync` writes full snapshot and resets journal.

## Thread Safety

Repository operations synchronize through state-store locking to ensure consistency of:

- entity dictionaries
- username index
- last-id and sequence counters

---

**Previous**: [Data Format](format.md) | **Next**: [Persistence Overview](overview.md)
