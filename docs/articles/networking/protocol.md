# Protocol Reference

Reference for current packet handling behavior in Moongate v2.

## Packet Framing

- First byte: opcode
- Fixed packet: total length from registry descriptor
- Variable packet: bytes `[1..2]` are big-endian length including header

## Parsing Rules

`NetworkService` enforces:

- descriptor must exist for opcode
- enough bytes must be buffered for full packet
- variable declared length must be within allowed bounds
- `TryParse` must succeed

On repeated violations, session is disconnected.

## Selected Inbound Packets

- `0xEF` Login Seed (`Length=21`, fixed)
- `0x80` Account Login (`Length=62`, fixed)
- `0xA0` Server Select (`Length=3`, fixed)
- `0x91` Game Login (`Length=65`, fixed)
- `0x5D` Login Character (`Length=73`, fixed)
- `0xBD` Client Version (`variable`)
- `0xF8` Character Creation (`Length=106`, fixed)
- `0x02` Move Request (`Length=7`, fixed)
- `0xC8` Client View Range (`Length=2`, fixed)
- `0x07` Pick Up (`Length=7`, fixed)
- `0x08` Drop Item (`Length=14`, fixed)
- `0x13` Drop -> Wear (`Length=10`, fixed)
- `0x09` Single Click (`Length=5`, fixed)
- `0x06` Double Click (`Length=5`, fixed)
- `0x34` Get Player Status (`Length=10`, fixed)
- `0x66` Book Pages (`variable`)
- `0x73` Ping Message (`Length=2`, fixed)
- `0xAD` Unicode Speech (`variable`)
- `0xB5` Open Chat Window (`Length=64`, fixed)
- `0x6C` Target Cursor Commands (`Length=19`, fixed)
- `0xBF` General Information (`variable`)
- `0xD6` Mega Cliloc (`variable`)

## Wired `0xBF` Subcommands

- `0x06` Party System
- `0x13` Request Popup Context Menu
- `0x14` Display Popup Context Menu (server -> client)
- `0x15` Popup Entry Selection
- `0x1A` Stat Lock Change
- `0x2C` Use Targeted Item
- `0x2D` Cast Targeted Spell
- `0x2E` Use Targeted Skill

## Selected Outbound Packets

- `0xA8` Server List (variable)
- `0x8C` Server Redirect (`Length=11`, fixed)
- `0x1B` Login Confirm (`Length=37`, fixed)
- `0xA9` Character / Starting Locations (variable)
- `0xB9` Support Features (`Length=5`, fixed)
- `0x55` Login Complete (`Length=1`, fixed)
- `0x22` Move Confirm (`Length=3`, fixed)
- `0x21` Move Deny (`Length=8`, fixed)
- `0x5B` Set Time (`Length=4`, fixed)
- `0xF2` Time Sync Response (`Length=13`, fixed)
- `0x78` Mobile Incoming (variable)
- `0x20` Draw Player (`Length=19`, fixed)
- `0x2E` Worn Item (`Length=15`, fixed)
- `0x24` Draw Container (`Length=9`, fixed)
- `0x3C` Add Multiple Items To Container (variable)
- `0x88` Paperdoll (`Length=66`, fixed)
- `0x11` Player Status (variable)
- `0x3A` Skill List (variable)
- `0xF3` Object Information (`Length=24`, fixed)
- `0x23` Dragging Of Item (`Length=26`, fixed)
- `0x76` Server Change (`Length=16`, fixed)
- `0xBC` Season (`Length=3`, fixed)
- `0x4F` Overall Light Level (`Length=2`, fixed)
- `0x4E` Personal Light Level (`Length=6`, fixed)
- `0x6D` Set Music (`Length=3`, fixed)
- `0x65` Set Weather (`Length=4`, fixed)
- `0x54` Play Sound Effect (`Length=12`, fixed)
- `0x70` Graphical Effect (`Length=28`, fixed)
- `0xC0` Hued Effect (`Length=36`, fixed)
- `0xC7` Particle Effect (`Length=49`, fixed)
- `0xAE` Unicode Speech Message (variable)
- `0xB0` Generic Gump (variable)
- `0xDD` Compressed Gump (variable)

Map transition notes:

- Map hue/map selection uses `0xBF` subcommand `0x08` (Set Cursor Hue / Set Map).
- On map changes, Moongate also sends `0x76` (Server Change) with target location + map dimensions.

## Opcode Constants

`PacketDefinition` is generated as a `partial` static class and used by bootstrap/handlers to avoid hardcoded byte literals.

## Notes

- Length/source metadata is defined in packet attributes and registration.
- Runtime listener availability is independent from packet registration: a packet can be parseable but still not be wired to gameplay flow.

## Current Status And Skill Flow

`0x34` is the shared client request for both status and skill window behavior.

- `GetPlayerStatusType.BasicStatus` -> server replies with `0x11` `Player Status`
- `GetPlayerStatusType.RequestSkills` -> server replies with `0x3A` full skill list including lock state

Moongate currently treats the outgoing `0x11` status packet as a modern `7.x`-style payload and serializes:

- hits / max hits
- effective str / dex / int
- stamina / mana
- carried gold aggregate
- effective physical / elemental resists
- luck
- carrying data
- follower counts
- weapon damage / tithing
- advanced modifier fields such as HCI, DCI, SSI, DI, LRC, SDI, FCR, FC, and LMC

## Current Book Flow

Moongate now supports both read-only and writable classic book flows.

- double-click opens the classic book UI for supported items
- `0x93` saves writable book `title` and `author`
- `0x66` is used for both:
  - page requests
  - writable page saves
- writable edits are accepted only when the book is:
  - equipped by the player, or
  - inside the player's backpack/container tree
- read-only books continue to serve rendered template content
- writable books persist `title`, `author`, and canonical newline content on the item
- book template files may force read-only or writable policy with `[ReadOnly] True|False`; when present, this overrides fallback item/startup `writable`

The book tooltip path uses the argument-style cliloc rule documented in [Cliloc Notes](cliloc-notes.md), avoiding the legacy `NEXT` tooltip artifact caused by generic string cliloc rotation.

---

**Previous**: [Packet System](packets.md)
