# 0x6C — Target Cursor Response

<span class="mg-dir mg-dir-in">Client → Server</span>

Target cursor response (0x6C, incoming): what the player clicked. <para> This record only reports. Whether the answer belongs to a cursor the server actually raised, and whether "nothing picked" means the player cancelled, is decided by the service that owns the pending request — the packet cannot tell. </para>

- **Class:** [`TargetCursorResponsePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/TargetCursorResponsePacket.cs)
- **Size:** 19 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `CursorId` | `uint` |
| `Selection` | `TargetSelectionType` |
| `Clicked` | `Serial` |
| `Location` | `Point3D` |
| `Graphic` | `ushort` |
