# Chat

Player speech (say/emote/whisper/yell) and server-wide system broadcasts.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0xAD`](../incoming/0xad-unicode-ascii-speech-request.md) | Unicode Ascii Speech Request | C → S | Variable | The only speech packet a modern client (ClassicUO 7.x) sends — ASCII TalkRequest (0x03) is intentionally not implemented. |
| [`0xAE`](../outgoing/0xae-unicode-speech-message.md) | Unicode Speech Message | S → C | Variable | The only outgoing chat packet, sent to every recipient of a message including the speaker — never a reuse of the incoming packet, per real UO server behavior (confirmed across ModernUO, UOX3 and polserver). |
