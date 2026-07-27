# 0xAE — Unicode Speech Message

<span class="mg-dir mg-dir-out">Server → Client</span>

Unicode speech message (0xAE): the only outgoing chat packet, sent to every recipient of a message including the speaker — never a reuse of the incoming packet, per real UO server behavior (confirmed across ModernUO, UOX3 and polserver). `Speaker` is `Zero` for system/broadcast messages, matching this codebase's existing "zero is no entity" convention on `Serial`.

- **Class:** [`UnicodeSpeechMessagePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/UnicodeSpeechMessagePacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Speaker` | `Serial` |
| `Body` | `ushort` |
| `Type` | `ChatMessageType` |
| `Hue` | `Hue` |
| `SpeakerName` | `string` |
| `Text` | `string` |
