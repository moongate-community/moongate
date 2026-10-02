# sql_backup

Saves the world, then writes a SQL backup of the databases this server owns.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `sql_backup` | Yes | Yes | Administrator | Every role |

```text
sql_backup
```

In game, administrators use `.sql_backup`. The command works also when `sql_backup.enabled` is
false: that setting only controls the schedule.

It answers with one line for each file written, with its size:

```text
SQL backup started.
auth_20261002_113000.sql (1.2 MB)
world_20261002_113000.sql (48.3 MB)
```

A database that could not be written gives an error line, `SQL backup of world failed: <reason>.`;
the details are in the server log. While another backup runs, scheduled or requested, the command
answers `A SQL backup is already running.` and does nothing. Extra arguments print the usage.

A login-only server writes `auth`, a game-only server writes `world`. See
[Database backups](../persistence-operations.md#database-backups) for the files, the rotation and
the restore procedure.

## See also

- [All commands](../commands.md)
- [`save`](save.md)
