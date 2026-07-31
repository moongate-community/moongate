# What is Moongate

Moongate is an open-source Ultima Online server written in C# on
modern .NET, built around one idea: **keep it simple**. A single game loop, a
small set of services, and a shard surface made of [Lua scripts](../scripting/index.md)
and [YAML data files](../scripting/data/item-templates.md) — you build a shard
without touching C#.

## Modern clients only

Moongate targets **ClassicUO 7.x** clients exclusively. There is no
expansion-era feature gating and no legacy client support: the server always
advertises a constant, modern feature set. If you can run ClassicUO, you can
connect.

## Project status

Moongate is young and moves fast. What works today:

- TCP login pipeline: seed, account login, server list, game-server handoff,
  character list, **character creation and deletion** (the created player
  mobile is persisted) and **world entry** — creating or selecting a character
  loads you into the map with its stats, skills and gear, and with the mobiles
  and items already around you. Movement is handled: validated against the map,
  rate-limited, and **the world appears and disappears as you walk** — things
  are drawn when they come into view, undrawn when they leave, and the same
  happens to you on everyone else's screen when they move.
- Item, mobile and loot systems driven by YAML templates, with a Lua API
  (`item`, `mobile`, `loot`, `chat`, `gump`, `target`, `ai`, `memory`, `account`,
  `game`, `events`, `log`) for shard logic.
- **Gumps**: server-drawn dialogs a player can act on — buttons, checkboxes,
  text fields — described in Lua and answered through a callback. Open to
  plugins as well as scripts.
- **Target cursor**: ask the player to click an entity or a spot on the ground
  and act on what they picked, from Lua or from a command. `.where` ships as a
  worked example.
- Binary snapshot persistence for accounts, mobiles and items.
- UO client file loading (art, maps, and friends) from a ClassicUO-compatible
  installation.

The documentation only describes what is implemented — if a page says it
works, it works in the current `main`.

## Where to go next

1. [Install & first launch](install-and-first-launch.md) — build the server
   and let it create its runtime directory.
2. [Connect with ClassicUO](connect-with-classicuo.md) — point a client at it.
3. [Configuration](configuration.md) — everything `moongate.yaml` can do.
4. [Scripting](../scripting/index.md) — where shard building actually happens.
