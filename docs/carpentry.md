# Carpentry

A player with a carpentry tool makes furniture, containers, staves and instruments from boards. The Carpentry skill
decides whether an attempt works and grows with use. The rules are shared by every craft to come: carpentry is the
first one.

## How to make something

1. Carry a carpentry tool in your backpack, or in a bag inside it: a saw, a dovetail saw, a moulding, jointing or
   smoothing plane, chisels, a draw knife, a froe or an inshave. A tool on the ground or in your bank box answers
   "This item must be in your backpack to be used."
2. Double click the tool. The crafting gump opens: the groups on the left, the recipes of the group on the right,
   ten a page.
3. Press the button before a recipe to make it, or the one after it to see its page: the item, what it takes and how
   many of each you carry, the skills it asks for, and your chance.
4. Two strokes later, 1.25 seconds apart, the item is in your backpack and the gump opens again with what happened.

The bottom line shows the wood you work and how many boards of it you carry. Change lists the kinds of wood.

## What an attempt asks for

| Check | What you read |
| --- | --- |
| Carpentry, and any other skill of the recipe, at least its least | "You don't have the required skills to attempt this item." |
| A kind of wood you can work | "You cannot work this strange and unusual wood." |
| Every resource in your backpack and its bags: not in the bank box, not a pile on your cursor | "You do not have sufficient wood to make that.", "You don't have enough cloth to make that." or "You don't have the components needed to make that." |
| Not already making something | "You must wait to perform another action." |

Nothing is taken when an attempt is refused. The resources and the tool are checked again at the second stroke: boards moved away
meanwhile make nothing. The skill and the kind of wood are checked at the start only.

## The chance

Each recipe has a least and a most of Carpentry. At the least the chance is one in two; it grows in a line to sure at
the most. A stool asks for 11 to 36: at 23.5 the chance is three in four. Every try may raise the skills of the recipe.

- **Success** takes every resource and makes the item in your backpack, "You create the item.". A backpack with no
  room still takes them and puts the item at your feet. An item that cannot be made at all takes nothing.
- **Failure** takes half of each resource, rounded down, and at least one unit of the first, "You failed to create the item, and some of your materials
  are lost."

## Exceptional items and the maker's mark

A success is exceptional as often as its chance minus six tenths: at the most of a recipe four times in ten, never
with a chance of six tenths or less. An item that joins a stack you carry is never exceptional. You read "You create an exceptional quality item." and its tooltip says exceptional.
Made at 100 Carpentry, an exceptional item also bears your mark: "You create an exceptional quality item and affix
your maker's mark.", and its tooltip says crafted by your name. An exceptional item is uncommon, a marked one rare,
and an exceptional staff hits harder in a fight, as [blacksmithing](blacksmithing.md) says.

## Tools wear out

A tool lasts 25 to 75 uses, drawn the first time you use it; its tooltip shows the uses left. Every attempt whose
skill is tried, a success or a failure, takes one; a refused attempt takes none. The last use breaks it:
"You have worn out your tool!". A stackable item (a shaft, an arrow) takes no use, and is never exceptional nor marked.

## Make last

The Make last button starts again the last recipe you started with that craft, with the wood picked now; each craft remembers its own. Before your
first one it answers "You haven't made anything yet.". It is kept until a restart.

## Kinds of wood

Plain boards are the default. A board of another kind, cut by a [lumberjack](lumberjacking.md) and sawn with an axe,
asks for as much Carpentry as it asks for Lumberjacking, and gives its colour to the item.

| Kind | Carpentry it asks for |
| --- | --- |
| Plain | 0 |
| Oak | 65 |
| Ash | 80 |
| Yew | 95 |
| Heartwood, bloodwood, frostwood | 100 |

The kind picked counts for wood only: the cloth of a harp stays cloth. The kind is kept until a restart.

## The recipes

42 recipes in six groups, converted from UOX3: Chairs, Tables, Containers, Other Items, Staves & Poles and Musical
items. A few of them:

| Recipe | Carpentry | Takes |
| --- | --- | --- |
| Barrel Staves | 0 to 25 | 5 wood |
| Stool | 11 to 36 | 9 wood |
| Wooden Box | 21 to 46 | 10 wood |
| Wooden Shield | 52.6 to 80 | 9 wood |
| Chest | 73.6 to 98.6 | 15 wood |
| Quarter Staff | 73.6 to 100 | 6 wood |
| Lute | 68.4 to 93.4, Musicianship 45 | 25 wood, 10 cloth |
| Fishing Pole | 68.4 to 93.4, Tailoring 68.4 | 5 wood, 5 cloth |

The groups of add-ons (house, blacksmith, tailor and cooking add-ons) make deeds, which mean nothing until houses
exist: they are left out. So is the recipe of boards, which an axe already saws.

## Change the rules

- The recipes are [`data/crafts`](data-files/crafts.md).
- The rules of an attempt are `scripts/common/crafting.lua`; the kinds of wood `scripts/common/woods.lua`, shared with
  the axe.
- The gump is `templates/gumps/craft_menu.xml` with `scripts/gumps/craft_menu.lua`.
- The tools are the templates with `script_id = "carpentry_tool"`. See [Shipped scripts](scripting/shipped-scripts.md#craftinglua-and-carpentry_toollua).

## Existing roots

`mgctl init` never replaces a file you may have changed. Copy from the distribution `data/crafts/`,
`scripts/common/crafting.lua`, `scripts/common/woods.lua`, `scripts/common/smithy.lua` (the engine reads it), `scripts/items/carpentry_tool.lua`,
`scripts/items/axe.lua` (it now reads `woods.lua`), `templates/gumps/craft_menu.xml`, `scripts/gumps/craft_menu.lua`,
and `templates/items/skills/tools/carpenty.toml`, or give `script_id = "carpentry_tool"` to your carpentry tools.

## Not yet

What being exceptional changes in an item beyond its tooltip, the add-ons, repair, and the other crafts.

## See also

- [Lumberjacking](lumberjacking.md)
- [Crafts data files](data-files/crafts.md)
- [Shipped scripts](scripting/shipped-scripts.md#craftinglua-and-carpentry_toollua)
