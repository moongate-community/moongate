# Fishing

A player with a fishing pole pulls fish out of the water. The Fishing skill decides what comes out and grows
with use; the fish of a place run out and come back with time.

## How to fish

1. Double click a fishing pole you carry. You read "What water do you want to fish in?" and get a cursor.
2. Pick water within 4 tiles, in sight.
3. Your character casts, the water splashes a moment later, and 8 seconds after the cast comes the result.

You must still be within 4 tiles of the water when the 8 seconds are over, and you fish one water at a time: a
second double click meanwhile answers "You are already fishing."

| You read | Why |
| --- | --- |
| `You need water to fish in!` | What you picked is not water: dry land, an item or someone |
| `You need to be closer to the water to fish!` | The water is more than 4 tiles away or out of sight, or you walked away before the result |
| `The fish don't seem to be biting here.` | The place has no fish left: try elsewhere, or come back later |
| `You fish a while, but fail to catch anything.` | The try failed, or nothing came out |
| `You do not have room in your backpack for a fish.` | Your backpack cannot take the catch, which stays in the water |

## What comes out

The try is rolled on the Fishing skill between 0 and 100, so the chance to pull something out is the skill itself,
and the skill may rise at every try. When the try passes:

| What | Chance | At skill 0 | At skill 100 |
| --- | --- | --- | --- |
| A piece of footwear: boots, sandals, shoes or thigh boots | (105 − skill) / 525 | 20% | about 1% |
| Nothing | (200 − skill) / 400 | 50% | 25% |
| A fish, one of four | the rest | | |

The catch goes into the backpack. The pole does not wear out.

## The fish of a place

Each map is cut in areas of 8 by 8 tiles. An area holds 5 to 15 fish, drawn the first time someone fishes there.
A catch takes one; a failed try and "nothing" take none. The area is full again, all at once, 10 to 20 minutes
after the first catch from it.

The areas are kept in memory: after a restart every place is full. The numbers are in
[`harvest.toml`](data-files/harvest.md).

## Change the rules

The rules are in `scripts/items/fishing_pole.lua`: the range, the seconds, what comes out and how often. See
[Shipped scripts](scripting/shipped-scripts.md#fishing_polelua). A template fishes with
`script_id = "fishing_pole"`.

## Not yet

Magic fish and big fish, deep water and what comes from it (special nets, messages in a bottle, sea serpents),
cutting a fish into steaks, bait, and the rule against fishing while riding, which waits for mounts.

## See also

- [`harvest.toml`](data-files/harvest.md)
- [Skills](skills.md)
- [Shipped scripts](scripting/shipped-scripts.md#fishing_polelua)
