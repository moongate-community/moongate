# music

Shows the music where you stand, or plays a track to you.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `music [track]` | No | Yes | GameMaster | Game |

```text
.music
.music tavern04
```

In game only. Without a track it prints the music of where you stand: the region's, else the map's,
else `no_music`, such as `Music here: britain1.` With a `MusicType` name it plays that track to you
until your next region change brings another: `Playing tavern04.` Region music comes from
[`data/regions`](../data-files/regions.md) and [`maps.toml`](../data-files/maps.md).

## See also

- [All commands](../commands.md)
- [`weather`](weather.md)
