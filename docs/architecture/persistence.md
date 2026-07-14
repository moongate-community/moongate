# Persistence

Moongate's persistence boundary consists of three registered entity categories: accounts, mobiles, and items. Server services obtain typed stores from `IPersistenceService`; the implementation and on-disk format remain behind the SquidStd persistence registration.

## Entities and identity

`AccountEntity` stores credentials, activation and access level, and a list of mobile serials. `MobileEntity` stores the character attributes currently created by the login flow: identity, map and position, appearance, profession, statistics, skill values, and the equipped-item and backpack links. `ItemEntity` stores an item's graphic (`ItemId`), hue, amount, flip variants, and one of three mutually exclusive placements—in the world, inside a container, or equipped on a mobile—plus the serials of any items it contains. All three implement `ISerialIdEntity` and use the UO `Serial` value type; `ItemEntity` and `MobileEntity` also implement `IPositionEntity`.

`Serial` reserves zero for “no entity,” `0x00000001`–`0x3FFFFFFF` for mobiles, and `0x40000000`–`0x7FFFFFFF` for items. The persistence plugin registers accounts with `DefaultSerialGenerator`, whose first value is one, mobiles with `MobileSerialGenerator`, whose first value is `MinMobile`, and items with `ItemSerialGenerator`, whose first value is `MinItem`.

## Registration and save lifecycle

`MoongatePersistencePlugin.Configure` registers a `saves` directory, a MessagePack serializer, and persistence configured to use that directory. It then registers the account, mobile, and item stores with numeric type ids (`1`, `2`, `3`), names, schema version `1`, id accessors, and generators. These facts establish the configured boundary; they do not establish transactional, durability, or atomic-save guarantees.

The plugin also registers a seeder. It creates the default `admin` account, upserts it, logs the initial credentials, and requests `SaveSnapshotAsync`. Separately, `TimerAutostartService` registers the `persistence_save` timer at a 300-second interval; each callback requests another snapshot. The host-level persistence service owns what a snapshot does internally.

Account authentication and username lookup query the account store. Character creation upserts a new mobile first, then adds the allocated mobile serial to the account and upserts the account. It finally publishes `CharacterCreatedEvent`. If the account is absent, the mobile is still stored but no account link is created. These are separate store operations; the source does not present them as one transaction.

## Source map

### Runtime

- `src/Moongate.Core/Primitives/Serial.cs`
- `src/Moongate.Persistence/Entities/AccountEntity.cs`
- `src/Moongate.Persistence/Entities/MobileEntity.cs`
- `src/Moongate.Persistence/Entities/ItemEntity.cs`
- `src/Moongate.Persistence/Interfaces/ISerialIdEntity.cs`
- `src/Moongate.Persistence/Interfaces/IPositionEntity.cs`
- `src/Moongate.Persistence/Generators/DefaultSerialGenerator.cs`
- `src/Moongate.Persistence/Generators/MobileSerialGenerator.cs`
- `src/Moongate.Persistence/Generators/ItemSerialGenerator.cs`
- `src/Moongate.Persistence/MoongatePersistencePlugin.cs`
- `src/Moongate.Server/Services/Accounts/AccountService.cs`
- `src/Moongate.Server/Services/Accounts/CharacterService.cs`
- `src/Moongate.Server/Autostart/TimerAutostartService.cs`

### Tests

- `tests/Moongate.Tests/Core/Primitives/SerialTests.cs`
- `tests/Moongate.Tests/Server/CharacterServiceTests.cs`
