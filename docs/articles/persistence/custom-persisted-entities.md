# How to Add and Use a Custom Persisted Entity

This guide shows how to register and use a new persisted entity in the current Moongate persistence system.

The current model is intentionally simple:

- persisted runtime entities are serialized directly with `MemoryPack`
- there is no separate snapshot DTO layer
- there is no snapshot mapper layer
- snapshot buckets and journal records are routed by stable manual `TypeId` values

## When You Need This

You need a custom persisted entity when:

- you are adding a new core entity kind to the server
- you want a plugin or extension to persist its own entity type
- you want to use the generic repository path instead of wiring a new domain-specific repository first

## Persistence Model Summary

Every persisted entity kind needs:

1. A `MemoryPack`-serializable runtime entity
2. A stable `TypeId`
3. A `PersistenceEntityDescriptor<TEntity, TKey>`
4. A registration call on `IPersistenceEntityRegistry`

After that, the entity automatically participates in:

- full snapshot capture
- journal replay
- generic repository access through `IPersistenceUnitOfWork`

## Step 1: Make the Entity MemoryPackable

Persisted entities are runtime entities annotated directly for `MemoryPack`.

Use this pattern:

```csharp
using MemoryPack;

namespace MyPlugin.Data.Persistence;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MyCustomEntity
{
    [MemoryPackOrder(0)]
    public int Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public DateTime CreatedAtUtc { get; set; }

    [MemoryPackIgnore]
    public object? RuntimeOnlyCache { get; set; }
}
```

Rules:

- mark persisted members with explicit `MemoryPackOrder`
- mark runtime-only or derived members with `MemoryPackIgnore`
- keep the persisted shape stable once data exists on disk
- if you need to rebuild caches or invariants after deserialization, use `MemoryPackOnDeserialized`

## Step 2: Assign a Stable TypeId

Each persisted entity kind needs a unique and stable `TypeId`.

Core entity ids are defined in `PersistenceCoreEntityTypeIds`. For plugin or extension entities, define your own constants in a dedicated type and keep them stable across releases.

Example:

```csharp
namespace MyPlugin.Persistence;

public static class MyPluginPersistenceTypeIds
{
    public const ushort MyCustomEntity = 1000;
}
```

Rules:

- never reuse an existing `TypeId`
- never change the `TypeId` after data has been written
- use a readable `TypeName` alongside the numeric id for diagnostics

## Step 3: Create a Persistence Descriptor

The descriptor tells Moongate how to:

- identify the entity kind
- extract the key from the entity
- serialize and deserialize the entity key

Basic example:

```csharp
using Moongate.Persistence.Data.Persistence;

var descriptor = new PersistenceEntityDescriptor<MyCustomEntity, int>(
    MyPluginPersistenceTypeIds.MyCustomEntity,
    "my-custom-entity",
    1,
    static entity => entity.Id
);
```

If your key needs a custom serialized shape, pass explicit key codec delegates. For example, `Serial` can be stored through its `uint` representation:

```csharp
using MemoryPack;
using Moongate.UO.Data.Ids;

var descriptor = new PersistenceEntityDescriptor<MySerialEntity, Serial>(
    MyPluginPersistenceTypeIds.MySerialEntity,
    "my-serial-entity",
    1,
    static entity => entity.Id,
    static value => MemoryPackSerializer.Serialize((uint)value),
    static payload => (Serial)MemoryPackSerializer.Deserialize<uint>(payload)
);
```

## Step 4: Register the Descriptor

Register the descriptor on `IPersistenceEntityRegistry` before the registry is frozen.

Example:

```csharp
registry.Register(
    new PersistenceEntityDescriptor<MyCustomEntity, int>(
        MyPluginPersistenceTypeIds.MyCustomEntity,
        "my-custom-entity",
        1,
        static entity => entity.Id
    )
);
```

Important:

- the registry rejects duplicate `TypeId` values
- the registry rejects duplicate `(TEntity, TKey)` registrations
- once `Freeze()` is called, no more registrations are allowed

## Step 5: Pass the Registry into the Unit of Work

If you want to add custom entity kinds, build the registry first and pass it into `PersistenceUnitOfWork`.

Example:

```csharp
using Moongate.Persistence.Services.Persistence;

var registry = new PersistenceEntityRegistry();

// Register your custom descriptor.
registry.Register(
    new PersistenceEntityDescriptor<MyCustomEntity, int>(
        MyPluginPersistenceTypeIds.MyCustomEntity,
        "my-custom-entity",
        1,
        static entity => entity.Id
    )
);

var unitOfWork = new PersistenceUnitOfWork(options, registry);
```

`PersistenceUnitOfWork` automatically registers the built-in core descriptors and then freezes the registry during construction, so all custom registrations must be completed first.

## Step 6: Use the Generic Repository

Once the entity is registered, you can use it immediately through the generic repository path:

```csharp
var repository = unitOfWork.GetRepository<MyCustomEntity, int>();

await repository.UpsertAsync(
    new MyCustomEntity
    {
        Id = 1,
        Name = "Example",
        CreatedAtUtc = DateTime.UtcNow
    }
);

var entity = await repository.GetByIdAsync(1);
var count = await repository.CountAsync();
```

This is enough for many plugin or extension scenarios.

## Optional: Add a Domain-Specific Repository

If the entity needs domain-specific queries, add a dedicated repository interface and implementation on top of the shared base repository pattern.

Use a dedicated repository when you need methods such as:

- `GetByOwnerIdAsync(...)`
- `GetByStatusAsync(...)`
- `GetByCategoryAsync(...)`

If you only need basic CRUD and counting, the generic repository is usually enough.

## Full Minimal Example

```csharp
using MemoryPack;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Services.Persistence;

namespace MyPlugin.Persistence;

public static class MyPluginPersistenceTypeIds
{
    public const ushort MyCustomEntity = 1000;
}

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MyCustomEntity
{
    [MemoryPackOrder(0)]
    public int Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
}

var registry = new PersistenceEntityRegistry();
registry.Register(
    new PersistenceEntityDescriptor<MyCustomEntity, int>(
        MyPluginPersistenceTypeIds.MyCustomEntity,
        "my-custom-entity",
        1,
        static entity => entity.Id
    )
);

var unitOfWork = new PersistenceUnitOfWork(options, registry);
var repository = unitOfWork.GetRepository<MyCustomEntity, int>();

await repository.UpsertAsync(new MyCustomEntity { Id = 1, Name = "Example" });
```

## Common Mistakes

- Changing `MemoryPackOrder` for existing persisted members without a migration plan
- Persisting runtime caches or computed values instead of source-of-truth state
- Forgetting to register the descriptor before constructing `PersistenceUnitOfWork`
- Reusing a `TypeId` that is already assigned
- Persisting a key type without providing custom key codecs when needed
- Assuming a new entity automatically gets a domain-specific repository property on `IPersistenceUnitOfWork`

## Current Core Registration Location

Core entity descriptors are currently registered internally in:

- `src/Moongate.Persistence/Data/Internal/PersistenceCoreDescriptors.cs`

You do not need to call that helper directly when building a custom registry for `PersistenceUnitOfWork`. The unit of work applies the core registrations before freezing the registry.

The registry contract lives in:

- `src/Moongate.Persistence/Interfaces/Persistence/IPersistenceEntityRegistry.cs`

The default descriptor implementation lives in:

- `src/Moongate.Persistence/Data/Persistence/PersistenceEntityDescriptor.cs`

The generic repository entry point is:

- `IPersistenceUnitOfWork.GetRepository<TEntity, TKey>()`

---

**Previous**: [Persistence Repositories](repositories.md) | **Next**: [Persistence Overview](overview.md)
