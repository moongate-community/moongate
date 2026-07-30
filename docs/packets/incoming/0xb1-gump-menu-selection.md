# 0xB1 — Gump Menu Selection

<span class="mg-dir mg-dir-in">Client → Server</span>

Gump response (0xB1): which button the player pressed, which switches were ticked, and what was typed into each text field. <para> This record only parses. Nothing here is trusted: whether the button was ever drawn, and whether the counts are plausible, is decided by the service that knows what it sent — this type has no way to tell a genuine answer from a fabricated one. </para>

- **Class:** [`GumpMenuSelectionPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/GumpMenuSelectionPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serial` | `uint` |
| `TypeId` | `int` |
| `ButtonId` | `int` |
| `Switches` | `IReadOnlyList<int>` |
| `TextEntries` | `IReadOnlyDictionary<int, string>` |
