# Maps

A map item opens in the client on the part of the world it shows, with the course of pins plotted on it. Cartography,
treasure maps and SOS bottles will draw their maps on top of this.

## Open a map

Double click a map in your backpack, or on the ground within 2 tiles; farther away it says "That is too far away.".
The client draws the land itself: the server tells it only the corners of the area, the size of the drawing and the
facet.

The 33 preset maps (the small and large world, Britain, Minoc, Britain to Trinsic, the worlds of Ilshenar, Malas, Tokuno
and Ter Mur, and the others) open on the area UOX3 gives them. A blank map, or a crafted map not drawn yet, has no area:
"It appears to be blank.". A map of Felucca opened in Trammel is drawn as Trammel, the same land.

## Plot a course

1. Open the map and press the lock in its corner: the course may now be changed.
2. Click the drawing to add a pin, drag a pin to move it, or use the buttons to remove pins or clear the course.
3. Press the lock again when the course is done.

A course holds at most 50 pins and is kept with the map. Only a map in your backpack or within 2 tiles can be changed,
and never one a script protected, one held on the cursor, or by a ghost. Clients older than 7.0.13, and a client that
has not told its version yet, show maps of Felucca and Trammel only; a map of another facet tells them so.

## For scripts

The `map` Lua module opens maps and sets their area and course:

```lua
-- A map of the 400 tiles around the player, with a pin where it stands.
local here = mobile.location(user)
map.set_bounds(serial, here.x - 200, here.y - 200, here.x + 200, here.y + 200, 200, 200, here.map)
map.add_world_pin(serial, here.x, here.y)
map.display(user, serial)
```

A map keeps its data in item props: `map.x1`, `map.y1`, `map.x2`, `map.y2`, `map.width`, `map.height`, `map.facet`,
`map.pins` (pixels of the drawing, `x,y;x,y`), `map.editable` and `map.protected`. A preset map without them takes
its area from the tags of its template, `map_x1` to `map_facet`. The course keeps its pixels when the area changes:
clear it or set it again.

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution
`scripts/items/map_item.lua` and `templates/items/skills/misc/maps.toml`, or give `script_id = "map_item"` and the
`map_*` tags to your map templates.

## Not yet

Cartography, treasure maps, SOS bottles and indecipherable maps. Inserting and removing pins has not been tried with a
client yet.

## See also

- [Shipped scripts](scripting/shipped-scripts.md)
- [Packets and handlers](packets.md)
