# Containers

`containers.toml` says how the client shows each kind of container:

```toml
[[container]]
name = "default"
gump = 0x003C
bounds = "(44, 65)..(186, 159)"
drop_sound = 0x0048
default = true
items = []

[[container]]
name = "bag"
gump = 0x003D
bounds = "(29, 34)..(137, 128)"
drop_sound = 0x0048
items = [0x0E76, 0x2256, 0x2257]
```

| Field | Meaning |
| --- | --- |
| `name` | A label for people reading the file and for logs. Optional. |
| `gump` | The id of the gump the client opens. |
| `bounds` | The area of the gump where items can be placed, a `Rectangle2D`. |
| `drop_sound` | The sound of an item dropped in. Leave it out for none. |
| `items` | The item ids (graphics) of the containers that use this entry. |
| `default` | `true` on the one entry used for containers not listed. |

`IContainerLayoutService` resolves these layouts. Starting-item and mobile
creation use their bounds to place items inside backpacks, and the container
window (`0x24`) opens with the layout's gump; see [Packets](../packets.md).

## Validation at startup

The server stops when:

- `containers.toml` does not exist;
- not exactly one entry has `default = true`;
- a `gump` is below 1, or `bounds` is smaller than 1x1;
- an item id is listed by two entries.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
