# ModernUO NPC Converter

Python tool to convert NPC definitions from ModernUO C# source files to Moongate JSON templates.

This converter is part of the ModernUO import pipeline:

1. `python3 scripts/generate_loot_templates.py /path/to/ModernUO .`
2. `python3 scripts/modernuo_npc_converter.py --all --source /path/to/ModernUO --output moongate_data`

The first step refreshes shared `loot_pack.*` tables, treasure/fillable loot, `loot_support.json`, and reagent tagging.
The second step regenerates mobiles plus creature-specific `creature.*` loot files.

## Usage

```
python3 scripts/modernuo_npc_converter.py --all \
  --source /path/to/ModernUO \
  --output moongate_data
```

## Options

- `--monsters` — Convert monster NPCs
- `--animals` — Convert animal NPCs
- `--vendors` — Convert vendor NPCs
- `--town-npcs` — Convert town NPCs
- `--all` — Convert all categories
- `--dry-run` — Parse and map without writing files
- `--source PATH` — Path to ModernUO repo
- `--output PATH` — Path to Moongate data directory

## Output

- `templates/mobiles/{category}/*.json` — Mobile templates
- `templates/loot/creatures/*.json` — NPC-specific loot
- `templates/sell_profiles/*.json` — Vendor sell profiles (placeholders)

When writing creature loot files, the generator also removes stale `templates/loot/creatures/*.json` files that are no longer produced by the current parse result.

## Post-conversion review

Generated templates need manual review for:
- Complex outfit logic that falls outside the supported ModernUO constructor and `InitOutfit` patterns
- Sell profile items (placeholders only, need manual population)
- Sound IDs (estimated from `BaseSoundID` offset pattern)

The converter now emits canonical mobile `variants[]` entries with `appearance` and spawn-time `equipment` for the supported ModernUO patterns it can parse automatically.
This includes inherited `InitOutfit()` chains, gender-specific `if (Female)` branches, and simple wrapper calls such as `ApplyHue(new Robe(), 0x47E)` when the underlying item constructor can be resolved.

Creature-specific loot generation also normalizes several ModernUO helpers into Moongate-compatible references:
- `PackReg(...)` and `Loot.Random...Reagent()` become `itemTag: "reagents"`
- `new RandomTalisman()` becomes `itemTag: "talismans"`
- `Seed`, `MetalChest`, and `GargoylesPickaxe` resolve through generated support item templates
- commented `AddLoot(...)` and `PackItem(...)` lines are ignored
