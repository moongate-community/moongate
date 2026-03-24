# Quivers

Moongate supports a first ranged-combat quiver implementation.

## Behavior

- A quiver is an item equipped on the `Cloak` layer.
- It behaves like a restricted container even if the base tile is not flagged as a generic container.
- It can hold exactly one ammo stack.
- Supported ammo types are:
  - `arrow`
  - `bolt`
- The stack capacity is `500`.

## Ranged Combat

When a ranged weapon fires, ammo is resolved in this order:

1. equipped quiver
2. backpack

If the quiver contains the wrong ammo type for the current weapon, the server falls back to the backpack.

## Lower Ammo Cost

Quivers can define `LowerAmmoCost`.

- When the proc succeeds, the shot does not consume ammo.
- The player must still have valid ammo in the quiver or backpack, otherwise the shot fails.

## Damage Increase

Quivers can define `quiverDamageIncrease`.

- It applies only to ranged attacks.
- It is added on top of the existing damage bonus flow.
- Melee attacks ignore quiver damage bonuses.

## Weight Reduction

Quivers can define `weightReduction`.

- It applies only to items contained inside the quiver.
- It does not reduce the base item weight of the quiver itself.

## Defense Chance Increase

Quivers can also use the normal `defenseChanceIncrease` item modifier.

- It is aggregated like any other equipped item modifier.
- `quiver_of_infinity` contributes `+5` defense chance increase.

## Built-In Templates

Current built-in quiver templates:

- `quiver`
- `quiver_of_infinity`

Both are defined in [special.json](/Users/squid/projects/personal/moongatev2/.worktrees/feature-quiver-v1/moongate_data/templates/items/modernuo/special.json).
