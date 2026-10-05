# Skills

A player uses a skill from its skill window or a macro; a script tries a mobile at a skill with
`skill.check`, and the try may raise the skill. This is the first slice: one skill is shipped,
[Hiding](scripting/shipped-scripts.md#hidinglua), and the rest of the game calls the same check as
it is built.

## Using a skill

The client sends a text command (packet `0x12`, kind `0x24`) whose text starts with the number of
the skill, as `21 0` for Hiding.

1. A prisoner of the [jail](jail.md) reads "You may not use skills in jail." (message 30168); the
   staff is never refused.
2. A character still waiting after its last skill reads "You must wait a few moments to use
   another skill." (cliloc 500118), once a second at most.
3. The script of the skill runs: `on_use(user)` of the table named after the skill in
   `scripts/skills/<skill>.lua`, with the names of `data/skills.toml` (`hiding`, `animal_lore`).
4. A skill without a script answers "That skill cannot be used directly." (500014), and asks no
   wait.
5. The number `on_use` returns is the seconds before the character may use another skill, from 0
   to 3600; one second when it returns none. An `on_use` that calls `wait()` is still running when
   the wait is set: it asks 10 seconds, and what it returns later is not read.

The wait is of the character and one for all its skills, as in ModernUO. It is not saved: it does
not outlive a restart.

```lua
-- scripts/skills/hiding.lua
hiding = {}

function hiding.on_use(user)
    if skill.check(user, "hiding", 0, 100) then
        mobile.set_hidden(user, true)
    end

    return 10
end
```

The scripts are loaded at startup, as the [item scripts](scripting/item-scripts.md) are; the other
kinds of text command (casting a spell, opening a door, an action) are read and not acted on yet.

## The check

`skill.check(who, skill, min, max)` tries a mobile at a task: `min` is the points at which the
task can just be begun, `max` those at which it never fails.

| Points of the mobile | Result | Can it teach? |
| --- | --- | --- |
| Below `min` | Fails | No |
| At `max` or above | Succeeds | No |
| In between | Succeeds with the chance `(points - min) / (max - min)` | Yes |

So a task too hard or too easy teaches nothing, as in ModernUO.

## The gain

A try in between, passed or failed, may raise the skill of a player. NPCs never learn.

- Below 10.0 points a skill always learns.
- From 10.0 on, the chance is ModernUO's: the mean of the room left under the total cap and under
  the cap of the skill, then the mean of that and of how hard the task was (a pass counts half of
  what was missing to the certainty, a failure a fifth), times the `gain_factor` of the skill in
  `data/skills.toml`; never less than one in a hundred.
- A skill up to 10.0 points gains from 0.1 to 0.4 points at a time, above it 0.1.
- A skill at its cap (100.0) does not rise, nor one whose lock is not up.
- The total of a player's skills stops at [`ultima.skills.total_cap`](server-configuration.md)
  (700.0 points). The nearer the total is to it, the more often a gain first lowers another skill
  whose lock is down by the same amount; at the cap, a skill rises only when one could be lowered.

A lock is stored with each skill and is up for every skill of a new character. The client cannot
change it yet: that is the next slice, with the gain of strength, dexterity and intelligence.

`ultima.skills.gain_enabled = false` stops every gain: the checks still pass and fail.

## See also

- [`hiding.lua`](scripting/shipped-scripts.md#hidinglua)
- [Server configuration](server-configuration.md): `[ultima.skills]`
- [Roadmap](roadmap.md): 1.2
