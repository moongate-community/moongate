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
5. The character then waits before another skill: the number `on_use` returns, in seconds from 0 to
   3600; when it returns none, the `delay` of the skill in
   [`data/skills.toml`](data-files/skills.md); one second when the file gives none either. An
   `on_use` that calls `wait()` is still running when the wait is set: it asks the `delay` of the
   skill, 10 seconds at least, and what it returns later is not read.

The wait is of the character and one for all its skills, as in ModernUO. It is not saved: it does
not outlive a restart.

```lua
-- scripts/skills/hiding.lua
hiding = {}

function hiding.on_use(user)
    if skill.check(user, "hiding", 0, 100) then
        mobile.set_hidden(user, true)
    end
end
```

```toml
# data/skills.toml
[[skill]]
id = "hiding"
delay = 10.0
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

## The locks

Each skill has a lock, up for every skill of a new character: the arrow beside it in the skill
window. The player moves it and the client tells the server (packet `0x3A`), which keeps it with the
skill and saves it.

| Lock | The skill |
| --- | --- |
| Up | May rise when it is tried. |
| Down | Does not rise, and may be lowered to make room for another that rises when the total is at its cap. |
| Locked | Does not rise and is never lowered. |

A skill or a lock that does not exist is ignored.

`ultima.skills.gain_enabled = false` stops every gain: the checks still pass and fail.

## The stats

A successful check of a player can also raise strength, dexterity or intelligence, as ModernUO's
classic rule, whether or not the skill itself rose (a failed check never does). The fields
`str_gain`, `dex_gain` and `int_gain` of the skill in `data/skills.toml` say how much the skill
favours each stat; a stat whose number is 0 is never tried by that skill.

1. For each stat the skill favours and whose lock is up, a roll with the chance `gain / 33.3` (a
   gain of 0.8 is 2.4%).
2. A stat that passes is tried once in `stat_gain_minutes` (10): the wait starts when it is tried,
   even if nothing rises. It is kept in memory, not saved: after a restart a stat can be tried at once.
3. The more the three stats add up to near `stat_cap` (225), the more often the player *gives way*:
   one point is taken from a stat locked down (above 10 points), the lower of the two when both
   can. Once the total is at the cap it always gives way.
4. If the total is then below `stat_cap`, the stat is up and below `stat_max` (100), it rises by one.

The maximum of its bar moves with it: strength the hit points, dexterity the stamina and
intelligence the mana, and the player sees the whole status again. NPCs never gain stats.

Strength, dexterity and intelligence have a lock each, as the skills do, saved with the character:
the arrows beside them in the status window (`0xBF` subcommand `0x1A` from the client, and `0x19` to
the client at login and each time a lock changes).

| Lock | The stat |
| --- | --- |
| Up | May rise when a skill is tried. |
| Down | Does not rise; gives a point away when another rises and the total is at the cap. |
| Locked | Does not rise and is never lowered. |

The wait of a stat is kept by character in memory, so logging out and in does not skip it; a restart
does. Existing characters are all up after the upgrade; the migration `0024_mobile_stat_locks.sql`
adds the three columns.

## Trainers

A vendor or a healer teaches the skills it has at 60.0 or more, as ModernUO's trainers do. The skills are those of its
mobile template, so a vendor teaches whichever skills rolled that high. Bankers do not teach: gold dropped on them is
deposited.

- **Ask.** The context menu of the NPC has a *Train* entry for each skill it teaches and the player knows less of, from
  8 tiles. Saying *train* within 4 tiles, alive, has the NPC list the skills it teaches, or say it has nothing to teach.
- **Price.** The NPC teaches up to a third of its own value, 42.0 at most and never above the player's cap for the skill.
  Picking an entry makes it say the price: 1 gold for each tenth of a point, so 10 gold for a whole point (420 gold for
  42.0), and that for less it teaches less. The quote lasts until it is paid, another one is given, or the session ends.
- **Pay.** The player drops gold on the NPC, from 2 tiles. The skill rises at once by one tenth of a point for each gold
  piece, up to what was quoted; only the gold needed is taken, and the rest of the pile stays with the player. A drop
  without a quote from that NPC, or of anything but gold, goes back.
- **Refused.** The player already knows as much as the NPC would teach (*thou knowest all I can teach*) or more; the
  skill is not locked up, or the total cap (`ultima.skills.total_cap`) leaves no room even after lowering the skills
  locked down, which give way in the order of the skills; the player is dead. A gold drop that can no longer teach
  gets the same answer, and the quote is dropped.

A script gives an NPC these lessons with the `trainer` module and `common/training.lua`; `shopkeeper.lua` and
`healer.lua` already do. `trainer.skills(npc, player)` lists the skills, `trainer.quote(npc, player, skill)` quotes a
price and `trainer.pay(npc, giver, item)` takes the gold, which is what an `on_drag_drop` answers.

## See also

- [`hiding.lua`](scripting/shipped-scripts.md#hidinglua)
- [`bandage.lua`](scripting/shipped-scripts.md#bandagelua): Healing, with the bandages
- [The lore skills](scripting/shipped-scripts.md#the-lore-skills): Anatomy, Evaluating Intelligence, Forensic Evaluation and Detecting Hidden
- [Server configuration](server-configuration.md): `[ultima.skills]`
- [Roadmap](roadmap.md): 1.2
