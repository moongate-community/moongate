# 0x13 — Drop Wear Item

<span class="mg-dir mg-dir-in">Client → Server</span>

Drop-wear item (0x13): the client dropped what it was holding onto a paperdoll. `Mobile` is who to put it on — normally the player themselves. 10 bytes fixed. <para> `Layer` is where the client believes the item goes, and the server does not take its word for it: which layer an item occupies is a property of the item. POL validates this byte and then assigns the item's own tile layer over it (eqpitem.cpp), for the same reason — honouring the request would let a client wear a dagger as a pair of boots. </para>

- **Class:** [`DropWearItemPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/DropWearItemPacket.cs)
- **Size:** 10 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Layer` | `byte` |
| `Mobile` | `Serial` |
