# 0x29 — Drop Item Approved

<span class="mg-dir mg-dir-out">Server → Client</span>

Drop item approved (0x29): the drop the client asked for went through. Opcode only, 1 byte — the client already knows what it dropped and where. A refused drop gets 0x27 instead.

- **Class:** [`DropItemApprovedPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/DropItemApprovedPacket.cs)
- **Size:** 1 bytes (fixed)

## Fields

This packet carries no fields beyond its opcode.
