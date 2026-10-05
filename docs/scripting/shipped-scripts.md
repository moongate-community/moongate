# Shipped scripts

This page is part of [Writing Lua scripts](../scripting.md). The distribution ships these scripts under
`scripts/`, and `mgctl init` copies them into the root: each one is an example to read and to change. How a
script is bound to a template is told in [Mobile scripts](mobile-scripts.md) and [Item scripts](item-scripts.md),
where `wander.lua` and `potion.lua` are listed.

## monster.lua

The distribution's `scripts/mobiles/monster.lua` is the script of the monsters that go for the players,
after ModernUO's melee AI without the fight: the server has no combat yet. A template takes it with
`script_id = "monster"`; the undead of the graveyards do (`skeleton`, `zombie`, `ghoul`, `headless`, `wraith`,
`spectre`, `lich`), and so the templates based on them. The wraith, the spectre and the lich are casters in
ModernUO: they walk up and snarl like the others until magic exists. A
monster is in one of three states:

| State | What it does | It ends when |
| --- | --- | --- |
| wander | Strolls in its home, the area of its spawn region: about a step every two seconds, mostly straight ahead. It strolls with `npc.wander`, which walks it back from outside, as after a chase. One think in twenty it rests 15 to 25 seconds, with its `idle` sound and a fidget | It sees a player |
| chase | Threatens the player with its `start_attack` sound and an animation, goes into war mode and walks to it with `npc.walk_to`, a step every think, never running. Beside it, it faces it and snarls every three seconds (`attack` sound and an attack animation): it does no harm | The player hides, leaves, is farther than 32 tiles, or cannot be reached for 20 seconds |
| guard | Stands in war mode for 10 seconds, looking around | It sees a player, or the time is over: back to wander, in peace |

It looks for a player every two seconds while it wanders and every second on guard, and takes the
nearest one of `npc.players_in_sight`: within 16 tiles and in line of sight, from eye to eye. It never
sees a hidden player, a game master or an administrator, and it ignores NPCs. Once it chases a player
it follows it without seeing it (`npc.can_see` with `in_sight` false), up to the leash. A player it could not
reach is left alone until it moves. What a monster is doing is kept in memory by its serial, not
saved: after a restart, or once no player is near enough for it to think, it starts again from
wandering. The numbers (16, 32, the times) are constants at the top of the file.

## guard.lua

The distribution's `scripts/mobiles/guard.lua` is the script of the town guards: the ones that stand in
the towns by their spawn, and the ones a player calls by saying "guards" (see
[`ultima.crime`](../server-configuration.md)). A template takes it with `script_id = "guard"`; `guard`,
`m_guard` and `f_guard` do. The server has no combat yet, so a guard only shows itself. A guard is in one
of two states:

| State | What it does | It ends when |
| --- | --- | --- |
| post | Strolls around its post, the area of its spawn region, about a step every four seconds, with `npc.wander`, which also walks it back from outside | It sees a criminal: to arrest |
| arrest | Goes into war mode; when it is not beside the criminal it appears on it, with a puff of smoke where it stood and where it comes and the teleport sound; says "Thou wilt regret thine actions, swine!" (message 30138). Then it stays on the criminal, facing it, and runs after it with `npc.walk_to` when it moves | The criminal is pardoned or its time is over, hides, leaves the guarded region, goes farther than 24 tiles from the guard or from its post, or cannot be reached for 10 seconds: back to its post, in peace |

It looks for a criminal every second: the nearest player of `npc.nearby` within 12 tiles whose
`mobile.criminal` is true, that stands in a guarded region (`world.is_guarded`) no farther than 24
tiles from the guard's post (`npc.home`), and that it sees (`npc.can_see`); only a criminal costs the
look along the line of sight. The post is the measure, not the guard, so a criminal cannot lead a guard
out of town step by step. A criminal it could not reach is left alone until it moves. It never sees a
hidden player, a game master or an administrator, and it does not arrest NPCs. A teleport that is
refused leaves the guard where it is, to run to the criminal.

A guard that was called bears the prop `guard.summoned`: it came onto its criminal and said its line
already, so it stays on it in silence and does not stroll, and once that criminal is let go it arrests
no other and waits to be sent away. What a guard is doing is kept in memory by its serial, not saved.
The numbers (12, 24, the 10 seconds) are constants at the top of the file.

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
prisoner finds in its backpack when its sentence ends. On `on_use` it opens a gump built in Lua
with the text of the prop `jail.text`, which the jail wrote: the days served, the cell, the dates
and the fine paid. A note with no text, such as one made with `.add`, shows nothing.
