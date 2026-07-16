# 0xBF — General Information

<span class="mg-dir mg-dir-in">Client → Server</span>

General information (0xBF): a multiplexed request whose meaning is chosen by a leading `SubCommand` (ushort). Variable length: 5-byte header (id + length + sub-command) followed by the sub-command payload, which is carried verbatim in `Payload`.

- **Class:** [`GeneralInformationPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/GeneralInformationPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `SubCommand` | `ushort` |
| `Payload` | `byte[]` |
