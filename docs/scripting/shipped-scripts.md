# Shipped scripts

This page is part of [Writing Lua scripts](../scripting.md). The distribution ships these scripts under
`scripts/`, and `mgctl init` copies them into the root: each one is an example to read and to change. How a
script is bound to a template is told in [Mobile scripts](mobile-scripts.md) and [Item scripts](item-scripts.md),
where `wander.lua` and `potion.lua` are listed.

## monster.lua

The distribution's `scripts/mobiles/monster.lua` is the script of the monsters that go for the players,
after ModernUO's melee AI: it chases a player and fights it from beside it. A template takes it with
`script_id = "monster"`; the creatures whose UOX3 `NPCAI` is evil, evil caster or chaotic do (the
orcs, the ogres, the lizardmen, the dragons, the undead and the other 200 or so templates and those based on
them; [UOX3 migration](../uox3-migration.md)); the good fighters and casters, who fight criminals only, do not. The casters, among them the wraith, the spectre and the lich, are
casters in ModernUO: they walk up and fight like the others until magic exists. The behaviour is the shared module
[`common/creature.lua`](#commoncreaturelua), `creature.new({ hunts = true })`. A monster is in one of three states:

| State | What it does | It ends when |
| --- | --- | --- |
| wander | Strolls in its home, the area of its spawn region: about a step every two seconds, mostly straight ahead. It strolls with `npc.wander`, which walks it back from outside, as after a chase. One think in twenty it rests 15 to 25 seconds, with its `idle` sound and a fidget | It sees a player |
| chase | Threatens the player with its `start_attack` sound and an animation, goes into war mode and walks to it with `npc.walk_to`, a step every think, never running. Beside it, it faces it and fights it with `combat.attack`, once: the swings, the hits and the [death](../combat.md) are the combat service's | The player hides, leaves, is farther than 32 tiles, or cannot be reached for 20 seconds |
| guard | Stops fighting, stands in war mode for 10 seconds, looking around | It sees a player, or the time is over: back to wander, in peace |

A monster that is hit, or missed, fights back (the [combat service](../combat.md) sees to it) and turns on the one who fights it
whatever it was doing, wandering or on guard, even if it had not seen it: it goes to war mode and chases it, without
threatening it again.

It looks for prey every two seconds while it wanders and every second on guard, among the first six of
`npc.mobiles_in_sight`: within 16 tiles and in line of sight, from eye to eye, nearest first. Its prey is any player
and, of the NPCs, the ones with a blue name, the townsfolk, `mobile.notoriety` innocent: a monster in a town goes for
the people in the street as well as for the players. It leaves alone the yellow ones, the vendors, the bankers and
the guards, that cannot be hurt, and the other creatures, animals or monsters. It never sees a hidden player, a ghost, a
game master or an administrator. Once it chases its prey it follows it without seeing it (`npc.can_see` with
`in_sight` false), up to the leash. Prey it could not reach is left alone until it moves. Its threat and its fidget
are the actions of a monster body; a creature with a human or an animal body, such as a brigand, plays none, since
those bodies number their actions otherwise. What a monster is doing is kept in memory by its serial, not
saved, and forgotten when it dies: after a restart, or once no player is near enough for it to think, it starts again
from wandering. The numbers (16, 32, the times) are constants at the top of the file.

**The town guards go for the monsters**, as ModernUO's: `guard.lua` counts as wanted any NPC whose
`npc.script_id` is `monster` that stands in a guarded region, as a criminal. The guard appears beside it, strikes it
and kills it with one blow.

## guard.lua

The distribution's `scripts/mobiles/guard.lua` is the script of the town guards: the ones that stand in
the towns by their spawn, and the ones a player calls by saying "guards" (see
[`ultima.crime`](../server-configuration.md)). A template takes it with `script_id = "guard"`; `guard`,
`m_guard` and `f_guard` do. The server has no combat yet: a guard kills a criminal that is an NPC with
one blow, as ModernUO's does, and only stands on one that is a player, since players do not die yet. A
guard is in one of two states:

| State | What it does | It ends when |
| --- | --- | --- |
| post | Strolls around its post, the area of its spawn region, about a step every four seconds, with `npc.wander`, which also walks it back from outside | It sees a criminal: to arrest |
| arrest | Goes into war mode; when it is not beside the criminal it appears on a free tile a step from it (`world.spot_beside`; on it when none is free), with a puff of smoke where it stood and where it comes and the teleport sound; says "Thou wilt regret thine actions, swine!" (message 30138). Then it stays on the criminal, facing it, and runs after it with `npc.walk_to` when it moves. Beside a criminal NPC it strikes (an attack animation) and a second later the NPC is dead: `mobile.kill`, with the guard as its killer, so it [dies as any other](../death.md) and leaves its corpse | The NPC is killed; or the criminal is pardoned or its time is over, hides, leaves the guarded region, goes farther than 24 tiles from the guard or from its post, or cannot be reached for 10 seconds: back to its post, in peace |

It looks for a criminal every second: the nearest player or NPC of `npc.nearby` within 12 tiles whose
`mobile.criminal` or `mobile.is_murderer` is true (a red name is wanted like a grey one), that stands in a guarded region (`world.is_guarded`) no farther than 24
tiles from the guard's post (`npc.home`), and that it sees (`npc.can_see`); only a criminal costs the
look along the line of sight. The post is the measure, not the guard, so a criminal cannot lead a guard
out of town step by step. A criminal it could not reach is left alone until it moves. It never sees a
hidden player, a game master or an administrator. An NPC it has just killed is still there while it
falls: the guard does not turn on it again. A teleport that is refused leaves the guard where it is, to
run to the criminal.

**The archer guard**, template `archerguard` (the guards called in Ilshenar and Malas, after ModernUO's `ArcherGuard`, without its horse and its stats: it is the guard's stats with a bow), is
this guard with a bow in its hands and `combat.range` of 10. At an NPC it goes for, a criminal or a monster, that is
within its range and in its sight, it does not come beside it: it stands where it is, faces it and starts the fight
(`combat.attack`), and the combat service shoots with the Archery skill; the arrest is over when the target is dead.
Out of its range, or out of its sight, it comes beside the NPC as any guard does, and shoots from there. At a player it
is the same guard as the others: it comes beside it and stands on it.

A guard that was called bears the prop `guard.summoned`: it came beside its criminal and said its line
already, so it stays on it in silence and does not stroll, and once that criminal is let go it arrests
no other and waits to be sent away. What a guard is doing is kept in memory by its serial, not saved.
The numbers (12, 24, the 10 seconds) are constants at the top of the file.

## animal.lua and scared_animal.lua

The animals, after ModernUO's animal AI, use the same module with `hunts = false`: they stroll in their home, rest now and then
with their idle sound and a fidget, and never go for a player. `scripts/mobiles/animal.lua` (`script_id = "animal"`, the
creatures whose UOX3 `NPCAI` is 6: bears, wolves and the like) fights back when it is hit, as a monster does.
`scripts/mobiles/scared_animal.lua` (`script_id = "scared_animal"`, `NPCAI` 12) does not: a scared animal that is hit
stops fighting and runs, up to twelve cells and ten seconds at most, away from whoever hit it, then strolls again. The
combat service makes every hit NPC answer the blow except one whose template has this script, which only runs.

## common/creature.lua

`scripts/common/creature.lua` is what the three scripts above share, taken with
`local creature = require("common.creature")`. `creature.new(options)` gives the table a mobile script defines, with its
`on_think`; the options are `hunts` (it goes for the players it sees), `flees` (it runs from a blow instead of
fighting back) and `flee_at` (see below).

**Archers.** A creature that holds a bow or a crossbow has `combat.range` of 10 or 8 ([Combat](../combat.md#archers)):
its chase stops where the prey is within that range and in its line of sight, `npc.can_see`, faces it and starts the
fight, and the combat service shoots. Too far, it walks until the prey is a step inside its range; within range but
with no line of sight, it comes closer. It does not step back from a prey that comes close: ModernUO's archers do not
either, unless their constructor asks.

**Running when hurt**, as ModernUO's creatures: one that fights (not a scared animal, which runs from a blow) and has
under `flee_at` percent of its hit points runs with a chance of one in ten at each think, for 10 to 30 seconds,
straight away from its prey, running. The percent is the template's `flee_at`, else the option of the script: 20 for a
monster, 10 for an animal; a template with `flee_at = -1`, as UOX3's `FLEEAT=-1` gives the undead, the elementals and the
daemons, never runs. While it runs it does not answer a blow: it sets the NPC prop `combat.passive`, which the combat
service reads, and it goes back to strolling, and so to hunting again, when it is over. A creature of your own takes it the same way: `mycreature = creature.new({ hunts = true })` in
`scripts/mobiles/mycreature.lua`. A root whose scripts are replaced by hand needs `scripts/common/` too, or the creatures
stop thinking.

## orione.lua and vega.lua

The repository also ships two cats of Moongate v2, `orione` and `vega` (`templates/mobiles/moongate_cats.toml` with `scripts/mobiles/orione.lua` and `vega.lua`): spawn them with `.spawn orione` or `.spawn vega`.

## door.lua

The distribution also ships `scripts/items/door.lua`, the script of the `decoration_door`
template that [`.decorate`](../commands/decorate.md) gives to doors and gates. Double clicking
a closed door opens it and its linked door (prop `door.link`): the graphic goes to the next
one, the door swings aside by its `facing` prop and plays the sound of its
`decoration_type` (metal, wood, gate or secret). Double clicking an open door closes both
when nobody stands in either doorway. An open door closes by itself after 20 seconds, then
tries again every 10 seconds while the doorway is taken. A door that cannot swing aside, such
as one at the edge of the map, stays closed. The open state is the prop `door.open`, with the
closed spot in `door.x`, `door.y` and `door.z`, saved with the door, and so is the auto-close timer (the door's `close` timer, started with
`item.start_timer`): a door left open when the server stops closes once the server is back. A door saved
open by an older version has no timer and stays open until someone uses it. A closed door with the prop `locked` does not
open for players, who read "That is locked." (message 398, in the server language), unless
they carry anywhere in their backpack a key whose prop `key.value` is the door's `key.value`
(message 405: they open it and it stays locked); game masters and administrators open it
(message 404). The prop comes from the decoration data
(`props = { facing = "west_cw", locked = true }`), such as the side doors of the New Haven
bank. `.lock` gives a door a key number and `.key` makes its key.

## light.lua

`scripts/items/light.lua` lights and douses candles, candelabras, lanterns, lamp posts, wall
sconces and torches: the `decoration_light` template and the light templates of
`templates/items` use it. Double clicking an unlit light gives it the lit graphic (ModernUO's
pairs), a light shape if it has none, and sound `0x47`; double clicking a lit one gives the
unlit graphic and sound `0x3BE`, keeping the shape for the next time. A light without an unlit
graphic, such as a brazier, stays as it is. The lights `.decorate` places have the prop
`protected`: only game masters and administrators light or douse them. The town lamp posts
light and douse themselves: every 30 seconds the server calls `on_darkness(serial, dark)` on a
lamp post whose spot turned dark or light (`ultima.world.lamp_post_light`), and `light.lua`
switches its graphic silently.

## food.lua

`scripts/items/food.lua` is the script of what can be eaten, as ModernUO's `Food`: the converted
food templates carry `script_id = "food"`. Double clicking a piece eats one: the player's hunger rises
by the item's prop `food.fill` (3 without it), 20 at most; it gets 6 to 8 points of stamina back, makes
the sound and, with a body of the `Human` kind (`mobile.body_type`), the gesture of eating, and reads how full it feels
in the language of its client (messages 500868 to 500872). A full player reads "You are simply too full
to eat any more!" (500867) and eats nothing.

## drink.lua

`scripts/items/drink.lua` is the script of what can be drunk, as ModernUO's beverages: the converted
drink templates carry `script_id = "drink"`. Double clicking one drinks a sip: the player's thirst rises
by the item's prop `drink.fill` (3 without it), 20 at most, and it makes the sound and, with a body of
the `Human` kind, the gesture of drinking. The graphic says how many sips a full container holds: a
pitcher or a bottle 5, a jug 10, a glass or a mug 1; the sips left are kept in the prop `drink.uses`.
Once empty, a pitcher, a glass or a mug turns into its empty graphic, is renamed and stays; a bottle or a jug
is gone. A quenched player reads "You are simply too full to drink any more!" and drinks nothing.
Refilling, pouring and drunkenness are not there yet.

## bandage.lua

`scripts/items/bandage.lua` is the script of the clean bandage (`0x0e21_clean_bandage`, `script_id = "bandage"`),
as ModernUO's classic Healing. Double click it, pick who it is for and wait: the one picked is healed, or raised.

- **Reach:** the bandage in the backpack, and who it is for within 1 tile; farther, the client's "too far away".
  The healer must see who it is for: a hidden one or one behind a wall "can not be seen". Using a bandage reveals a
  hidden healer. The bandage is taken out of the stack when the healing begins. A second bandage of the same healer replaces the
  first.
- **The wait:** 3 seconds for a healer with 100 dexterity or more, 4 from 40, 5 under it; 5 more to raise a
  ghost; 9.4 + 0.6 × (120 − dexterity) / 10 on itself. The healer has to stay within 1 tile and alive, or the
  healing is lost with its bandage.
- **A living one that is hurt:** it works with a chance of (Healing + 10) %, and heals from
  Anatomy / 5 + Healing / 5 + 3 up to Anatomy / 5 + Healing / 2 + 10 points; a roll under 1 heals 1 and says the
  bandages barely helped. A creature with the body of a monster or an animal is a case for Veterinary and Animal
  Lore, with a point more per 100 of its hit points. One that is not hurt reads "That being is not damaged!" and
  keeps the bandage.
- **A ghost:** it needs 80 points of Healing and of Anatomy and a chance of (Healing − 68) / 50; then the ghost
  is asked in the gump of the ankhs whether to come back, and it costs a tenth of its fame, as at an ankh.
- **Skills:** both are tried for a rise after a healing, whether the roll worked or not, and after a raise that
  worked.

There is no poison and no bleeding in the game yet, so there is no cure; and the ground where a ghost is raised is
not checked, as it is not at an ankh.

## dyes.lua and dye_tub.lua

`scripts/items/dyes.lua` and `scripts/items/dye_tub.lua` dye clothes in two steps, as ModernUO does;
the converted templates of the dyes (`0x0fa9_dyes`) and of the tub (`0x0fab_dying_tub`) carry
`script_id = "dyes"` and `script_id = "dye_tub"`.

1. Double click the dyes and pick a dye tub: the client's hue picker opens with the tub in it, and
   the tub takes the hue picked, from 2 to 1001, as its own hue.
2. Double click the tub and pick what to dye: it takes the hue of the tub, with the sound of dyeing
   (`0x23E`).

What can be dyed is an item whose template says [`dyeable = true`](../templates.md), as the clothing
converted from UOX3 does. It must not be worn, and the player must reach it, the tub and the dyes:
carried, or on the ground within 1 tile. Neither the dyes nor the tub is used up, and a tub never
dyed has hue 0, which takes the colour off. An item held on the cursor is not dyed ("You can not dye
that."), and one inside a container on the ground counts as too far: take it first.

The texts are the client's own, read in its language:

| Text | Cliloc |
| --- | --- |
| Select the dye tub to use the dyes on. | 500856 |
| Use this on a dye tub. | 500857 |
| Select the clothing to dye. | 500859 |
| Can't Dye clothing that is being worn. | 500861 |
| You can not dye that. | 1042083 |
| That is too far away. | 500446 |

The player may answer the hue picker much later, or never. When the answer comes the script checks
again that the dyes and the tub are within reach, and the server takes an answer only for the picker
it opened: the other emulators take it as it comes. `scripts/common/dye.lua` holds what the two
scripts share (`dye.reach`, `dye.worn`, `dye.tell`). The special tubs (leather, furniture, black,
metallic) and the hair dyes are not there yet.

## hiding.lua

`scripts/skills/hiding.lua` is the [skill script](../skills.md) of Hiding, as ModernUO's without
what needs a fight or a house. A player that uses the skill is tried at it with
`skill.check(user, "hiding", 0, 100)`: the chance is its points in a hundred, and the try may raise
the skill.

- Passed: the player is hidden (`mobile.set_hidden`), out of war mode, and reads "You have hidden
  yourself well." (cliloc 501240).
- Failed: the player is shown, also when it was hidden, and reads "You can't seem to hide here."
  (501241).

Either way it waits before another skill the `delay` of `hiding` in
[`data/skills.toml`](../data-files/skills.md), 10 seconds. Its first step shows it again, with "You have
been revealed!" (500814): the server does that for every hidden player of a regular account, since
there is no Stealth yet; a turn on the spot does not. The staff hides to watch and stays hidden.
Speaking, being hit and the sight of who stands near do not show it yet.

## Regeneration props

Hit points, mana and stamina come back by themselves (see
[`ultima.regeneration`](../server-configuration.md)). A script changes the rate of one mobile with its
props, in seconds for a point: `mobile.set_prop(who, "regen.hits", 2)` heals it five times faster than
the default; `nil` gives it the configured rate back. The props are `regen.hits`, `regen.mana` and
`regen.stamina`.

Moving takes stamina from a player (see `fatigue_enabled` in
[`ultima.regeneration`](../server-configuration.md)): running, and every step when it carries more than
`mobile.max_weight`. A script that gives or takes items changes what the mobile carries at once; the
status bar of its player follows at the next status update.

## common/teleport.lua

The two teleporter scripts below share `scripts/common/teleport.lua`, a Lua module they take with
`local teleport = require("common.teleport")`: `teleport.send(serial, who)` sends a mobile where the
item's props say (`teleport.x`, `teleport.y`, `teleport.z`, `teleport.map`), with the smoke of
`source_effect` and `dest_effect` and the sound of `sound_id`, and `teleport.is_on(value)` reads a flag
the decoration files carry as text. A script of your own that teleports can take it the same way.
A script keeps the module it took: after `script reload common/teleport.lua`, reload the scripts that
use it too (see [Reload and ownership](runtime.md#reload-and-ownership)). `mgctl init` adds `scripts/common/` to an
existing root and keeps the scripts already there; a root whose item scripts are replaced by hand needs
`scripts/common/` as well, or its teleporters stop.

## common/numbers.lua

`scripts/common/numbers.lua` is how the shipped scripts write a number a player reads:
`numbers.with_thousands(1234567)` gives `"1,234,567"`. The banker and the bank check take it with
`local numbers = require("common.numbers")`, and a script of your own can do the same. A root whose
scripts are replaced by hand needs it too, or its bankers and its bank checks stop.

## teleporter.lua

`scripts/items/teleporter.lua` is the script of the `decoration_teleporter` template that
[`.decorate`](../commands/decorate.md) gives to ModernUO's `Teleporter`: on `on_move_over` it
teleports the player to the props `teleport.x`, `teleport.y` and `teleport.z` with
`mobile.teleport`, shows a puff of smoke where the player left (prop `source_effect`) and
arrived (prop `dest_effect`), then plays the prop `sound_id` there when the teleporter has one. The prop
`active = false` turns a teleporter off. Only players travel, unless the prop `creatures` is true: then an NPC that steps on it travels too. A teleporter with the prop `teleport.map`, a `MapType`
number, takes the player to that map: the client changes map, then gets the season when it differs
from the one it shows, the light, the weather and the music of the place; when the map is not loaded nothing happens. The template has `visibility = "game_master"`: a ground item is sent only to
the accounts its visibility allows, so players walk onto a teleporter they never see.

## keyword_teleport.lua

`scripts/items/keyword_teleport.lua` is the script of the `decoration_keyword_teleporter`
template that `.decorate` gives to ModernUO's `KeywordTeleporter`, such as the mantra of a
shrine: on `on_speech` it teleports the player who says the prop `substring` (found anywhere in
the text, in any case) or whose client sends the speech keyword of the prop `keyword`, standing
within `range` cells (0, the default, is the teleporter's own cell). With a `delay`
(`"0:0:1"`, or a number of seconds) the teleport happens later, if the player still stands in
range. The destination, the smoke, the sound and `active` are those of the plain teleporter.

## public_moongate.lua

`scripts/items/public_moongate.lua` is the script of the `decoration_public_moongate` template
that `.decorate` puts on every destination of [`moongates.toml`](../data-files/moongates.md), as
ModernUO's `PublicMoongate`: on `on_move_over`, and on `on_use` from the next cell, it builds a
gump with `gump.create`, one page per map of `moongates.facets()` and one button per city, the
page of the player's own map first, and plays the sound `0x20E`. A button teleports the player
with `mobile.teleport`, to another map too, and plays `0x1FE` there. A player who walked more
than a cell away while the gump was open is told so and stays; choosing the city of the gate
itself does nothing.

## healer.lua

`scripts/mobiles/healer.lua` is the script of the healers (`script_id = "healer"`): on every think it asks
`npc.ghosts_in_sight(serial, 4)` for the ghosts within 4 cells in its line of sight. A ghost that was not
there at the last think is turned to (`npc.look_at`), gets the sound `0x1F2` and the sparkles `SparkleHeal`, and
the gump `resurrect` with the argument `healer`, as ModernUO's `BaseHealer`. A healer waits 2 seconds (4 thinks)
between two offers, and a ghost met during the wait is offered when it is over. A criminal is refused with the
client text 501222 and a murderer (red) with 501223, and a player of negative karma is told 501224 and offered all
the same. An evil healer, whose template id starts with `evil` (`evilhealer`, `evilwhealer`), refuses
nobody and says nothing. A healer of a
template ending with `whealer`, a wandering one, takes a step with `npc.wander` every fourth think.

## ankh.lua and resurrect.lua

`scripts/items/ankh.lua` is the script of the `decoration_ankh` template, the two pieces of each
`AnkhWest` and `AnkhNorth` that `.decorate` places. It has no `on_use`: the living have nothing to do
with an ankh. Its `on_ghost_use` runs when a dead player double clicks it ([Death and
resurrection](../death.md#death-of-a-player)): from more than 2 cells it says "That is too far away."
(client text 500446), else it opens the gump `resurrect` (`templates/gumps/resurrect.xml`). Its
Continue button calls `resurrect.accept` in `scripts/gumps/resurrect.lua`, which, if the player is
still dead and within 2 cells of the ankh (8 of the healer, when it was a [healer](#healerlua) that
asked), calls `mobile.resurrect`, plays the sound `0x214` and the effect `SparkleHeal` on the player and
takes a tenth of its fame; from five short-term murders it takes skills and stats too ([Murder counts](../death.md#murder-counts)).
Cancel does nothing.

## moongate.lua

`scripts/items/moongate.lua` is the script of the `moongate` template, the gate with one
destination that the command [`moongate`](../commands/moongate.md) puts at a game master's feet, as
ModernUO's `Moongate`. On `on_move_over`, and on `on_use` from the next cell, it waits one second
with `timer.after`, then takes the player, if it still stands there, to the props `teleport.x`,
`teleport.y` and `teleport.z`, on the map of the prop `teleport.map` (a `MapType` number or its
name; the player's own map without it), and plays `0x1FE`. A gate without the three
numbers, with a map that does not exist or is not loaded, or with a spot outside the map tells the
player "This moongate does not seem to go anywhere." (message 30114). Touching the gate again
during the second starts nothing. When the gate
stands in a guarded region and the destination does not (`world.is_guarded`), it asks first: a
gump with OKAY and CANCEL and the sound `0x20E`; OKAY from more than a cell away tells "That is
too far away." (message 393) with `mobile.message`. ModernUO's rules about sigils, young
players, murderers, casting, pets and dispelling the gate are not there yet.

## bulletin_board.lua

`scripts/items/bulletin_board.lua` is the script of the [bulletin boards](../bulletin-boards.md)
(the item template `bulletin_board` that `.decorate` places, and `0x1e5e_bulletin_board` and
`0x1e5f_bulletin_board` for a board added by hand). Its `on_use` calls `board.open(serial, user)`
and returns `true`: the player's client gets the board and the list of its messages, and from
there reads, posts, replies and removes its own. Any item template with this script is a board,
each item with its own messages.

## clock.lua

`scripts/items/clock.lua` is the script of the clocks (the item templates `0x104b_clock` and
`0x104c_clock`, and `decoration_clock` for those `.decorate` places), as ModernUO's `Clock`: on
`on_use` the player reads over the clock the part of the day ("It's the afternoon") and the time to
the minute ("1:07 to be exact") where they stand, from `world.time`, as texts of the client sent
with `item.message_cliloc`.

## fillable.lua

`scripts/items/fillable.lua` is the script of the `decoration_fillable` template that `.decorate`
gives to the town containers, ModernUO's `FillableContainer`: the crates, boxes, chests and barrels
of the shops and the bookcases of the libraries. On `on_use`, before the container opens, a
container whose time has come (prop `fill.next`, as `world.now()` counts) and that holds two things
or fewer, a pile counting for its amount, gets up to twice what it misses to hold three, each one a
roll of the loot table of its kind with `item.add_loot`; a bookcase fills up to five books. It then waits 60 to 90 minutes; a fill that could add nothing is tried again at the next opening.
Nothing runs while nobody opens the container, and the times survive a restart. The kind is the
prop `content_type`, such as `baker` for the table `fillable_baker` of
`templates/loots/fillable_containers.toml`; without it the container takes the kind of the nearest
vendor within 20 tiles, told by `mobile.template`, and keeps it. With no vendor around it stays
empty and looks again five minutes later. ModernUO starts the wait when an item is taken out, and
locks and traps the container: those are not there yet. The town tables use
`templates/loots/randomshields.toml` (one plain shield, ModernUO's `Loot.ShieldTypes`) and the two goods of
`templates/items/town_goods.toml` (mallet and chisel, arrow shafts) that the converted item files lack.

## gmtools.lua

`scripts/gumps/gmtools.lua` is the script of the gump of the game master's tools
(`templates/gumps/gmtools.xml`), which [`.gmtools`](../commands/gmtools.md) opens. The gump has two
slots, filled by two functions: `tools` draws the sidebar, a button for each entry of the table
`tools` in the script, and `panel` draws the panel of the selected one (`args.tool`, the first when
none or an unknown one is given). A click on the sidebar opens the gump again on that tool.

There are three tools, the weather, the season and the time. The weather panel reads `world.weather_profile` and `world.weather` and
has a button for each kind, `none`, `rain`, `snow` and `storm`, that calls `world.set_weather` on the
player, tells it `The weather of temperate is now storm until the next hour.` and opens the gump again.
The season panel reads `world.season_here` and the season of the map the player stands on
(`world.season` of `mobile.location(player).map`) and has a button for each season and one for
`auto`, that call `world.set_season` or `world.clear_season`, tell the player `The season of your map
is now winter.` and open the gump again. The time panel reads `world.time`, `world.moon`,
`world.light_here` and `world.global_light` and has a button for each of four light levels and one for
`auto`, that call `world.set_global_light` or `world.clear_global_light` and tell the player `The
global light is now 26.`. Staff only: the slots are empty for anyone else, and every
button checks `world.is_staff` again.

To add a tool, write a panel function with the signature `function(g, player)` and add
`{ id = "...", title = "...", panel = ... }` to `tools`.

## jail_sentence.lua

`scripts/gumps/jail_sentence.lua` is the script of the gump of the [jail](../jail.md)
(`templates/gumps/jail_sentence.xml`), which [`.jail`](../commands/jail.md) opens with no target,
and `.jail <name>` on the player of that name.
Its `rows` function fills the slot: first a button that gives the cursor with `target.pick` and opens
the gump again on the character picked, then the cells of `jail.cells()`, ten per page. With a
character picked, a free cell has a button that reads the days typed in the gump and calls
`jail.send(target, cell, days, who, reason)`, the reason being what is typed in the second field; days that are empty, not a number, a fraction or beyond
`jail.max_days()` jail nobody, and the gump opens again with the reason. A cell that holds someone
shows its name, the time left as `2d 4h`, `5h 10m` or `12m`, and a button that calls
`jail.release`. Every cell has a button that takes the game master into it with
`mobile.teleport(who, cell.x, cell.y, cell.z, cell.map)`, on the map of the jail. A target that is
already in jail has its release on a line of its own at the top.
A target that is not in the world, a player `.jail <name>` found, is shown as `(offline)`:
`mobile.name` is nil for it. `jail.send` then answers `JailResultType.Pending` and the game master
is told the cell is kept until the login; such a cell reads `waits for login`, from the `pending`
of `jail.cells()`. When the gump is opened with `candidates`, several players of one name, the
function lists them, ten at most, with their account and whether they are online, each with a
button that opens the gump on it, and leaves the cells out until one is picked.
Every button checks `world.is_staff` again: the rank may have gone while the gump was open.

## jail_note.lua

`scripts/items/jail_note.lua` is the script of the `jail_release_note` template, the note a
prisoner finds in its backpack when its sentence ends. On `on_use` it delegates to
`book.open`, displaying saved `book.content` or legacy `jail.text`: the days served, cell, dates
and fine paid. The shared parchment escapes plain text and scrolls long bodies. A note with no
text, such as one made with `.add`, shows nothing.

## readable_scroll.lua

`scripts/items/readable_scroll.lua` delegates double click to `book.open`. The unstackable
`readable_scroll` template is used by the [text catalog](../data-files/books.md).
Create a personalized letter with `book.give(player, "welcome_letter", { contact_name = "Vega" })`.
Its saved title, author and body remain fixed when another player reads it.

## readable_book.lua

`scripts/items/readable_book.lua` delegates double click to `book.open` as the scroll does; for an
item of the `readable_book` template `book.open` sends the client's book, its cover and every page,
instead of the parchment ([books and parchments](../data-files/books.md#books-and-parchments)).
The imported ModernUO texts use it: `book.give(player, "grammar_of_orcish")`.
