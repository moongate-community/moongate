# 0xB9 — Support Features

<span class="mg-dir mg-dir-out">Server → Client</span>

Support features (0xB9): unlocks the client feature set at login, sent right before the character list. Moongate targets modern (7.x) clients only, so it always writes the extended 4-byte flags form.

- **Class:** [`SupportFeaturesPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/SupportFeaturesPacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Flags` | `FeatureFlagType` |
