# Cliloc Notes

This page documents the current state of `CommonClilocIds` in Moongate v2 and the level of confidence behind each value.

## Why This Exists

`POL` and `UOX3` do not expose a single central file equivalent to Moongate's `CommonClilocIds`.

Instead, they hardcode cliloc values inside tooltip, gump, and protocol code paths. Because of that, not every cliloc in Moongate can be treated as emulator-verified just because the numeric value looks plausible.

Moongate now separates clilocs into two categories:

- `Verified And In Active Use`
- `Provisional Or Not Verified Against POL Or UOX3`

The source of truth is:

- `src/Moongate.UO.Data/MegaCliloc/CommonClilocIds.cs`

## Verified Values

These are either:

- actively used by the current runtime, and
- verified against emulator references in this repository, or
- validated by packet behavior in Moongate tests and client behavior.

Examples:

- `ObjectName = 1042971`
- `ItemName = 1050039`
- `Weight = 1072788`
- `Blessed = 1038021`
- `Cursed = 1049643`
- `Insured = 1061682`

Notable emulator references found during audit:

- `UOX3/source/CPacketSend.cpp:7625` uses `1072788` for item weight.
- `POL/pol-core/pol/tooltips.cpp:121` uses `1050045` for prefix/name/suffix tooltip composition.
- `POL/pol-core/pol/tooltips.cpp:123` uses `1042971` for custom string-style tooltip entries.
- `UOX3/source/CPacketSend.cpp:7398` uses `1042971` for custom tooltip text.

## Provisional Values

Values in the provisional section are kept because they are currently useful or historically common, but they are not yet verified against emulator code or packet captures in this repository.

Examples:

- `Mana`
- `Stamina`
- `Strength`
- `Dexterity`
- `Intelligence`
- `Criminal`
- `Karma`
- `Fame`
- regeneration-related clilocs

Use these cautiously. If a tooltip looks wrong in client, verify the exact cliloc before reusing it elsewhere.

## Tooltip Rule Of Thumb

For Moongate item tooltips:

- single item name: use `CommonClilocIds.ObjectName`
- stacked item name: use `CommonClilocIds.ItemName`
- freeform custom tooltip text: prefer a cliloc explicitly designed for argument-only text, such as the `1042971` pattern used by `POL` and `UOX3`

Do not use `ObjectPropertyList.Add(string)` for canonical object names. That path rotates through generic argument-only clilocs and can produce incorrect localized client text.

### Book-Specific Note

Read-only books should follow the same argument-style custom text rule.

Using a generic rotating string path for book item tooltip text can produce client-localized artifacts such as `NEXT` instead of the intended object name. Moongate now uses the verified `1042971` custom-string pattern for book tooltip text so read-only books render like other custom-named objects.

## Future Cleanup

A value should move from provisional to verified only after at least one of these is true:

- confirmed in emulator source (`POL`, `UOX3`, `ModernUO`, `ServUO`)
- confirmed by packet capture against a known-good shard/client interaction
- confirmed by an automated Moongate regression test tied to real client behavior
