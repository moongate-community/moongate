# Animal taming

A player with the Animal Taming skill tames a wild creature and it becomes its own: it can ride it, leave it in a
[stable](mounts.md#the-stable) and so on. This is the first slice: the creature does not follow its owner or obey words
yet.

## How to tame

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
| `You are dead and cannot continue taming.` | You died |
| `You can no longer see the creature.` | There is something between you |
| `The animal is too angry to continue taming.` | It was hurt since you began |

## Followers

A player may have 5 followers (`[ultima.pets] max_followers`, 1 to 50). Each creature counts for its slots, usually 1: the
creatures of yours that are in the world, and the one you ride. A creature in a stable counts for nothing. The count is
in the status window of your character (`followers 2/5`). The stable does not look at the limit when you take a pet out,
so a player can hold more than the limit by stabling and claiming.

## What can be tamed

The creatures of [`data/taming.toml`](data-files/taming.md): about 75, with the skill each asks and the slots each counts
for. A game master can still give any creature that can be ridden with [`tame`](commands/tame.md),
and ignores the limit.

## For scripts

The Lua module `pet`: `pet.info(creature)`, `pet.followers(player)`, `pet.max_followers()` and `pet.tame(player,
creature)`; the skill script is `scripts/skills/animal_taming.lua`.

## Not built yet

The creature does not follow its owner or obey spoken commands, there is no release, no loyalty or hunger, and a creature
that has an owner cannot be tamed by another.
