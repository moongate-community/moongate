# Mounts

A player rides a tamed horse, llama or ostard: it walks and runs twice as fast. This is the first slice of the
mounts; the stable, the bola and the ethereal mounts are not built yet.

## How to ride

1. Get a creature that is yours. A game master gives it with [`tame`](commands/tame.md); the skill of taming does
   not exist yet. A creature with no owner is nobody's to ride, a game master's included.
2. Stand within one tile of it, on its level, and double click it. Its owner sits on it; a game master can sit on a
   creature that belongs to someone else.
3. Walk or run. A rider takes a step every 200 ms walking and every 100 ms running, a rider on foot every 400 and 200.
   The horse runs, not the rider: running costs a rider no stamina, and an overloaded rider tires as if it walked.
4. Double click yourself to get off, or die: the horse stands again where you were.

The client shows its own text when a mount is refused: you ride already, the creature is more than one tile away,
or it is nobody's or somebody else's.

A dead rider cannot mount. The paperdoll button of the client still opens your paperdoll while you ride.

## What happens to the horse

The horse does not stay alive off the map: it turns into data on the mount item the rider wears on
the mount layer (25). The item keeps the template of the horse and its owner, is saved with the rider, and nothing is
left behind after a restart or a logout. Getting off removes the item at once and makes the horse again, from its
template, on the tile of the rider, with its owner. The horse gets a new serial, and it keeps neither its hit points,
its hue nor what it carried.

The row of the mount item is deleted before the horse is made, so a crash between the two leaves the rider on foot
and the horse in the world, never both. If the horse cannot be made it is tried three times, a second apart, each
failure logged; after the third it is lost, and the log line says which template and where.

The mount item cannot be lifted off the rider. A creature that is dying cannot be ridden, nor by a rider whose
inventory is reserved, such as while a book attachment is being claimed.

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

## What a rider cannot do

A rider cannot mine with a pickaxe, fish with a pole or use the Stealth skill: each is refused with the client's own
text and nothing starts. The axe and the Hiding skill are left alone. A script asks `mobile.is_mounted(serial)`.

## Teleporters that refuse a rider

A teleporter item with the prop `deny_mounted` set to `true` (as text in a decoration file: `deny_mounted = "true"`)
tells a rider to dismount first and leaves it where it is; no smoke and no sound play. A walker on foot goes through.
No shipped teleporter carries the flag yet.

```toml
[[decoration]]
type = "Teleporter"
item_id = 0x1BC3
props = { point_dest = [5690, 569, 25], deny_mounted = "true" }
locations = [[5827, 593, 0]]
```

## The stable

The animal trainers of the towns, the gypsy ones included (`script_id = "stablemaster"`), keep a stable. They are vendors as before: the shop
and the lessons stay.

1. Stand within 12 tiles and say *stable*, or pick *Stable* in the trainer's context menu. You read the prompt and
   get a cursor: pick a pet of yours, within a tile of you.
2. The pet leaves the world and its template joins your stable; the fee is taken from your backpack and then your bank
   (30 gold by default). The trainer answers with the client's text: your pet is stabled, or you cannot stable that,
   it is not yours, you have too many pets in the stable, or you cannot pay.
3. Say *claim* to see the list of your stabled pets, one button each; a button makes that pet again beside you.
   *Claim All* in the menu takes them all back.

Only a pet that can be ridden and is yours can be stabled, not one that is dying. A claimed pet is made again from its
template, with you as its owner, and keeps neither its hit points, hue nor what it carried; the spawn is tried three
times, and a pet that cannot be made goes back to your stable. The stable is the prop `stabled` of your character (the template ids, joined by `;`), saved with it. The limit
and the fee are in [`[ultima.stable]`](server-configuration.md).

## Ethereal mounts

A statuette of an ethereal mount (a horse, a llama, an ostard, a kirin, a unicorn, a ridgeback, a swamp dragon or a
beetle, `templates/items/misc/ethereal-statues.toml`) lets its owner ride with no creature. Double click it while it
lies in your backpack: it is gone and you sit on the ethereal mount. You read the client's text when it is not in
your backpack, or when you ride already. Getting off, or dying, gives the statuette back in your backpack, past its limit of items
(its place was freed when you rode), or on the ground where you stand when you have no backpack. It comes back with its
hue and its name but a new serial. There is no wait to cast it and no follower slot. A game master
makes one with `.add ethereal_horse_statue`. The statuette is a template with `script_id = "ethereal_mount"` and the tag
`mount_item`, the template of the mount item that is worn.

## Fighting from the saddle

A rider who attacks plays the actions of a mount: one hand, two hands, bow or crossbow. The animation that tells a
blow was taken is the usual one. The ids are those of the client's animation table; they were not tried with a client.

## Not built yet

The bola and the dismount ability of weapons.
