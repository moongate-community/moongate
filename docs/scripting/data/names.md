# Name pools

`names.yaml` holds the pools of candidate names an NPC draws from at spawn. It
ships under `src/Moongate.Server/Assets/names.yaml` and is seeded into the
runtime `data/` directory on first boot.

| Concern | Type |
|---|---|
| DTO | `NameList` (`Moongate.UO.Data.Names`) |
| Loader | `Moongate.Server.Loaders.NamesLoader` (priority 30) |
| Registry | `Moongate.Server.Abstractions.Interfaces.Mobiles.INameService` |

Pools load at priority 30 and mobile templates at 150, so a template's
[`NamePool`](mobile-templates.md#namepool) is checked against this file while
the server is still starting. Naming a pool that does not exist is a load error.

## File shape

A top-level list of pools, each a `Type` and its `Names`:

```yaml
-   Type: male
    Names:
        - Aaron
        - Abbott
        - Abdel
-   Type: tokuno male
    Names:
        - Dosyaku
        - Warimoto
```

`Type` is matched case-insensitively. Registering a pool whose type already
exists replaces it.

## Shipped pools

Twenty-eight, ported from ModernUO's `Data/names.json`:

`ancient lich`, `balron`, `bird`, `centaur`, `daemon`, `darknight creeper`,
`demon knight`, `ethereal warrior`, `evil mage`, `evil mage lord`, `female`,
`female elf brigand`, `fire gargoyle`, `gargoyle vendor`, `golem controller`,
`impaler`, `lizardman`, `male`, `male elf brigand`, `orc`, `pixie`, `ratman`,
`savage`, `savage rider`, `savage shaman`, `shadow knight`, `tokuno female`,
`tokuno male`

> [!WARNING]
> **Gender is not a suffix.** The six pools that name a gender put it in three
> different places — bare in `male` and `female`, trailing in `tokuno male`,
> and **leading** in `male elf brigand`. A template must name the pool it wants
> in full; nothing is composed for it. ModernUO does the same, writing each
> pool out at the call site rather than normalising the names.

## Adding a pool

Append a `Type`/`Names` pair. Nothing else needs changing: a template can name
it the moment it exists, and the load-time check will accept it.
