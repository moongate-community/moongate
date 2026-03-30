# Death and Resurrection

Moongate now ships a complete first slice of the player death loop: players leave a corpse, remain in the world as ghosts, wear a death shroud while dead, and can return to life through healer or ankh resurrection offers.

This page describes the current runtime behavior.

## What Happens On Death

When a player dies:

1. A corpse is created at the death location.
2. Equipped items are moved onto the corpse.
3. The player's backpack is moved onto the corpse as a container, with its contents still inside it.
4. The player remains in the world instead of being deleted.
5. The player is rendered as a ghost.
6. A `Death Shroud` is equipped while the player is dead.

This means resurrection does not auto-return gear. Recovering your items is a separate step, just like standard UO corpse recovery.

## Corpse Behavior

Player corpses are real corpse items, not a cosmetic effect.

Important consequences:

- the corpse stays at the place of death
- your items remain on the corpse until looted or decayed
- resurrection does not automatically move items back onto the player

The current system is intentionally conservative: it favors a clear corpse-recovery loop over hidden auto-restore behavior.

## Ghost State

After death, the player becomes a ghost mobile in the world.

Current ghost behavior includes:

- dead players stay visible as ghosts
- the death shroud remains equipped while dead
- nearby clients receive a redraw so the body change is visible immediately

This is a real gameplay state, not only a persistence flag.

## Resurrection Sources

The current build supports resurrection from two sources:

- healer NPCs
- ankhs

### Healers

Resurrection-capable healer NPCs automatically offer resurrection to nearby dead players.

Current healer behavior:

- the player must be dead
- the player must be within healer range
- the server opens a resurrection confirmation gump

### Ankhs

Ankhs also offer resurrection to nearby dead players.

In addition, the runtime ankh item script supports direct interaction from the ghost, so ankhs work both as world resurrection sources and as explicit interaction targets.

## Confirmation Flow

Resurrection is not applied blindly.

The current flow is:

1. a valid healer or ankh creates a pending offer
2. the server opens a resurrection gump
3. the player chooses `Accept` or `Decline`
4. on accept, the server validates the source again before restoring life

This prevents invalid or stale resurrection requests from applying after the player has moved away or the source is no longer valid.

## What Happens On Resurrection

When resurrection succeeds:

- the player becomes alive again
- the ghost appearance is cleared
- the death shroud is removed
- hit points are restored to a small safe amount
- the corpse remains in the world

You return to life without auto-equipping your old gear. If you want your items back, recover them from your corpse.

## Current Scope

This slice is intentionally focused. It includes:

- player corpse creation
- ghost body rendering
- death shroud handling
- healer resurrection
- ankh resurrection
- confirmation gump flow

The following are still outside the current scope:

- resurrection spells
- stat loss or murder-rule penalties
- pet resurrection
- automatic corpse reclaim
- broader shrine or virtue systems
