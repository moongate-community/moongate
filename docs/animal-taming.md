# Animal taming

A player with the Animal Taming skill tames a wild creature and it becomes its own: it follows the player, obeys what the
player says, and the player can ride it or leave it in a [stable](mounts.md#the-stable).

## How to tame

Using the skill again while you tame says you must wait; an error in a time of the script ends the taming, is logged, and
locks nobody.

1. Use the skill (the skill list, then *Use*, or the *Animal Taming* button of the client). You read "Tame which
   animal?" and get a cursor.
2. Pick a creature within 3 tiles. If it can be tamed, you read "You start to tame the creature."
3. Stay within 7 tiles of it, in sight, for 9 or 12 seconds. Every 3 seconds you say a kind word to it.
4. At the last time the skill is rolled. On a success "It seems to accept you as master." and the creature is yours; on a
   failure "You fail to tame the creature." and you may try again at once.

The chance is the skill against what the creature asks: nothing 0.1 point under it, a sure thing 49.9 points over it, and
in between it grows in a straight line. The skill may rise as with any other. Creatures that ask more than the skill are
refused at the pick: "You have no chance of taming this creature."

| You read | Why |
| --- | --- |
| `You can't tame that!` | What you picked is no creature: an item, the land, or someone gone |
| `That being cannot be tamed.` | A player |
| `That creature cannot be tamed.` | The creature has no entry in [`taming.toml`](data-files/taming.md) |
| `That creature looks tame already.` | It has an owner, at the pick or while you tame it |
| `You have too many followers to tame that creature.` | Its slots do not fit in your followers |
| `You have no chance of taming this creature.` | Your Animal Taming is under what it asks |
| `That is too far away.` | The creature is more than 3 tiles away at the pick |
| `Someone else is already taming this creature.` | Another player is taming it |
| `You are too far away to continue taming.` | You went more than 7 tiles away |
| `You are dead and cannot continue taming.` | You died, also while the cursor was out |
| `You can no longer see the creature.` | There is something between you |
| `The animal is too angry to continue taming.` | It was hurt since the last time, or it is fighting |

A creature that is tamed leaves its spawn region, which brings another in its place, and goes for nobody any more, whatever it hunted before, and the guards of a town leave it alone: a
tamed dragon is not a monster to them. It still defends itself when it is hit.

## What a pet does

A tamed creature follows its owner: it walks when it is more than 2 tiles away and runs from 7, and when it cannot get
there in ten steps it is moved beside the owner. Farther than 24 tiles, on another map, or with the owner not in the
world, it stays where it is. A creature that sleeps because no player is near does not follow: a pet left far behind is
met again where it stood. A pet that is hit defends itself.

The same goes for a creature a game master gave with [`tame`](commands/tame.md), one taken out of a stable and one that
was ridden: they follow by default. The order of a pet is kept with it, apart from those three cases, which forget it.

## What you can tell it

Say the words within 14 tiles, as the owner and alive. With **all** before them they are for every pet of yours near; with
the name of the pet first (`a horse stay`) they are for that pet only.

| Say | The pet |
| --- | --- |
| `come`, `all come` | Walks to you and stays beside you |
| `follow`, `follow me`, `all follow`, `all follow me` | Follows you |
| `stay`, `all stay` | Stands where it is |
| `stop`, `all stop` | Stops what it does, the fight too, and stays |
| `guard`, `all guard`, `all guard me` | Stays near you and fights whoever fights you or it |
| `kill`, `attack`, `all kill`, `all attack` | Asks for a target; the pets fight it, then go back to what they did |
| `release` (with its name) | Asks if you are sure; yes lets it go: it is wild again and you have one follower less |

It will not fight you, another pet of yours, or what is dead. Friend, transfer, drop and patrol are not built.

## Followers

A player may have 5 followers (`[ultima.pets] max_followers`, 1 to 50). Each creature counts for its slots, usually 1: the
creatures of yours that are in the world, and the one you ride. A creature in a stable counts for nothing. A creature of yours that a game master rides counts for you, not for the rider. The count is
in the status window of your character (`followers 2/5`), shown again when you tame, stable, claim, mount or dismount, and
when one of your creatures dies. The stable does not look at the limit when you take a pet out,
so a player can hold more than the limit by stabling and claiming.

## What can be tamed

The creatures of [`data/taming.toml`](data-files/taming.md): about 75, with the skill each asks and the slots each counts
for. A game master can still give any creature that can be ridden with [`tame`](commands/tame.md),
and ignores the limit.

## For scripts

The Lua module `pet`: `pet.info(creature)`, `pet.followers(player)`, `pet.max_followers()` and `pet.tame(player,
creature)`; the skill script is `scripts/skills/animal_taming.lua`.

## Not built yet

Loyalty and hunger, so a pet always obeys; friend, transfer, drop and patrol; bringing the pets along when the owner
travels by gate or spell; and the pets of a player who is offline stay where they were.
