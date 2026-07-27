# English NPC Brain Dialogue Design

## Goal

Make the shipped NPC brain dialogue consistently English without changing brain behavior, state,
hooks, scheduling, or runtime architecture.

## Scope

The shipped `guard` brain uses two player-facing lines:

- first meeting: `I haven't seen you before.`
- returning player: `Welcome back.`

The embedded Lua asset is the source of truth. The matching authoring-guide example and behavioral
test expectations must use the same text.

`orion.lua` and `vega.lua` contain no player-facing dialogue and remain unchanged.

## Files

- `src/Moongate.Scripting/Assets/Brains/guard.lua`
- `docs/scripting/guides/npc-brains.md`
- `tests/Moongate.Tests/Scripting/LuaNpcBrainRuntimeTests.cs`
- `tests/Moongate.Tests/Server/AI/NpcBrainIntegrationTests.cs`

## Runtime Copy Behavior

The embedded assets seed missing files into `scripts/brains/` under the resolved Moongate root.
Existing runtime brain files are intentionally never overwritten. Therefore this change affects new
roots and roots where `guard.lua` is absent; administrators retain control of existing customized
copies.

## Testing

Use a red-green cycle:

1. Change the runtime and integration expectations to the English lines and verify they fail against
   the current Italian asset.
2. Change the embedded Lua asset and documentation example.
3. Run the focused runtime and integration tests.
4. Run the full test suite and DocFX with warnings treated as errors.

## Non-Goals

- No localization system.
- No configurable dialogue catalog.
- No changes to Lua state, hooks, intent handling, sector activity, or hot reload.
- No automatic replacement of an existing `$MOONGATE_ROOT/scripts/brains/guard.lua`.

## Acceptance Criteria

- No Italian dialogue remains in the shipped brain, its documentation example, or its behavioral
  tests.
- First and returning-player behavior remain distinct and stateful.
- Focused tests, the full solution test suite, and DocFX pass.
