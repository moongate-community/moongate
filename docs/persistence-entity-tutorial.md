# Create a persistent entity

This tutorial creates a `CharacterProfile`, stores it in PostgreSQL, and reads,
updates, queries, and deletes it through `IDataAccess<CharacterProfile>`. Moongate
manages the persistence modules internally.

The runnable example uses the current Moongate source checkout and a standalone
console application. The last step shows how to register the same entity in a
Moongate plugin.

## 1. Create the example project

You need the .NET 10 SDK, a Moongate checkout, and an empty PostgreSQL database.
The example's database role must be allowed to create schemas, tables and sequences.
Create the database first: Moongate creates the entity schema and table, not the
database itself.

From the Moongate repository root, create a sibling project and reference the
persistence library:

```sh
dotnet new console --framework net10.0 --name EntityTutorial --output ../Moongate.EntityTutorial
dotnet add ../Moongate.EntityTutorial/EntityTutorial.csproj reference src/Moongate.Persistence/Moongate.Persistence.csproj
```

The project reference keeps the tutorial on the same API version as your checkout.
It also supplies the FreeSql attributes, DryIoc container, and Moongate Core types
used below.

## 2. Define the entity

Create `CharacterProfile.cs` in the new project:

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace EntityTutorial;

[Table(Name = "tutorial_entities.character_profiles")]
public sealed class CharacterProfile : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";

    [Column(Name = "level")]
    public int Level { get; set; } = 1;
}
```

| Declaration | Purpose |
| --- | --- |
| `IMoongateEntity` | Gives persistence a common `Serial Id` identity. |
| `Table(Name = "tutorial_entities.character_profiles")` | Selects the PostgreSQL schema and table. Use explicit lowercase names. |
| `IsPrimary = true, MapType = typeof(long)` | Stores the `Serial` as a PostgreSQL `bigint` primary key. |
| `Column(Name = ...)` | Keeps database column names stable when C# names change. |
| `StringLength = 100` | Declares the maximum stored name length. |

Leave `Id` at its default (`Serial.Zero`) for a new entity. `UpsertAsync` reserves
an ID from the table's PostgreSQL sequence, inserts the entity and writes the ID
back to the same object. No sequence name or extra service dependency is needed.
Keep a public `Id` setter and do not use `IsIdentity = true`. An explicitly supplied
nonzero ID is preserved and uses the normal insert-or-update behavior.

Column names default to lowercase snake_case: `Username` maps to `username`,
`HashPassword` to `hash_password`, and `CreatedAt` to `created_at`. Explicit
`[Column(Name = "...")]` mappings take precedence and must also use lowercase
snake_case. Keep the schema-qualified `[Table(Name = "...")]` and identity
mapping explicit.

Keep the initial model scalar. For a property that must stay in memory, use
`[Column(IsIgnore = true)]`. Complex properties need an explicit supported mapping
or omission; an arbitrary object graph is not automatically serialized.

## 3. Register the entity and use its data access

Replace the generated `Program.cs` with this complete program:

```csharp
using DryIoc;
using EntityTutorial;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

var connectionString = Environment.GetEnvironmentVariable("MOONGATE_TUTORIAL_DATABASE")
    ?? throw new InvalidOperationException("Set MOONGATE_TUTORIAL_DATABASE before running the tutorial.");

var options = new PostgreSqlPersistenceOptions(
    [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, connectionString)],
    autoSynchronizeSchema: true);

using var container = new Container();
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<CharacterProfile>();

await using var persistence = container.Resolve<MoongatePersistenceService>();
await persistence.InitializeAsync();

var profiles = container.Resolve<IDataAccess<CharacterProfile>>();
var profile = new CharacterProfile
{
    Name = "Mario",
    Level = 1
};

await profiles.UpsertAsync(profile); // profile.Id is now assigned.

var loaded = await profiles.GetByIdAsync(profile.Id)
    ?? throw new InvalidOperationException("The saved profile was not found.");
Console.WriteLine($"Created: {loaded.Name}, level {loaded.Level}");

loaded.Level = 2;
await profiles.UpsertAsync(loaded);

var updated = await profiles.GetByIdAsync(profile.Id)
    ?? throw new InvalidOperationException("The updated profile was not found.");
Console.WriteLine($"Updated: {updated.Name}, level {updated.Level}");

var page = await profiles.QueryAsync(value => value.Level >= 2, skip: 0, take: 20);
Console.WriteLine($"Profiles at level 2 or higher: {page.Count}");

var deleted = await profiles.DeleteAsync(profile.Id);
Console.WriteLine($"Deleted: {deleted}");
Console.WriteLine($"Missing after delete: {await profiles.GetByIdAsync(profile.Id) is null}");
```

`AddPersistenceWorld<CharacterProfile>()` selects the Realm database. Moongate
groups the registered entities by target and schema into internal modules; you do
not need an `IPersistenceModule` class. Register everything before initialization
or schema preview, because schema preparation freezes the registration batch.

This standalone example enables schema synchronization explicitly so
`InitializeAsync()` creates the tutorial table. Use this setting for the empty
development database in this exercise. Normal Moongate deployments default to
schema synchronization being disabled and use the review/apply workflow in step 5.

## 4. Run and check the result

Set `MOONGATE_TUTORIAL_DATABASE` through your environment or secret provider to the
connection URI for the empty database. Its shape is:

```text
postgres://USER:PASSWORD@HOST:5432/moongate_tutorial
```

Replace the placeholders with your connection details and percent-encode reserved
characters in credentials, such as `@` as `%40`. Keep actual credentials out of
source files. See [connection configuration](persistence-operations.md#connections)
for URI options and environment expansion in server TOML.

From the repository root, run:

```sh
dotnet run --project ../Moongate.EntityTutorial/EntityTutorial.csproj
```

The program prints:

```text
Created: Mario, level 1
Updated: Mario, level 2
Profiles at level 2 or higher: 1
Deleted: True
Missing after delete: True
```

The table remains in PostgreSQL; the example deletes only its profile row at the
end. The operations illustrate the persistence contract:

- `UpsertAsync` inserts a missing ID or updates the row with that ID. Independent
  writes commit before their returned task completes.
- Reads return detached values. `loaded.Level = 2` changes only that object;
  the second `UpsertAsync` persists the change.
- `QueryAsync` translates its predicate to SQL. The paged overload limits the
  result and orders it by identity; unsupported expressions fail explicitly.
- `DeleteAsync` removes a row and returns whether it existed. A subsequent
  `GetByIdAsync` returns `null`.

Use [transactions](persistence.md#reads-writes-and-transactions) when several writes
to the same target must commit together. A transaction cannot span Accounts and Realm.

## 5. Use the entity in a Moongate plugin

Move the entity into your plugin's `Data` directory and update its namespace.
Reference the matching `Moongate.Persistence` version as described in
[Writing a plugin](plugins.md#creating-the-project).

Inside your existing plugin's `Register(Container container)` method, register
the entity using `Moongate.Persistence.Extensions`:

```csharp
public void Register(Container container)
{
    container.AddPersistenceWorld<CharacterProfile>();
}
```

The host already owns persistence registration, initialization, and disposal.
Your plugin registers entity types and lets its services receive
`IDataAccess<CharacterProfile>` through constructor injection. Do not run the
console example's initialization or database operations inside `Register`.

Choose the helper according to the data's owner:

| Helper | Database and intended data |
| --- | --- |
| `AddPersistenceWorld<TEntity>()` | This realm's database: characters, items, and world state. |
| `AddPersistenceAuth<TEntity>()` | The shared Accounts database: account and authentication data. |

Register a type once, in one target per container. The helper chooses the database;
the table attribute chooses the schema within that database. These helpers route
database access in the current process; they do not implement communication
between login and game servers.

For the world entity above, set the realm connection in the host's
`config/moongate.toml`:

```toml
[persistence]
auto_sync_schema = false

[persistence.realm]
connection_string = "$MOONGATE_REALM_DATABASE"
```

Set that variable to the realm's PostgreSQL URI. A `game` host checks only its
Realm database; `standalone` also needs `[persistence.accounts]`. The defaults are
local `auth` and `world` databases using `moongate` / `moongate`. Schema preview/generate commands only
connect to the targets needed for their mappings. In deployment, keep auto-sync disabled and ship a
versioned SQL migration with the plugin.

1. Deploy the plugin to a reference root whose database has the previous schema.
2. Generate a draft for World, choosing the next component sequence:

   ```sh
   Moongate.Server --root-directory /srv/moongate/reference \
     --persistence-schema generate --migration-target world \
     --migration-output ./MyPlugin/migrations/world/0001_create_characters.sql
   ```

3. Review the SQL and add `MyPlugin/migrations/manifest.json`:

   ```json
   { "id": "my-plugin" }
   ```

4. Include `migrations/**/*` in the plugin's published output and commit it with
   the entity. Stop the target realm and apply using a schema-role connection:

   ```sh
   ./migration-runner/Moongate.MigrationRunner apply \
     --root-directory /srv/moongate/realm-1 --target world
   ```

5. Start the server with its runtime connection. It verifies both migration
   history and the mapped schema before starting services.

**Where are the files?** Core SQL is in `migrations/auth` and `migrations/world`
beside the server. Plugin SQL is in each bundle's `migrations/` directory. The
runner records successful files and checksums in `moongate_migrations.history`.
Never edit an applied file: add a new numbered migration. Generate against the
previous schema, not an already updated database. The draft includes all registered
modules for the target, so use an isolated plugin reference root and review ownership.

The earlier console example deliberately uses automatic synchronization for a
throwaway database. That convenience does not create SQL files or version history.
Use the [schema operations guide](persistence-migrations.md#generate-review-and-apply) for
production-style deployment, reference databases, roles and failure handling.

Simple entity registration enables explicit reads and writes. For live objects
kept by the game loop, the source-and-snapshot overload of `AddPersistenceWorld`
also enrolls them in `SaveAllAsync` and host world saves. Follow
[Live world snapshots](persistence.md#live-world-snapshots) to register a detached
clone and capture it through the owning loop. Removing an object from memory does
not delete its row; deletion remains explicit.

## 6. Generate migrations automatically while developing

Once your entity is registered with `AddPersistenceAuth<TEntity>()` or
`AddPersistenceWorld<TEntity>()`, enable the development workflow:

```toml
[persistence]
auto_sync_schema = false
auto_generate_migrations = true
migrations_directory = "${MOONGATE_ROOT}/migrations"
```

Set `MOONGATE_ROOT` to your server data root, or use an absolute source directory.
The shipped core auth migrations (`0001` to `0003`) must already be applied, which
is why the generated files below start at `0004`. For a new custom entity
registered with `AddPersistenceAuth<CustomAuthEntity>()`:

1. Start the server with the new entity registered. Startup writes
   `migrations/auth/0004_auto_schema.sql`, applies it and records its checksum.
2. Stop the server and add `public DateTime? LastLoginAt { get; set; }` to the entity.
3. Start again. Startup writes and applies `0005_auto_schema.sql`; existing rows
   receive a null `last_login_at` value.
4. Restart without changing the entity: no new migration is generated.
5. Commit both generated SQL files and the entity code. Numbers always continue
   after the highest existing migration in the component.

Indexes declared with `[Index(...)]` are automatic for a newly created table when
they use plain columns, including unique and composite indexes. Adding an index
to an existing table requires review: existing data may violate a unique index.

A change that needs review leaves a marked SQL draft and stops startup. Review the
unapplied file and remove `-- moongate:review-required` before restarting. Existing
applied files are immutable. Leave the development flags off in deployment and use
the standalone migration runner for the reviewed files.

See [automatic development migrations](persistence-migrations.md#automatic-development-migrations)
for plugin directories, required-column defaults, existing database baselines and
failure recovery.
