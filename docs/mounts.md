# Mounts

A player rides a tamed horse, llama or ostard: it walks and runs twice as fast. This is the first slice of the
mounts; the stable, the bola and the ethereal mounts are not built yet.

## How to ride

1. Get a creature that is yours. A game master gives it with [`tame`](commands/tame.md); the skill of taming does
   not exist yet.
2. Stand within one tile of it and double click it. Its owner, or a game master, sits on it.
3. Walk or run. A rider takes a step every 200 ms walking and every 100 ms running, a rider on foot every 400 and 200.
4. Double click yourself to get off, or die: the horse stands again where you were.

The client shows its own text when a mount is refused: you ride already, the creature is more than one tile away,
or it is nobody's or somebody else's.

A dead rider cannot mount. The paperdoll button of the client still opens your paperdoll while you ride.

## What happens to the horse

The horse does not stay alive off the map, as in ModernUO: it turns into data on the mount item the rider wears on
the mount layer (25). The item keeps the template of the horse and its owner, is saved with the rider, and nothing is
left behind after a restart or a logout. Getting off removes the item at once and makes the horse again, from its
template, on the tile of the rider, with its owner. The horse gets a new serial, and it keeps neither its hit points,
its hue nor what it carried.

## Making a creature rideable

Give its mobile template the tag `mount_item`, the id of an item template whose layer is `mount`:

```toml
[[mobile]]
id = "horse"
body = 228
[mobile.tags]
mount_item = "horse4"
```

Children of a template inherit the tag. An empty value, `mount_item = ""`, makes a child no mount, as the ethereal
mounts, the nightmares and the other creatures of `templates/mobiles/mounts.toml` are for now.

## Not built yet

Stabling, the bola and the dismount ability of weapons, the stop of harvesting while mounted, teleporters that deny
mounts, mounted animations and stamina, and the ethereal mounts.
