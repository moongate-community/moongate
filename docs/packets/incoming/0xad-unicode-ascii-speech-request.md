# 0xAD — Unicode Ascii Speech Request

<span class="mg-dir mg-dir-in">Client → Server</span>

Unicode speech request (0xAD): the only speech packet a modern client (ClassicUO 7.x) sends — ASCII TalkRequest (0x03) is intentionally not implemented. `IsEncoded` is true for classic-client "encoded" speech (12-bit-packed keyword triggers for NPC menus); its payload has no fixed size without decoding it, so `Read` stops at the type byte and returns an empty result rather than guessing where the text starts. No NPC system exists yet to consume keywords anyway.

- **Class:** [`UnicodeSpeechPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/UnicodeSpeechPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `IsEncoded` | `bool` |
| `Type` | `ChatMessageType` |
| `Hue` | `Hue` |
| `Text` | `string` |
