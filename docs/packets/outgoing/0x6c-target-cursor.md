# 0x6C — Target Cursor

<span class="mg-dir mg-dir-out">Server → Client</span>

Target cursor (0x6C, outgoing): raises the client's targeting cursor, or takes it down when <paramref name="CursorType" /> is `Cancel`. <para> The same opcode carries the answer back, as `TargetCursorResponsePacket`. They are two records rather than one because every other packet here has a single direction — the interfaces differ and the docs generator files pages by them. </para> <para> Only the selection type, the cursor id and the cursor type mean anything outbound; the fields the client fills in on the way back are written as zero. 19 bytes fixed. </para>

- **Class:** [`TargetCursorPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/TargetCursorPacket.cs)
- **Size:** 19 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `CursorId` | `uint` |
| `Selection` | `TargetSelectionType` |
| `CursorType` | `TargetCursorType` |
