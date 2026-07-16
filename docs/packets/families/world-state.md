# World state

Light levels, game time, season, map change and map patches.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x4E`](../outgoing/0x4e-personal-light-level.md) | Personal Light Level | S → C | 6 bytes (fixed) | The light radiating around the given mobile. |
| [`0x4F`](../outgoing/0x4f-overall-light-level.md) | Overall Light Level | S → C | 2 bytes (fixed) | 0 is full daylight, higher is darker. |
| [`0x5B`](../outgoing/0x5b-game-time.md) | Game Time | S → C | 4 bytes (fixed) | The in-world clock shown to the client. |
| [`0xBC`](../outgoing/0xbc-season-change.md) | Season Change | S → C | 3 bytes (fixed) | Sets the client's season and optionally plays the season-change sound. |
| [`0xBF`](../outgoing/0xbf-map-change.md) | Map Change | S → C | 6 bytes (fixed) | Map (facet) change (0xBF sub-command 0x08): switches the client to the given map. |
| [`0xBF`](../outgoing/0xbf-map-patches.md) | Map Patches | S → C | 41 bytes (fixed) | Map patches (0xBF sub-command 0x18): declares the static/land map-diff block counts for the four classic facets. |
