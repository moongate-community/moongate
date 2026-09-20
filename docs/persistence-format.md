# Migrating from binary persistence

This page preserves the former persistence-format route for existing links. The
snapshot/journal format and its synchronous APIs were removed when Moongate moved
to PostgreSQL. They are no longer a runtime backend, and the current server does
not import those files.

## Before upgrading

1. Stop the old server cleanly and keep its complete root directory unchanged.
2. Make an independent copy of every old save and world-save directory. Preserve
   the matching server, plugin binaries, configuration, and source revision.
3. Retain a runnable copy of the old version if the data must be inspected or
   exported. The PostgreSQL release cannot open or validate the binary files.
4. Design and test an application-specific migration. Recreate entities through
   the current attributed PostgreSQL models and asynchronous persistence APIs.
5. Import into new, isolated PostgreSQL databases and validate counts, identities,
   relationships, and gameplay meaning before changing deployment endpoints.

There is no generic binary importer because the old payload meaning depended on
the exact entity and serializer versions. Do not delete or rewrite old files after
an upgrade; they remain the rollback/evidence copy.

## API changes

The persistence service now uses registered `IPersistenceModule` ownership,
`IDataAccess<T>` asynchronous methods, and `ExecuteInTransactionAsync`. File
collections, checkpoints, binary restore helpers, synchronous reads, and managed
world-save backup generations are gone.

Replace synchronous calls with awaited operations and review their new semantics:

| Removed pattern | PostgreSQL replacement |
| --- | --- |
| Named binary collection | Attributed entity owned by one `IPersistenceModule` |
| Synchronous `GetById` / `GetAll` | `GetByIdAsync` / `GetAllAsync` |
| In-memory predicate over a snapshot | SQL-translated `QueryAsync` expression |
| Per-row writes assumed to be grouped | `ExecuteInTransactionAsync` for one target |
| Checkpoint or binary backup generation | Operator-managed PostgreSQL backup policy |
| Binary restore helper | Operator-managed PostgreSQL recovery process |

Reads are detached, writes are last-writer-wins, and a transaction never spans
Accounts and Realm. Missing entities in a live snapshot remain in PostgreSQL;
deletion is explicit.

## Operating the new schema

Keep `auto_sync_schema = false` in deployments. Use the schema preview/apply CLI
with a separately authorized schema role, review FreeSql's generated DDL, and use
versioned SQL for semantic data transformations. `OldName` can express supported
renames, but schema comparison cannot infer all rename, conversion, or downgrade
intent.

The complete model, connection, migration, world-save, and recovery contract is
in [PostgreSQL persistence and world saves](persistence.md).
