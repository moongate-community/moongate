# Items & containers

World items, worn items, container gumps, contents and lift rejects.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x24`](../outgoing/0x24-draw-container.md) | Draw Container | S → C | 7 bytes (fixed) | Opens the container's gump on the client. |
| [`0x25`](../outgoing/0x25-add-item-to-container.md) | Add Item To Container | S → C | 21 bytes (fixed) | Drops one item into an already-open container gump. |
| [`0x27`](../outgoing/0x27-lift-reject.md) | Lift Reject | S → C | 2 bytes (fixed) | The lift the client asked for is refused, and why. |
| [`0x2E`](../outgoing/0x2e-worn-item.md) | Worn Item | S → C | 15 bytes (fixed) | Draws a single item on a mobile that the client already knows about. |
| [`0x3C`](../outgoing/0x3c-container-content.md) | Container Content | S → C | Variable | Every item inside a container, in one variable-length packet. |
| [`0xF3`](../outgoing/0xf3-world-item.md) | World Item | S → C | 24 bytes (fixed) | Draws an item lying in the world. |
