# Writing Lua scripts

Put scripts under `<root>/scripts`. The default bootstrap is `init.lua`, selected
by `[scripting].bootstrap_file` in [server configuration](server-configuration.md).
The host uses Lua 5.2 through LuaCSharp; execution and resumes occur on the game
loop. No separate Lua installation is required.

## First script and modules

Create `scripts/common/greeting.lua`:

```lua
local greeting = {}

function greeting.for_name(name)
    return "Welcome, " .. name
end

return greeting
```

Create `scripts/init.lua`:

```lua
local greeting = require("common.greeting")
log.info("{Message}", greeting.for_name("Moongate"))
log.info("{Engine} {Version} ({Codename}) on {Platform}",
    engine.name, engine.version, engine.codename, engine.platform)

local pulse = timer.every(30, function()
    log.info("Pulse")
    wait(2)
    log.info("Pulse resumed")
end)

timer.after(95, function()
    log.info("Pulse cancelled: {Cancelled}", timer.cancel(pulse))
end)
```

Start the server or enter `script reload init.lua` in its console. `require` maps
dotted module names to relative `.lua` paths: `common.greeting` loads
`common/greeting.lua`. It caches the module result. Resolution stays inside the
scripts directory, including checks against symbolic links escaping that root.
A missing bootstrap logs a warning and starts an empty engine. A bootstrap that
exists but fails compilation/execution aborts server startup.

## Available host functions

| API | Purpose |
| --- | --- |
| `engine.name`, `.version`, `.codename`, `.platform` | Read-only engine metadata |
| `log.debug/info/warning/error(template, ...)` | Structured Serilog events; extra arguments fill template properties |
| `log.LEVEL_DEBUG/.LEVEL_INFO/.LEVEL_WARNING/.LEVEL_ERROR` | Numeric constants for the matching Serilog level |
| `print(...)` | Tab-separated values written to the server log at Information level |
| `timer.after(seconds, fn)` | One callback after a positive delay; returns a cancellation handle |
| `timer.every(seconds, fn)` | Repeating callbacks with a positive interval; returns a handle |
| `timer.cancel(handle)` | Cancels a pending registration; returns false if no timer remains |
| `wait(seconds)` | Parks the current scheduled coroutine, then resumes it on the loop |
| `events.on(name, fn)` | Runs `fn` with the event's table each time the named server event happens; returns a handle. See [Events](#events) |
| `events.off(handle)` | Removes a subscription; returns false when the handle is unknown |
| `dice.roll(expression)` | Rolls dice notation such as `"1d4+2"` or `"4d6k3"`, the forms of [DiceSpec](toml-types.md#dicespec); a malformed expression raises an error naming it |
| `dice.try_roll(expression)` | The same roll, or `nil` when the expression is malformed: `dice.try_roll(text) or 0` |
| `localization.get(id, ...)` | Message `id` of `data/messages` in the server language, with `{0}`, `{1}`, ... filled by the extra arguments; see [Localization](localization.md#read-a-message-from-lua) |
| `localization.text(id)`, `localization.language()` | The raw text of a message, or `nil`; the server language code |
| `npc.say(serial, text)` | The NPC says `text` overhead to the players within 15 cells (cut to 128 characters); `false` for blank text or a serial that is not an NPC in the world |
| `npc.play_sound(serial, sound)` | Plays a sound where the NPC stands, for the players within 15 cells (0x54): a sound id from 0 to 65535, such as `0x69`, or a kind of the NPC template's `[mobile.sounds]`, `"start_attack"`, `"idle"`, `"attack"`, `"hurt"` or `"death"`, so a script makes each creature sound like itself; `false` for a sound out of range, a kind its template does not set, or a serial that is not an NPC in the world |
| `npc.step(serial, direction, running?)` | One step toward a `DirectionType` (`North` to `NorthWest`), turning first when needed, seen by the players in range; a run when `running` is `true`. How often the script calls it sets the speed. `false` when blocked or for `DirectionType.Running`, which is not a direction |
| `npc.location(serial)`, `npc.name(serial)` | `{ x, y, z, map }` and the name of the NPC, or `nil` |
| `npc.get_prop(serial, key)`, `npc.set_prop(serial, key, value)` | A value the NPC keeps across restarts: a string, a number or a bool, saved with the NPC by the world save; `get_prop` gives `nil` when it has none, `set_prop` with `nil` removes it and gives `false` for a table, a function or a blank key |
| `npc.spawn(template, map, x, y, z, fn?)` | Brings a new NPC of a mobile template into the world. The NPC is saved first, so it appears a moment later: its `on_spawn` runs then, and so does `fn(serial)` when given. `false` for an unknown template, a spot outside the map or a `z` outside -128 to 127 |
| `npc.delete(serial)` | Deletes the NPC and what it carries, on the next turn of the game loop; `false` for a serial that is not an NPC in the world |
| `npc.face(serial, x, y)`, `npc.distance_to(serial, x, y)` | Turns the NPC towards a place without stepping, seen by the players in range (`false` for a frozen NPC); and the tiles between the NPC and a place, the larger of the two differences, as the view range counts them |
| `npc.nearby(serial, range, kind?)` | The serials of the mobiles within `range` tiles (0 to 32) of the NPC, itself left out, nearest first, as a list: the other NPCs, or with `kind` `"players"` the players, with `"all"` everyone. Height and line of sight are not checked: `for _, other in ipairs(npc.nearby(serial, 8)) do ... end`. Empty for a serial that is not an NPC or a range out of bounds |
| `npc.home(serial)`, `npc.wander(serial)` | The home of an NPC of a spawn region, the area it was spawned in, as `{ x1, y1, x2, y2 }` (`nil` without one); and one stroll step, as ModernUO's wander: two times in three straight ahead, else another way. An NPC with a home keeps to it, and from outside it walks back along a path, with a step at random when none is found so a wall does not hold it. `false` when it did not move, as in a home of one cell |
| `npc.can_see(serial, other, range?, in_sight?)`, `npc.players_in_sight(serial, range?, limit?)` | Whether the NPC sees a mobile: on its map, within `range` tiles (default 16, 0 to 32), not hidden, not a game master or an administrator, and in line of sight from eye to eye; with `in_sight` `false` the line of sight is not checked, for what it keeps following once it saw it. A line of sight is never checked beyond `ultima.line_of_sight.max_distance` (25), so nothing is seen farther. And the players it sees, nearest first, as a list, `limit` of them at most: `local prey = npc.players_in_sight(serial, 16, 1)[1]` |
| `npc.walk_to(serial, x, y, z?, range?, running?)` | One step along a path to a place that goes around what stands in the way; call it on every `on_think`. It answers `"moving"` after a step, `"arrived"` once within `range` tiles of the place (default 0), `"blocked"` while it waits to look for another way, `"no_path"` when none was found; `nil` for a serial that is not an NPC. See [Walking a path](#walking-a-path) |
| `npc.find_path(serial, x, y, z?, partial?)` | The steps from the NPC to a place, as a list of `DirectionType`, for a script that walks them itself with `npc.step`; with `partial`, the steps to the closest place when it cannot be reached. `nil` when there is no path or the place is too far. Each call searches: keep the list |
| `item.name(serial)`, `item.amount(serial)`, `item.owner(serial)` | The item's name (its template id when it has none), its amount, and the serial of the mobile carrying or wearing it (`nil` on the ground); `nil` for an unknown item |
| `item.consume(serial, amount?)` | Takes `amount` units (default 1) off the item, deleting it at 0, and updates the owner's container or the players around a ground stack; `false` for a worn item, an `amount` below 1, fewer units left, or an item a player holds on the cursor |
| `item.get_prop(serial, key)`, `item.set_prop(serial, key, value)` | The same for an item, saved with it by the world save or its owner's save |
| `item.item_id(serial)`, `item.set_item_id(serial, graphic)` | The item's graphic, and changing it (0 to 65535), as a door opening: the players around a ground item, or the owner of a carried one, see it change; `false` for an unknown, worn or held item or a graphic out of range; an item inside a container on the ground changes without being shown again |
| `item.set_light(serial, type)` | The light shape a light source gives, by `LightType` name such as `circle150`, `circle300` or `west_big`; `nil` clears it. The players who see the item are shown it again; the client draws the light only for a lit graphic. `false` for an unknown shape or a worn or held item |
| `item.location(serial)`, `item.move_to(serial, x, y, z)` | Where a ground item lies, `{ x, y, z, map }`, and moving it on its map: the players around the old spot lose it and those around the new one see it; `nil`/`false` for an item not on the ground, a spot outside the map or a `z` outside -128 to 127; moving restarts a decaying item's decay |
| `item.play_sound(serial, sound)` | Plays a sound id (0 to 65535) where the item lies, or where the mobile carrying it stands, for the players within 15 cells; `false` for an unknown item, a sound out of range, or an item inside a container on the ground |
| `item.give(mobile, template, amount?)` | Makes a new item from an item template in the mobile's backpack and gives its serial; the owner sees it at once and its next save keeps it. `nil` for an unknown mobile or template, a mobile without a backpack, an amount the template cannot have (more than 1 of what does not stack) or when the server has no serial ready: it keeps 64 in reserve and refills them in the background, so a script that makes more than that in one go gets `nil` for the rest and must try again later |
| `item.add_loot(container, table, rolls?)` | Rolls a loot table of `templates/loots` once, or `rolls` times, and puts what it gives into a container, or into the backpack of a mobile; returns how many items it added, `0` for rolls that give nothing, an unknown table or something that is no container; fewer than the roll gave when the container is full (125 items) or the server has no item serial at hand for a moment |
| `item.create(template, map, x, y, z, amount?)` | Makes a new item from an item template on the ground and gives its serial; the players around see it. `nil` as `item.give`, and for a spot outside the map or a `z` outside -128 to 127 |
| `item.template(serial)`, `item.hue(serial)` | The id of the item's template and its hue (0 for the colours of its art); `nil` for an unknown item |
| `item.set_name(serial, name?)`, `item.set_hue(serial, hue)`, `item.set_amount(serial, amount)` | Give the item a name of its own (`nil` takes it back to its template's), a hue (0 to 65535) or, for a stack, an amount (1 to 60000); the players who see the item see it change. `false` for a held or worn item, or one that does not stack (amount above 1), as its template or its graphic says |
| `item.container(serial)`, `item.contents(serial)` | The serial of the container the item lies in (`nil` on the ground or worn), and the serials of the items lying directly in a container, as a list: `for _, inside in ipairs(item.contents(bag)) do ... end` |
| `item.equip(serial, mobile)` | Puts the item on a mobile, on the layer its template gives it; everyone around sees it worn and its script runs `on_equip`. `false` for a worn or held item, a stack, an item without a layer or one the mobile cannot wear, a taken layer, a mobile not in the world, or an item another mobile carries. The item's `can_equip` is not asked. Taken from a chest on the ground, those who look into the chest see it go |
| `item.find(holder, template)` | The serials of the items of a template inside a container, at any depth, or among everything a mobile wears and carries (not what lies in its bank), as a list: `for _, coins in ipairs(item.find(user, "gold")) do ... end`; empty when there is none |
| `item.start_timer(serial, name, seconds)`, `item.stop_timer(serial, name)`, `item.timer(serial, name)` | A timer the item keeps: when its time comes its script runs `on_timer(serial, name)`. It is saved with the item, so it also runs after a restart. Starting a running timer starts it again from now; `item.timer` gives the seconds left (`nil` when there is none). `false` for an unknown item, a blank name or one over 32 characters, or seconds not above 0 or over a year. The timers are kept in the props `timer.<name>`, which `item.set_prop` refuses; splitting a stack leaves them with the part that is lifted. A timer whose script has no `on_timer`, or fails, is dropped with a warning in the log |
| `prompt.ask(player, fn)`, `prompt.cancel(player)` | Ask the player for a line of text, typed in the journal line, and run `fn(text)` with it: up to 128 characters, without the spaces around it, or `nil` when the player pressed escape, typed only spaces, was asked something else or left. Say what to type first with `mobile.message`. `false` for an NPC or a player not in the world |
| `item.move_into(serial, container)` | Moves the item into a container, or into the backpack of a mobile when `container` is a mobile's serial; those who saw it lose it and the new owner sees it. `false` for a worn or held item, a target that is not a container, a container put into itself or into what it holds, or an item one mobile carries moved to another mobile (a trade, not supported yet). No weight or item limit is checked, and players who have a container on the ground open do not see it change until they open it again |
| `mobile.teleport(serial, x, y, z, map?)` | Teleports a mobile, a player or an NPC, to `x`, `y`, `z` of its own map, or of `map` (a `MapType`, or its name such as `"Tokuno"`) when given: a player's client is told of the map change (0xBF 0x08) and where it stands (0x20), the players around the old spot lose the mobile and those around the new one see it; `false` for a mobile not in the world, a map that does not exist or is not loaded, a spot outside the map or a `z` outside -128 to 127 |
| `mobile.animate(serial, action, frames?, repeat_count?)` | Plays an animation of the mobile, seen by its own player and those who see it: `action` is a number of its body (0 to 65535), with `frames` (1 to 255, default 5) played `repeat_count` times (1 to 255, default 1). The bodies do not share the numbers, so use the names of the body: `HumanAnimationType` (`Bow`, `Salute`, `Fidget1`, `Spell1`...), `MonsterAnimationType` (`Attack1`, `GetHit`, `Pillage`, `Fidget1`...) or `AnimalAnimationType` (`Eat`, `Alert`, `LieDown`...), as in `mobile.animate(who, HumanAnimationType.Bow)`; a number works too. `false` for a mobile not in the world or a number out of range |
| `mobile.location(serial)`, `mobile.play_sound(serial, sound)` | Where a mobile stands, `{ x, y, z, map }` (`nil` when it is not in the world), and a sound id (0 to 65535) played where it stands for the players within 15 cells; `false` for a sound out of range or a mobile not in the world |
| `mobile.message(serial, text)` | A system message, in the lower left of the screen, read only by that player: `mobile.message(who, "That is too far away.")`; cut at 128 characters; `false` for an empty text, an NPC or a player not in the world |
| `mobile.template(serial)` | The id of the mobile template an NPC was made from, such as `"f_baker"`; `nil` for a player or a mobile not in the world |
| `mobile.name(serial)`, `mobile.is_player(serial)`, `mobile.direction(serial)` | The mobile's name, whether it is a player's character, and the `DirectionType` it faces; `nil`, `false` and `nil` for a mobile not in the world |
| `mobile.stats(serial)` | The mobile's numbers as a table: `body`, `strength`, `dexterity`, `intelligence`, `hits`, `hits_max`, `mana`, `mana_max`, `stamina`, `stamina_max`, `fame`, `karma`. Read only |
| `mobile.set_stats(serial, values)` | Changes the mobile's numbers, given as a table with any of those `mobile.stats` gives but `body`: `mobile.set_stats(who, { hits = 10, strength = 80 })`. Hit points, mana and stamina stay between 0 and their maximum, also when only the maximum changes. The mobile's player sees its bars or its status change and the players around the new health bar. `false`, with nothing changed, for an unknown name, a value that is not a whole number, a stat or a maximum outside 0 to 65535, an empty table or a mobile not in the world |
| `mobile.skill(serial, skill)`, `mobile.skills(serial)` | A skill as `{ value, cap, lock }`, in points (`50.5`) with `lock` being `up`, `down` or `locked`: `mobile.skill(who, SkillType.Magery).value`; a skill never trained is 0. And every skill above 0 as a table of name and value: `mobile.skills(who).magery`. `nil` for a mobile not in the world |
| `mobile.set_skill(serial, skill, value, cap?)` | Sets a skill in points, and its cap when given; the value stays between 0 and the cap. The mobile's player sees it in the skill window. `false` for a cap outside 0 to 6553.5 or a mobile not in the world; a number that is no `SkillType` raises an error |
| `mobile.set_name(serial, name)`, `mobile.set_body(serial, body)`, `mobile.set_hue(serial, hue)` | Give the mobile another name (30 characters at most), another body graphic or another skin hue (0 to 65535), seen at once by its player and the players around; `false` for a blank or longer name, a value out of range or a mobile not in the world |
| `mobile.flags(serial)` | What the mobile is, as `{ hidden, frozen, war_mode }`, each `true` or `false`; `nil` for a mobile not in the world |
| `mobile.set_hidden(serial, hidden)`, `mobile.set_frozen(serial, frozen)`, `mobile.set_war_mode(serial, war_mode)` | Hide or reveal the mobile: hidden, it leaves the screens of the players around, who get it back when it is revealed, while game masters and administrators still see it; the players do not hear what it says, cannot open its paperdoll or read its tooltip, and NPCs do not sense it (`world.mobiles_in_range` still returns it). Freeze or free it: frozen, it neither steps nor turns. Put it in war mode or in peace, shown to its player and the players around. Hidden and frozen are saved with the mobile; a mobile comes back in peace. `false` for a mobile not in the world |
| `mobile.backpack(serial)`, `mobile.region(serial)`, `mobile.light(serial)` | The serial of the backpack the mobile wears (look into it with `item.contents`), the name of the region it stands in (`nil` outside every region) and the light level there, 0 (day) to 30 (dark) |
| `mobile.get_prop(serial, key)`, `mobile.set_prop(serial, key, value)` | A value a mobile, a player or an NPC, keeps across restarts: a string, a number or a bool; `nil` removes it. A player's is saved with its character. `set_prop` is `false` for a table, a function, a blank key or a mobile not in the world |
| `mobile.play_music(player, music)` | Plays a `MusicType` to a player, until its region gives it another; `false` for an NPC or a player not in the world |
| `target.pick(player, fn)`, `target.pick_location(player, fn)`, `target.cancel(player)` | Give the player the target cursor, to pick an item or a mobile, or a place, and run `fn(picked)` with what it clicked: `{ kind = "object", serial }`, `{ kind = "location", map, x, y, z }` or `{ kind = "canceled" }` (ESC, another cursor, or the player left); when a script itself replaces or cancels the cursor, the function runs on the next turn of the game loop. `false` for an NPC or a player not in the world |
| `effect.at(map, x, y, z, graphic, options)` | Plays an effect graphic that stays at a point of a map, such as the smoke of a teleport: `effect.at(MapType.Trammel, 1600, 1628, 5, EffectGraphicType.Smoke)`; see [Effects](#effects) |
| `effect.on(serial, graphic, options)` | Plays an effect graphic on a mobile, which it follows, or on an item lying on the ground; `false` for something not in the world |
| `effect.moving(from, to, graphic, options)` | Plays an effect graphic flying from one mobile or ground item to another on the same map, such as a fireball; `false` when one is not in the world or they are on two maps |
| `effect.lightning(serial, hue)` | Strikes a mobile or a ground item with a lightning bolt; the hue is optional |
| `locations.node(path)` | One level of the named places of [`locations.toml`](data-files/locations.md), as the [go gump](commands/go.md) lists them: `{ path, name, categories, locations }`, each category `{ name, path }` and each location `{ name, category, map, x, y, z }`. `""` is the list of the maps, then `map/category/...` in any case; `nil` for an unknown path |
| `locations.find(text, map)` | The places a text names, as an array of `{ name, category, map, x, y, z }`: a name, or the last words of the categories and the name (`"covetous entrance"`); a category alone gives its first place. Those of `map` when any fits, else those of the other maps |
| `moongates.facets()` | The public moongates of the loaded maps, from [`moongates.toml`](data-files/moongates.md): an array of `{ map, cliloc, selected_cliloc, destinations }`, each destination `{ name, cliloc, x, y, z }` |
| `world.is_occupied(map, x, y)` | Whether a player or an NPC stands on the tile, at any height, such as a door's doorway; `map` is a `MapType` |
| `world.is_guarded(map, x, y, z)` | Whether guards protect the region of the place, such as a town: `world.is_guarded(MapType.Trammel, 1496, 1628, 10)`; `false` outside every region |
| `world.moon(moon, x)` | The phase of `MapType.Trammel` or `MapType.Felucca` seen from the column `x`, a `MoonPhaseType` (`NewMoon`, `WaxingCrescent`, `FirstQuarter`, `WaxingGibbous`, `FullMoon`, `WaningGibbous`, `LastQuarter`, `WaningCrescent`): `world.moon(MapType.Trammel, x) == MoonPhaseType.FullMoon`. Felucca turns every 10 game minutes, Trammel every 30 |
| `world.time(map, x)` | The time of day on the map at the column `x`, as `{ hours, minutes }`: `world.time(MapType.Trammel, 1600).hours`; see `ultima.world.seconds_per_uo_minute` |
| `world.now()` | The real time as whole seconds since 1970 (UTC). Keep `world.now() + 3600` in a prop to do something an hour from now, also after a restart, as the town containers do with their next refill |
| `world.get_prop(key)`, `world.set_prop(key, value)` | A value the whole shard keeps across restarts, saved with the world: a string, a number or a bool, `nil` removes it. `world.set_prop("event.day", 12)`; `false` for a table, a function or a blank key |
| `world.is_staff(player)` | Whether the player is a game master or an administrator in the world; `false` for an NPC or a player not in the world |
| `world.carries(mobile, key, value)` | Whether the mobile wears or carries, in its containers at any depth, an item whose prop `key` is `value`, such as the key of a door: `world.carries(user, "key.value", 1234)` |
| `world.region(map, x, y, z)` | The name of the region of a place; `nil` outside every region |
| `world.mobiles_in_range(map, x, y, range)`, `world.items_in_range(map, x, y, range)` | The serials of the players and NPCs, or of the items on the ground, within `range` tiles (0 to 32) of a place, at any height, as a list |
| `world.players()` | The serials of the players' characters in the world, as a list |
| `world.line_of_sight(map, x1, y1, z1, x2, y2, z2)` | Whether nothing stands between two places of a map, as for a spell or an arrow; `false` beyond the range a line of sight is checked at and on a map that is not loaded |
| `world.standing_z(map, x, y, z)` | The height a mobile can stand at on a cell, at or below `z`, such as before teleporting someone there; `nil` when nothing there can be stood on |
| `world.weather(player)`, `world.season(map)` | The weather where a player stands, as `{ kind, density, temperature }` with `kind` a `WeatherKindType` (`nil` for an NPC or a player not in the world), and the `SeasonType` of a map (`nil` when the seasons are not running) |
| `world.broadcast(text)` | A system message (cut to 128 characters) to every player in the world; `false` for a blank text |
| `bank.open(player)`, `bank.is_open(player)` | Opens the player's bank box, made the first time, open while the player stands still; and whether it is open. `false` for an NPC or a player not in the world; see [Bank](bank.md) |
| `gump.open(player, id, args)`, `gump.close(player, id)` | Opens the gump `templates/gumps/<id>.xml` on the player, its `${name}` filled from `args`, and closes it; its script `scripts/gumps/<id>.lua` gets the answer. `false` for an unknown player, and from `gump.open` for an unknown gump. Called from a script, the gump opens or closes on the next turn of the game loop, so `gump.close` gives `true` even for a gump that is not open. See [Gumps](gumps.md) |
| `gump.create(id, x, y)`, `gump.send(player, g, args)` | Builds a gump in Lua (`g:text{...}`, `g:button{...}`, `g:paginate(...)`, ...) and opens it, from a script on the next turn of the game loop; `false` for an unknown player. A button's `on_click` may be a function. See [Gumps built in Lua](gumps.md#gumps-built-in-lua) |
| `item.delete(serial)` | Deletes the item; `false` for a worn item, an item a player holds on the cursor, or a container that still holds items |
| `item.message(serial, player, text)` | A label over the item seen only by `player` (cut to 128 characters); `false` for blank text, an unknown item, or a player not in the world |
| `item.message_cliloc(serial, player, cliloc, args?)` | The same label with a text of the client, by its number, so each player reads it in the language of the client; `args` fills its `~1_NAME~` places, split by tabs. `false` when the player or the item is not in the world |

The default host registers `log`; the engine supplies `engine`, `timer`, `events` and `wait`.
The Ultima plugin registers `dice`, `localization`, `npc`, `item`, `mobile`, `world`, `target`, `prompt`, `gump`, `bank`, `effect`, `moongates` and `locations` in game and standalone modes. The repository also ships two cats of Moongate v2, `orione` and `vega` (`templates/mobiles/moongate_cats.toml` with `scripts/mobiles/orione.lua` and `vega.lua`): spawn them with `.spawn orione` or `.spawn vega`.
Log levels still follow the host's logging policy, so a `log.debug` call need not
appear in the default console output. Use templates rather than concatenating
changing values into messages.

Call `wait` from a scheduled coroutine, such as a timer callback, not at the top
level of `init.lua` or a required module. It needs a positive, finite delay that
fits the timer range. It yields the coroutine rather than blocking the thread.
Each repeating timer occurrence starts a coroutine: if one calls `wait` for longer
than the repeat interval, multiple suspended invocations can coexist. Cancelling
the timer prevents later starts; it does not cancel an already-started coroutine.
For sequences that must not overlap, use a one-shot callback that schedules its
next run only after its work finishes.

The `npc`, `item`, `mobile`, `effect`, `world`, `target`, `prompt`, `bank` and `gump` modules serve the [mobile](#mobile-scripts) and
[item scripts](#item-scripts). A script reads and writes a mobile's numbers and skills; nothing
uses them yet, so a skill a script sets gains nothing by itself (see the
[Roadmap](roadmap.md#phase-0-what-lua-needs-before-any-gameplay)). To expose application
behavior, bind a C# module using [Writing a Lua module](lua-modules.md).

## Events

Scripts react to server events with the built-in `events` module:

```lua
local handle = events.on("character_created", function(e)
    log.info("New character {Name}", e.name)
end)

events.off(handle) -- returns false when the handle is unknown
```

- Each handler runs on the game loop as a coroutine, so it may call `wait()`.
- Handlers of one event run in the order they subscribed. Subscribing or
  unsubscribing inside a handler takes effect from the next event.
- Every handler receives its own table; changing it does not affect other handlers.
- Events are notifications: a handler cannot cancel or change what happened. An
  error in a handler is reported like any script error, and the other handlers
  still run.
- Subscriptions belong to the file that made them. Reloading or invalidating the
  file removes them, like its timers; stopping the engine removes all of them.
- An unknown event name raises an error in `events.on`. The generated
  `definitions.lua` lists the valid names as the `EventName` alias, so editors
  complete them.

### Available events

| Event | Fields |
| --- | --- |
| `character_created` | `serial`, `account_id`, `name`, `race` and `gender` (numbers of `RaceType` and `GenderType`), `map`, `x`, `y`, `z`. Raised after a new character and its starting items are saved. |
| `character_deletion_requested` | `serial`, `account_id`, `name`. Raised after a player asks to delete a character; it stays restorable until removed. |
| `character_entered_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character entered the world and the client's login completed. |
| `player_say` | `serial`, `name`, `text`. Raised after a player's character said something and the players and NPCs around heard it; `text` is what they heard. A command (text starting with a dot) raises nothing. |
| `character_left_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Raised after a character left the world because its session closed, once its save was attempted. |
| `player_region_changed` | `serial`, `name`, `previous`, `current`, `map`, `x`, `y`, `z`. Raised when a player's character walks or is teleported from a region into another, or changes map; `previous` and `current` are the regions' names, `nil` outside every region, and `previous` is also `nil` when the character just entered the world |

### Publishing an event from C#

Hosts and plugins publish a bus event to Lua with an explicit registration,
before the engine starts:

```csharp
container.AddScriptEvent<MyEvent>(
    "my_event",
    e => new Dictionary<string, object?> { ["name"] = e.Name, ["amount"] = e.Amount });
```

The name must be snake_case and unique, and each event type is published once.
The mapping runs on the publishing thread and must only read the event. It may
return strings, booleans, numbers, enums (sent as numbers) or null. A mapping
that fails is logged, and the event is skipped for Lua only. Events published
while no script is subscribed cost one lookup and are not queued.

## Mobile scripts

A mobile template names its script with `script_id`, the name of a global Lua table
defined by `scripts/mobiles/<script_id>.lua`. The server loads every `*.lua` directly
in that directory at startup, in name order, after `init.lua`; a script that fails
to load is reported like any script error, and the server starts with the others.

```toml
# templates/mobiles/animals.toml
[[mobile]]
id = "cat"
name = "a cat"
body = 201
script_id = "wander"
```

The table may define these functions; each one is optional:

| Function | When |
| --- | --- |
| `on_think(serial)` | On every think of the NPC: every `ultima.npcs.think_interval_ms` (500 ms by default) while a player is within the 5×5 sectors around it; see [NPC tick](game-loop-and-timers.md#npc-tick). A think is instantaneous, as ModernUO's: it must not call `wait` (the server warns once per script), so keep the timing in the script, for example by counting thinks. |
| `on_speech(serial, speaker, text, keywords)` | When a player says `text` within 15 cells (commands are not heard). `speaker` is the player's serial; `keywords` the speech keywords the client found, an array of numbers whatever its language, such as `SpeechKeywordType.Bank`. It may call `wait`. |
| `on_spawn(serial)` | Once, right after the NPC is spawned (`.spawn`), in the world with its items and shown, before any other function of its script. Not when the saved NPCs are loaded at startup. It may call `wait`. |
| `on_mobile_in_range(serial, other)` | Each time another mobile, player or NPC, comes within `ultima.npcs.sense_range` cells (8 by default, a square along X and Y) by a step or by entering the world. Once per arrival: it fires again only after the mobile has left the range and come back. Both ways: an NPC walking toward a mobile senses it too. NPCs loaded together at startup do not sense each other until one moves out of range and back. `other` is its serial; `npc.name(other)` gives `nil` for a player. It may call `wait`. |

`on_spawn` and `on_mobile_in_range` run right after what caused them, on the next
turn of the game loop: a step made by `npc.step` inside a running handler cannot
start another script at once. No function runs before the scripts are loaded at
startup, which is after the saved NPCs enter the world.

Scripts act on their NPC with the `npc` module, passing its serial. A serial that
is not an NPC in the world, such as a removed NPC or a player, gives `false` or
`nil`, never an error: a handler that waited may outlive its NPC, and a script can
never voice or move a player.

The distribution's `scripts/mobiles/wander.lua`, copied into the root by `mgctl`:

```lua
wander = {}

local thinks = {}

function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.wander(serial)
    end
end

function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end

function wander.on_spawn(serial)
    npc.say(serial, "*stretches*")
end

function wander.on_mobile_in_range(serial, other)
    if npc.name(other) == nil then
        npc.say(serial, "Who goes there?")
    end
end
```

No template in the repository uses it: add `script_id = "wander"` to a mobile template
to try it. An NPC spawned by a spawn region carries its home area in the props `spawn.x1`,
`spawn.y1`, `spawn.x2` and `spawn.y2`, which `npc.home` gives as a table: `npc.wander` only steps
inside it, and walks the NPC back when it is outside. It strolls as ModernUO's creatures do, mostly
straight ahead, where the script before picked a new direction at every step.

A script's `local` tables live in memory: they start again empty after a restart or a
reload. To remember something across restarts, keep it in the NPC's props, prefixing the
key with the script name, as `vega.lua` counts the hellos it hears:

```lua
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        local times = (npc.get_prop(serial, "vega.greeted") or 0) + 1
        npc.set_prop(serial, "vega.greeted", times)
        npc.say(serial, "Meow! That's " .. times .. " hellos.")
    end
end
```

A change made after the last world save is lost if the server stops without saving.

Reload one script with `script reload mobiles/wander.lua`. Its table is replaced,
so the NPCs use the new functions from their next think; state kept in `local`
tables of the old file starts again, and the waits its handlers left are cancelled,
because a script's calls belong to `mobiles/<script_id>.lua`. When the server stops,
the scripts are no longer called, before the script engine stops.

### The monster script

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

### Walking a path

`npc.walk_to` lets an NPC reach a place around walls, water and cliffs. The script calls it
on every tick and the NPC takes one step each time:

```lua
guard = {}

function guard.on_think(serial)
    local state = npc.walk_to(serial, 1434, 1699)

    if state == "arrived" then
        npc.say(serial, "All quiet at the bank.")
    end
end
```

| Answer | Means |
| --- | --- |
| `"moving"` | The NPC took a step |
| `"arrived"` | It stands within `range` tiles of the place (default 0), at its height; it is not checked that nothing stands between them |
| `"blocked"` | The step was refused, or the NPC waits to look for another way |
| `"no_path"` | The last search did not reach the place: nothing leads there, or only somewhere near |
| `nil` | The serial is not an NPC, `range` is negative or `z` is outside -128 to 127 |

The path is found with the server's [path search](world-queries.md#pathfinding) and kept for
the NPC, so most calls only take the next step. A search runs when the NPC has no steps left
or the place changed, and only:

- two seconds after the NPC's last search, as ModernUO; ten seconds when that search did not
  reach the same place from where the NPC stands, since such a search is the costly kind;
- for ten NPCs a second in the whole server; the others wait their turn.

While it may not search, an NPC goes on along the path it has, or with none steps straight
towards the place, so one that chases something keeps moving. A place that cannot be reached
is walked towards as far as a path leads. To follow someone, pass where it stands on every
tick and a `range` of 1 to stop beside it:

```lua
local where = mobile.location(target)
npc.walk_to(serial, where.x, where.y, where.z, 1, true)
```

`running` only changes how the step looks: an NPC takes one step per `on_think`, two a second.
Without `z` the place is the highest ground of the cell not above the NPC's head, else the
highest there. Start and goal must be within `ultima.world.pathfinding_range` tiles (38);
farther is `"no_path"`. A closed door blocks the way, and an NPC does not open it: it goes
around, or walks up to it and then answers `"no_path"`. Other mobiles do not block a path.

## Item scripts

An item template names its script with `script_id`, the name of a global Lua table
defined by `scripts/items/<script_id>.lua`. The files of `scripts/items/` load at
startup like the [mobile scripts](#mobile-scripts), and `script reload
items/potion.lua` reloads one.

```toml
# a potion template of your own
[[item]]
id = "my_potion"
item_id = 0x0F0C
script_id = "potion"
```

| Function | When |
| --- | --- |
| `on_use(serial, user)` | A player double clicks the item, carried (worn or in its containers) or on the ground within 2 tiles and in sight; farther, the player reads "That is too far away." and nothing runs. Items inside a container lying on the ground cannot be used yet: the player reads "That is too far away.". A missing `on_use`, or one that raises an error, lets the default action follow. Return `true` to stop the default action, such as opening a container; return nothing to let it follow. A handler that calls `wait` counts as handled; after the wait the item may have moved, so check it again, for example `item.owner(serial) == user`. |
| `on_move_over(serial, mobile)` | A player stepped onto the cell of the item, lying on the ground at the player's height, up to 14 above its feet, or below them and tall enough to reach them (ModernUO's rule). It runs after the step was acknowledged and shown to the players around; an NPC runs `on_npc_move_over` instead. Once a script moved the player off the cell, the other items of the cell are not run. Arriving by teleport does not trigger it, so two teleporters that point at each other do not loop. |
| `on_npc_move_over(serial, npc)` | An NPC stepped onto the cell of the item, by the same height rule. It runs on the turn of the game loop after the step, so the NPC may already have moved on: check where it is |
| `on_speech(serial, speaker, text, keywords)` | A player said `text` within 15 cells of the item, lying on the ground (commands are not heard). `speaker` is the player's serial; `keywords` the speech keywords the client found, an array of numbers. Every scripted ground item in range is asked, after the NPCs, so a script checks its own range and words. It may call `wait`. |
| `on_equip(serial, wearer)` | The item went onto a layer of the mobile `wearer`, dropped on the paperdoll. A worn item lifted and bounced back never left its layer, and items loaded or spawned already dressed raise nothing. It cannot refuse the item: `can_equip` does. |
| `on_unequip(serial, wearer)` | The item left the layer of `wearer`: dropped in a container or on the ground, or merged into a stack (the item is gone then, so `item.*` gives `nil`). Logging out, removing an NPC or deleting a mobile with its items raise nothing. |
| `on_pickup(serial, picker)` | The player `picker` lifts the item from a container, the paperdoll or the ground; lifting part of a stack lifts this item, and the rest left behind is not new. While it is held, `item.consume` and `item.delete` refuse it. A held item ends in `on_drop`, in `on_equip` when it is worn by a new wearer, or in nothing: when it bounces back, is worn again on the layer it came from, or its player logs out holding it. |
| `on_drop(serial, dropper)` | The player `dropper` puts the held item down: into a container, on the ground, or onto a stack (the item is gone then, so `item.*` gives `nil`). Not when it bounces back or is worn. A worn item put down runs `on_unequip` first, then `on_drop`. |
| `can_pick_up(serial, picker)` | The player `picker` is about to lift the item, from a container, the paperdoll or the ground, once every rule of the server allows it and before a stack is split. Return `false` to refuse: the item stays where it is and the client shows no message of its own. A worn item is lifted before it is taken off, so this is also where a script keeps an item on its wearer |
| `can_drop(serial, dropper)` | The player `dropper` is about to put the held item down, anywhere: on the ground, into a container or onto a stack. Return `false` to refuse: the item goes back where it was lifted from |
| `can_equip(serial, wearer)` | The held item is about to be worn by `wearer`, once the layer is free and the rules allow it. Return `false` to refuse: the item goes back where it was lifted from and `on_equip` does not run. Not asked of a worn item lifted and put back on its layer, which it never left |
| `can_insert(serial, mobile, item)` | Asked of a container: the player `mobile` is about to put `item` into it, or onto a stack that lies directly in it, once the rules allow the drop. `serial` is the container, carried or lying on the ground; a container holding that container is not asked. Return `false` to refuse: the item goes back where it was lifted from. It is asked after the item's own `can_drop`, and once for each drop |
| `on_timer(serial, name)` | A timer of the item, started with `item.start_timer`, is due. Timers are checked once a second. The timer is gone when the function runs: start it again there for something that repeats. One that came due while the server was down, or while the character carrying the item was offline, runs as soon as the item is in the world again. It may call `wait` |
| `on_darkness(serial, dark)` | Every 30 seconds, and right after `.globallight`, on a lamp post (template `decoration_light`, prop `decoration_type` LampPost1 to LampPost3) whose spot turned dark (`true`) or light (`false`) |
| `on_create(serial)` | A newly created item enters the world: the equipment, backpack and loot of a spawned NPC, before that NPC's `on_spawn`, and a chest a spawn region makes, with everything inside it. A new character's starting items, the rest of a split stack, and items made by `item.give`, `item.create`, `item.add_loot` or `.decorate` raise nothing. |

`on_equip`, `on_unequip`, `on_pickup`, `on_drop` and `on_create` run right after what
caused them, on the next turn of the game loop, once the players have seen it: a script
may then delete or consume the item. They are notifications: none can refuse the move.

`can_pick_up`, `can_drop`, `can_equip` and `can_insert` are questions, asked before the
move and answered at once: only `false` refuses. A missing function, an error, a call to
`wait` or any other value lets the move follow, so a broken script never locks an item. Tell
the player why with `mobile.message` before returning `false`. They are asked for the moves
a player makes with the client, staff included; a script that moves an item itself
(`item.move_to`, `item.move_into`) is not asked. While a question is asked the item counts as held, so
`item.delete`, `item.consume`, `item.move_into` and the functions that change it refuse it:
answer the question there, and act on the item in `on_pickup`, `on_drop` or `on_equip`.

```lua
-- a cursed ring: once worn, it stays on
function ring.can_pick_up(serial, picker)
    -- worn: it has an owner and lies in no container
    if item.owner(serial) == picker and item.container(serial) == nil then
        mobile.message(picker, "The ring will not come off.")
        return false
    end
end
```

The script acts on its item with the `item` module, passing its serial; `user` is
the serial of the player. The distribution's `scripts/items/potion.lua`, copied into the root by `mgctl`; no
template uses it yet:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

The distribution also ships `scripts/items/door.lua`, the script of the `decoration_door`
template that [`.decorate`](commands/decorate.md) gives to doors and gates. Double clicking
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

`scripts/items/teleporter.lua` is the script of the `decoration_teleporter` template that
[`.decorate`](commands/decorate.md) gives to ModernUO's `Teleporter`: on `on_move_over` it
teleports the player to the props `teleport.x`, `teleport.y` and `teleport.z` with
`mobile.teleport`, shows a puff of smoke where the player left (prop `source_effect`) and
arrived (prop `dest_effect`), then plays the prop `sound_id` there when the teleporter has one. The prop
`active = false` turns a teleporter off. Only players travel, unless the prop `creatures` is true: then an NPC that steps on it travels too. A teleporter with the prop `teleport.map`, a `MapType`
number, takes the player to that map: the client changes map, then gets the season when it differs
from the one it shows, the light, the weather and the music of the place; when the map is not loaded nothing happens. The template has `visibility = "game_master"`: a ground item is sent only to
the accounts its visibility allows, so players walk onto a teleporter they never see.

`scripts/items/public_moongate.lua` is the script of the `decoration_public_moongate` template
that `.decorate` puts on every destination of [`moongates.toml`](data-files/moongates.md), as
ModernUO's `PublicMoongate`: on `on_move_over`, and on `on_use` from the next cell, it builds a
gump with `gump.create`, one page per map of `moongates.facets()` and one button per city, the
page of the player's own map first, and plays the sound `0x20E`. A button teleports the player
with `mobile.teleport`, to another map too, and plays `0x1FE` there. A player who walked more
than a cell away while the gump was open is told so and stays; choosing the city of the gate
itself does nothing.

`scripts/items/moongate.lua` is the script of the `moongate` template, the gate with one
destination that the command [`moongate`](commands/moongate.md) puts at a game master's feet, as
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

`scripts/items/keyword_teleport.lua` is the script of the `decoration_keyword_teleporter`
template that `.decorate` gives to ModernUO's `KeywordTeleporter`, such as the mantra of a
shrine: on `on_speech` it teleports the player who says the prop `substring` (found anywhere in
the text, in any case) or whose client sends the speech keyword of the prop `keyword`, standing
within `range` cells (0, the default, is the teleporter's own cell). With a `delay`
(`"0:0:1"`, or a number of seconds) the teleport happens later, if the player still stands in
range. The destination, the smoke, the sound and `active` are those of the plain teleporter.

`scripts/items/clock.lua` is the script of the clocks (the item templates `0x104b_clock` and
`0x104c_clock`, and `decoration_clock` for those `.decorate` places), as ModernUO's `Clock`: on
`on_use` the player reads over the clock the part of the day ("It's the afternoon") and the time to
the minute ("1:07 to be exact") where they stand, from `world.time`, as texts of the client sent
with `item.message_cliloc`.

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

LuaCSharp does not read a hexadecimal number between brackets (`t[0x0A27]` or
`{ [0x0A27] = ... }` fail with "malformed number"): pass it through a function or a variable,
as `light.lua` does with `add(0x0A27, 0x0B1D, "circle225")`.

## Effects

The `effect` module shows graphic effects to the players of the map within the view range
(`ultima.world.view_range`); a moving effect also reaches those in range of where it arrives.
The graphic is an art id: `EffectGraphicType` names the animations the emulators use
(`Smoke`, `LargeFireball`, `SmallFireball`, `FireColumn`, `Explosion`, `SparkleHeal`,
`SparkleBless`, `SparkleCurse`, `Fizzle`, `SmallBolt`, `Glow`, the four fields and others; the
generated `definitions.lua` lists them all), and any other art id works too.

`effect.at`, `effect.on` and `effect.moving` take an optional table of options:

| Option | Meaning |
| --- | --- |
| `speed`, `duration` | 0 to 255 each; both default to 10 |
| `hue` | Hue of the graphic, 0 to 65535 |
| `render` | An `EffectRenderModeType`: `Normal`, `Darken`, `Lighten`, `LightenTransparent`, `Translucent`, `TranslucentColor`, `Negative`, `NegativeTransparent` |
| `fixed_direction`, `explodes` | For a moving effect: keep the graphic's direction, and explode on arrival |
| `particle`, `explode_particle`, `explode_sound` | Particle effect ids and the arrival sound; only the Enhanced Client shows particles |
| `layer` | An `EffectLayerType`, the body part the particles are shown at: `Head`, `RightHand`, `LeftHand`, `Waist`, `LeftFoot`, `RightFoot`, `CenterFeet`, or `None`, the default |

```lua
-- A fireball from the caster to the target, exploding there.
effect.moving(caster, target, EffectGraphicType.LargeFireball, { speed = 7, duration = 0, explodes = true })

-- The healing sparkle on a mobile; the Enhanced Client also gets particles at the waist.
effect.on(who, EffectGraphicType.SparkleHeal, { speed = 9, duration = 32, particle = 5005, layer = EffectLayerType.Waist })
```

A function returns `false` and plays nothing for a value out of range, an option of the wrong
type (`speed = "9"`, `explodes = 1`), an option it does not know, and an effect with neither a
graphic nor a particle. An argument of the wrong type raises an error, as for every module. A classic client draws no particles: it gets the graphic, and
nothing for an effect made of particles only; a moving effect with the graphic `1`, ModernUO's
placeholder, counts as one. A lightning bolt has no graphic and is always sent. Effects are not sequenced: chain them with
`timer` calls.

## Reload and ownership

The console accepts:

```text
script reload init.lua
script metrics
```

Reload invalidates the named file, evicts its matching `require` cache entry,
cancels timers/coroutines owned by that file and executes it again on the loop.
It does not rebuild the whole Lua state, recursively invalidate dependencies or
roll back globals if the new file fails. Existing references to a module table
remain references to that old table. After changing a required helper, invalidate
that helper and reload the consuming script to obtain the new module value.

Ownership follows the engine's active load/coroutine context. In particular,
`require` executes a module within its caller's context; timers created there
must not be assumed to belong to the module filename. Prefer modules that return
functions/data and let `init.lua` or an explicitly loaded owner create timers.
This makes cancellation on reload predictable. Engine shutdown cancels all owned
scheduled work before disposing its Lua state.

`script metrics` shows file loads, calls, coroutine resumes/completions/errors,
active coroutines, budget aborts, string-cap hits and server events dropped
because the game loop refused them (each drop is also logged as a warning). Script errors include source
information where available, are logged, and publish `ScriptErrorEvent`. Ordinary
runtime errors are reported by the scheduler; host C# callers of `LoadFile` must
handle its exceptions. Missing files and cancellation have their own failure paths.

## Budgets and sandbox

Defaults allow 150,000 instructions per coroutine resume and 10,000,000 per
loaded top-level chunk, checked every 1,000 instructions. They bound VM instruction
execution, not wall-clock duration or native C# work. A module that blocks on I/O
can still block the loop; keep host-bound functions short and synchronous.
`string.rep` caps its result at 16,777,216 UTF-16 characters by default. Other
allocations, including tables and concatenation, do not have a global memory cap.
Treat scripts and C# plugins as trusted shard content, not as an isolation boundary
for arbitrary hostile code.

Available libraries are base, `string`, `table`, `math`, restricted `coroutine` and
restricted `package`. There is no `io`, `os`, `debug`, `dofile`, `loadfile` or
`rawset`. Filesystem/native package search paths and script-created/resumed
coroutines are disabled. Use `require` for local modules and `wait` for scheduled
yielding. See the [library sandbox reference](../src/Moongate.Scripting/README.md#sandbox)
for the exact removed functions.

## Editor support

With `write_definitions = true`, startup writes `scripts/definitions.lua` and
`scripts/.luarc.json` from registered modules, functions, constants and enums.
Open the scripts folder in an editor using the Lua language server for completion.
These files are generated: put handwritten content in separate files and register
C# modules before startup so they appear in the definitions. They provide editor
metadata, not runtime loading; do not `require("definitions")`.
