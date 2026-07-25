# English NPC Brain Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Translate the shipped guard brain's two player-facing replies from Italian to neutral English while preserving all existing behavior.

**Architecture:** Keep the embedded `guard.lua` asset as the source of truth. Update behavioral expectations first to prove the old Italian asset fails, then update the Lua asset and its documentation example; do not add localization or alter runtime behavior.

**Tech Stack:** Lua, .NET 10, C# 14, xUnit 2.9.3, DocFX.

## Global Constraints

- First-meeting text is exactly `I haven't seen you before.`
- Returning-player text is exactly `Welcome back.`
- Existing runtime copies under `$MOONGATE_ROOT/scripts/brains/guard.lua` are never overwritten.
- `orion.lua` and `vega.lua` remain unchanged.
- Do not change hooks, state, intents, scheduler behavior, hot reload, or runtime architecture.
- Work directly on `develop`, as explicitly requested by the user.
- `.claude/` must never be committed.
- Use English Conventional Commits with no AI attribution.

---

### Task 1: Translate the shipped guard dialogue

**Files:**

- Modify: `tests/Moongate.Tests/Scripting/LuaNpcBrainRuntimeTests.cs:1009-1010`
- Modify: `tests/Moongate.Tests/Server/AI/NpcBrainIntegrationTests.cs:29-30`
- Modify: `src/Moongate.Scripting/Assets/Brains/guard.lua:25,31`
- Modify: `docs/scripting/guides/npc-brains.md:52,58`

**Interfaces:**

- Consumes: built-in brain seeding and the existing `on_speech_heard(ctx, state, event)` behavior.
- Produces: the same `brain.say(text)` intents with exact English text; no signature or state change.

- [ ] **Step 1: Change the behavioral expectations to English**

In `LuaNpcBrainRuntimeTests.cs`, replace the two assertions with:

```csharp
Assert.Equal("I haven't seen you before.", Assert.Single(firstPlayerSpeech.Decision.Intents).Text);
Assert.Equal("Welcome back.", Assert.Single(returningPlayerSpeech.Decision.Intents).Text);
```

In `NpcBrainIntegrationTests.cs`, replace the constants with:

```csharp
private const string FirstMeetingReply = "I haven't seen you before.";
private const string ReturningPlayerReply = "Welcome back.";
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
dotnet test Moongate.slnx --filter "FullyQualifiedName~StartAsync_BuiltInBrainsExposeDeterministicDescriptorsAndBehavior|FullyQualifiedName~PlayerSpeech_CompleteGuardLifecycle_RepliesWithRetainedMemory"
```

Expected: FAIL because the embedded `guard.lua` still returns `Non ti avevo mai visto prima.` and `Bentornato.`.

- [ ] **Step 3: Translate the embedded Lua asset**

In `src/Moongate.Scripting/Assets/Brains/guard.lua`, use:

```lua
return brain.say("I haven't seen you before.")
```

for the first meeting, and:

```lua
return brain.say("Welcome back.")
```

for a returning player.

- [ ] **Step 4: Update the documentation example**

Make the identical two replacements inside the complete guard example in
`docs/scripting/guides/npc-brains.md`.

- [ ] **Step 5: Run the focused tests and verify GREEN**

Run:

```bash
dotnet test Moongate.slnx --filter "FullyQualifiedName~StartAsync_BuiltInBrainsExposeDeterministicDescriptorsAndBehavior|FullyQualifiedName~PlayerSpeech_CompleteGuardLifecycle_RepliesWithRetainedMemory"
```

Expected: 2 passed, 0 failed.

- [ ] **Step 6: Verify no shipped Italian dialogue remains**

Run:

```bash
rg -n "Non ti avevo mai visto prima|Bentornato" \
  src/Moongate.Scripting/Assets/Brains \
  docs/scripting \
  tests/Moongate.Tests
```

Expected: no matches.

- [ ] **Step 7: Run complete verification**

Run:

```bash
dotnet test Moongate.slnx
dotnet docfx docs/docfx.json --warningsAsErrors
git diff --check
git status --short
```

Expected: all tests pass, DocFX reports zero warnings/errors, no whitespace errors, and only the four intended implementation files plus this plan are changed.

- [ ] **Step 8: Commit the implementation**

```bash
git add \
  src/Moongate.Scripting/Assets/Brains/guard.lua \
  docs/scripting/guides/npc-brains.md \
  tests/Moongate.Tests/Scripting/LuaNpcBrainRuntimeTests.cs \
  tests/Moongate.Tests/Server/AI/NpcBrainIntegrationTests.cs
git commit -m "fix(ai): translate guard dialogue to English"
```
