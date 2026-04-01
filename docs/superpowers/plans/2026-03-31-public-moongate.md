# Public Moongate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a shared public moongate gump that opens from a dedicated `public_moongate` item and teleports players to Lua-authored shard destinations.

**Architecture:** Keep the existing `items.teleport` flow unchanged and introduce a separate `public_moongate` template plus `items.public_moongate` Lua script. Author destinations in a shared Lua dataset, render the UI through a dedicated shared gump under `gumps/moongates/`, and perform final revalidation in Lua before teleporting with the same effects/sound semantics used by the generic teleporter.

**Tech Stack:** .NET 10, NUnit, MoonSharp Lua runtime, Lua gump scripts, item templates under `moongate_data/templates/items/base/`, DocFX docs.

---

## File Structure

### New files

- `tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs`
  - End-to-end Lua runtime coverage for gump open and destination selection.
- `moongate_data/scripts/items/public_moongate.lua`
  - Dedicated public moongate item script for double-click open.
- `moongate_data/scripts/moongates/data.lua`
  - Shared Lua-authored destination dataset for public moongates.
- `moongate_data/scripts/gumps/moongates/public_moongate.lua`
  - Shared gump entry point and button handlers.
- `moongate_data/scripts/gumps/moongates/constants.lua`
  - Gump ids, sizes, button id constants, layout constants.
- `moongate_data/scripts/gumps/moongates/state.lua`
  - Session-scoped state: selected group, source moongate serial.
- `moongate_data/scripts/gumps/moongates/ui.lua`
  - Shared frame and list row primitives for the public moongate gump.
- `moongate_data/scripts/gumps/moongates/render.lua`
  - Renders group list and destination list from state + dataset.
- `docs/articles/scripting/public-moongates.md`
  - Authoring guide for public moongate destinations and usage.

### Modified files

- `moongate_data/templates/items/base/teleports.json`
  - Add `public_moongate` template; do not change existing `moongate`.
- `docs/articles/scripting/toc.yml`
  - Add the new guide.
- `docs/articles/scripting/overview.md`
  - Mention the new public moongate system and guide.

### Existing references to follow

- `moongate_data/scripts/items/teleport.lua`
  - Reuse teleport effect/sound behavior and destination revalidation style.
- `moongate_data/scripts/gumps/teleports/*.lua`
  - Reuse controller/state/render organization and navigation patterns.
- `moongate_data/scripts/gumps/layout/header.lua`
- `moongate_data/scripts/gumps/layout/stack.lua`
  - Reuse layout helpers where they make the gump smaller and clearer.
- `tests/Moongate.Tests/Scripting/ResurrectionLuaRuntimeTests.cs`
- `tests/Moongate.Tests/Scripting/QuestLuaRuntimeTests.cs`
  - Follow runtime Lua test harness patterns.
- `tests/Moongate.Tests/Server/Services/Magic/Spells/Magery/Seventh/GateTravelSpellTests.cs`
  - Keep in mind existing `moongate` template is already used by magic.

## Task 1: Create Issue, Branch, Worktree, and Baseline

**Files:**
- Modify: none

- [ ] **Step 1: Create the GitHub issue**

Open a new issue for the feature, for example: `Public moongate gump`.

- [ ] **Step 2: Create the feature branch in an isolated worktree**

Run:

```bash
git worktree add .worktrees/feature-<issue>-public-moongate -b feature/<issue>-public-moongate
```

Expected: new worktree created from current `develop`.

- [ ] **Step 3: Verify clean baseline in the worktree**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~ResurrectionLuaRuntimeTests|FullyQualifiedName~QuestLuaRuntimeTests"
```

Expected: passing baseline before new work starts.

## Task 2: Add the Failing Runtime Test for Opening the Public Moongate Gump

**Files:**
- Create: `tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs`

- [ ] **Step 1: Write the failing open-flow runtime test**

Add a test that:

- creates a temp script root
- copies in:
  - `moongate_data/scripts/items/public_moongate.lua`
  - `moongate_data/scripts/moongates/data.lua`
  - `moongate_data/scripts/gumps/moongates/public_moongate.lua`
  - `moongate_data/scripts/gumps/moongates/constants.lua`
  - `moongate_data/scripts/gumps/moongates/state.lua`
  - `moongate_data/scripts/gumps/moongates/ui.lua`
  - `moongate_data/scripts/gumps/moongates/render.lua`
- boots `LuaScriptEngineService` with the minimal module set needed for:
  - gump sending
  - item lookup
  - mobile lookup
- invokes `items_public_moongate.on_double_click(...)`
- asserts a `CompressedGumpPacket` is queued and contains the public moongate title plus at least one destination group label

Use the runtime harness shape from `ResurrectionLuaRuntimeTests`.

- [ ] **Step 2: Run the new test and verify it fails**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests"
```

Expected: fail because the files and handlers do not exist yet.

- [ ] **Step 3: Commit the failing test skeleton**

```bash
git add tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs
git commit -m "test: add failing public moongate gump runtime coverage"
```

## Task 3: Implement Dataset and Gump Open Flow

**Files:**
- Create: `moongate_data/scripts/items/public_moongate.lua`
- Create: `moongate_data/scripts/moongates/data.lua`
- Create: `moongate_data/scripts/gumps/moongates/public_moongate.lua`
- Create: `moongate_data/scripts/gumps/moongates/constants.lua`
- Create: `moongate_data/scripts/gumps/moongates/state.lua`
- Create: `moongate_data/scripts/gumps/moongates/ui.lua`
- Create: `moongate_data/scripts/gumps/moongates/render.lua`

- [ ] **Step 1: Add the shared destination dataset**

Create `moongate_data/scripts/moongates/data.lua` with a declarative `load()` function that returns a stable list of groups and destinations.

Use a small starter dataset, for example:

```lua
return {
  load = function()
    return {
      {
        id = "britannia",
        name = "Britannia",
        destinations = {
          { id = "moonglow", name = "Moonglow", map = "felucca", x = 4467, y = 1283, z = 5 },
          { id = "britain", name = "Britain", map = "felucca", x = 1336, y = 1997, z = 5 }
        }
      }
    }
  end
}
```

- [ ] **Step 2: Build the gump shell and state**

Implement:

- constants for gump id, sizes, button ranges
- session state storing:
  - `group_id`
  - `source_item_serial`
- render logic for:
  - left-side group list
  - right-side destination list for selected group

Reuse `gumps.layout.header` and `gumps.layout.stack` where that makes the code smaller.

- [ ] **Step 3: Implement the item double-click open path**

In `items/public_moongate.lua`:

- validate `ctx.session_id`, `ctx.mobile_id`, `ctx.item.serial`
- require the gump entry module
- call `public_moongate.open(session_id, mobile_id, item_serial)`

Do not add this to `interaction/init.lua`; the item script should be self-contained.

- [ ] **Step 4: Run Lua syntax checks**

Run:

```bash
luac -p moongate_data/scripts/items/public_moongate.lua
luac -p moongate_data/scripts/moongates/data.lua
luac -p moongate_data/scripts/gumps/moongates/public_moongate.lua
luac -p moongate_data/scripts/gumps/moongates/constants.lua
luac -p moongate_data/scripts/gumps/moongates/state.lua
luac -p moongate_data/scripts/gumps/moongates/ui.lua
luac -p moongate_data/scripts/gumps/moongates/render.lua
```

Expected: all parse cleanly.

- [ ] **Step 5: Re-run the open-flow test**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests"
```

Expected: the open-flow test now passes, while the travel-flow test does not exist yet.

- [ ] **Step 6: Commit the first working slice**

```bash
git add moongate_data/scripts/items/public_moongate.lua \
        moongate_data/scripts/moongates/data.lua \
        moongate_data/scripts/gumps/moongates/public_moongate.lua \
        moongate_data/scripts/gumps/moongates/constants.lua \
        moongate_data/scripts/gumps/moongates/state.lua \
        moongate_data/scripts/gumps/moongates/ui.lua \
        moongate_data/scripts/gumps/moongates/render.lua \
        tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs
git commit -m "feat: add public moongate gump shell"
```

## Task 4: Add the Failing Runtime Test for Destination Selection and Travel

**Files:**
- Modify: `tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs`

- [ ] **Step 1: Add a second runtime test for destination selection**

Extend the test file with a case that:

- opens the public moongate gump
- simulates a gump button click for a destination
- asserts:
  - the mobile teleports to the expected map/x/y/z
  - no teleport occurs if the mobile is already at the selected location
  - no teleport occurs if the player is no longer in range of the source item

Model the gump response flow after `ResurrectionLuaRuntimeTests`.

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests"
```

Expected: the new travel test fails because the selection handler and final revalidation do not exist yet.

- [ ] **Step 3: Commit the failing travel test**

```bash
git add tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs
git commit -m "test: add failing public moongate travel coverage"
```

## Task 5: Implement Destination Selection, Revalidation, and Teleport

**Files:**
- Modify: `moongate_data/scripts/gumps/moongates/public_moongate.lua`
- Modify: `moongate_data/scripts/gumps/moongates/constants.lua`
- Modify: `moongate_data/scripts/gumps/moongates/state.lua`
- Modify: `moongate_data/scripts/gumps/moongates/render.lua`
- Modify: `moongate_data/scripts/items/public_moongate.lua`

- [ ] **Step 1: Encode stable destination button ids**

Use a simple button encoding scheme in `constants.lua`, for example:

- `BUTTON_GROUP_BASE = 1000`
- `BUTTON_DEST_BASE = 2000`

Render groups and destinations from those ranges and decode them in `public_moongate.lua`.

- [ ] **Step 2: Implement final travel revalidation**

Before teleporting, check:

- source item still resolves via `item.get(source_item_serial)`
- actor still resolves via `mobile.get(character_id)`
- actor is still within 1 tile of the source item
- selected group/destination ids still exist in the shared dataset
- actor is not already at the destination tile on the destination map

- [ ] **Step 3: Reuse teleporter visual behavior**

Mirror the visible semantics from `items/teleport.lua`:

- source effect before teleport
- destination effect after teleport
- optional sound if configured

Keep the logic local to `items.public_moongate` / the moongate gump modules; do not mutate `items.teleport.lua`.

- [ ] **Step 4: Re-run syntax checks**

Run:

```bash
luac -p moongate_data/scripts/items/public_moongate.lua
luac -p moongate_data/scripts/gumps/moongates/public_moongate.lua
luac -p moongate_data/scripts/gumps/moongates/constants.lua
luac -p moongate_data/scripts/gumps/moongates/state.lua
luac -p moongate_data/scripts/gumps/moongates/render.lua
```

Expected: all parse cleanly.

- [ ] **Step 5: Re-run the public moongate runtime tests**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests"
```

Expected: both open-flow and travel-flow tests pass.

- [ ] **Step 6: Commit the second working slice**

```bash
git add moongate_data/scripts/items/public_moongate.lua \
        moongate_data/scripts/gumps/moongates/public_moongate.lua \
        moongate_data/scripts/gumps/moongates/constants.lua \
        moongate_data/scripts/gumps/moongates/state.lua \
        moongate_data/scripts/gumps/moongates/render.lua \
        tests/Moongate.Tests/Scripting/PublicMoongateLuaRuntimeTests.cs
git commit -m "feat: add public moongate travel handling"
```

## Task 6: Wire the Dedicated Template Without Breaking Spell Moongates

**Files:**
- Modify: `moongate_data/templates/items/base/teleports.json`

- [ ] **Step 1: Add a new `public_moongate` template**

Add a sibling item template such as:

```json
{
  "type": "item",
  "id": "public_moongate",
  "name": "Public Moongate",
  "category": "Structure",
  "tags": ["moongate", "teleport", "worldgen", "public"],
  "base_item": "teleporter",
  "itemId": "0x0F6C",
  "scriptId": "items.public_moongate",
  "description": "Shared destination moongate."
}
```

Do not modify the existing `moongate` template id, because `Gate Travel` already depends on it.

- [ ] **Step 2: Re-run the public moongate runtime tests**

Run:

```bash
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests|FullyQualifiedName~GateTravelSpellTests"
```

Expected: public moongate tests pass and `GateTravelSpellTests` stays green.

- [ ] **Step 3: Commit the template wiring**

```bash
git add moongate_data/templates/items/base/teleports.json
git commit -m "feat: add public moongate item template"
```

## Task 7: Document Public Moongate Authoring

**Files:**
- Create: `docs/articles/scripting/public-moongates.md`
- Modify: `docs/articles/scripting/toc.yml`
- Modify: `docs/articles/scripting/overview.md`

- [ ] **Step 1: Write the authoring guide**

Document:

- what `public_moongate` is
- where the shared destinations live: `moongate_data/scripts/moongates/data.lua`
- the Lua data shape for groups and destinations
- how `public_moongate` differs from `moongate`
- that `moongate` is still reserved for spell-created moongates

- [ ] **Step 2: Link the guide from scripting docs**

Add it to `docs/articles/scripting/toc.yml` and mention it in `docs/articles/scripting/overview.md`.

- [ ] **Step 3: Commit the docs**

```bash
git add docs/articles/scripting/public-moongates.md \
        docs/articles/scripting/toc.yml \
        docs/articles/scripting/overview.md
git commit -m "docs: add public moongate authoring guide"
```

## Task 8: Full Verification and Finish

**Files:**
- Modify: none

- [ ] **Step 1: Run targeted verification**

Run:

```bash
luac -p moongate_data/scripts/items/public_moongate.lua
luac -p moongate_data/scripts/moongates/data.lua
luac -p moongate_data/scripts/gumps/moongates/public_moongate.lua
luac -p moongate_data/scripts/gumps/moongates/constants.lua
luac -p moongate_data/scripts/gumps/moongates/state.lua
luac -p moongate_data/scripts/gumps/moongates/ui.lua
luac -p moongate_data/scripts/gumps/moongates/render.lua
dotnet test tests/Moongate.Tests/Moongate.Tests.csproj --filter "FullyQualifiedName~PublicMoongateLuaRuntimeTests|FullyQualifiedName~ResurrectionLuaRuntimeTests|FullyQualifiedName~QuestLuaRuntimeTests|FullyQualifiedName~GateTravelSpellTests"
```

Expected: all pass.

- [ ] **Step 2: Run the full repository test suite**

Run:

```bash
dotnet test Moongate.slnx
```

Expected: full suite green.

- [ ] **Step 3: Finish the branch**

Use the finishing workflow:

- push the feature branch
- open PR to `develop`
- merge after checks pass
- close the linked issue
- remove the feature worktree and local branch

