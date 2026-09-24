![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence.Migrations

Driver-independent discovery and validation of versioned PostgreSQL SQL migrations for .NET 10.

```shell
dotnet add package Moongate.Persistence.Migrations
```

Core SQL lives in `migrations/auth` or `migrations/world`. Plugin bundles carry
`migrations/manifest.json` with a stable `id` and the same target directories.
Files use positive four-digit sequences: `0001_create_characters.sql`.

The catalog orders core first, then plugins by ID, then component sequence.
`MigrationHistory.Validate` rejects changes to applied SQL, missing files in
installed components, and newly inserted earlier sequences. SHA-256 checksums
normalize CRLF to LF and ignore a UTF-8 BOM. Removed plugin histories remain.

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Persistence.Migrations.Types.Migrations;

Console.WriteLine(MigrationTarget.World);
```

`MigrationCatalog.Load(directory, pluginsDirectory, target)` reads SQL without
loading plugin DLLs. `MigrationHistory.ReadAsync` uses a caller-owned
`DbCommand` factory; it never creates database objects or executes migrations.

Use the separate `Moongate.MigrationRunner` executable to apply reviewed SQL
with DbUp. See the [persistence guide](https://moongate.sh/server/persistence/).

Licensed under AGPL-3.0-or-later. See the
[source repository and license](https://github.com/moongate-community/moongate).
