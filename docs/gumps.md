# Gumps

Gumps are the dialogs the server opens on a player: a layout of backgrounds, texts, buttons,
checkboxes and text fields, and the player's answer coming back to the server.

A gump is two files:

- `templates/gumps/<id>.xml`: the layout, checked against `templates/gumps/gump.xsd`;
- `scripts/gumps/<id>.lua`: what happens when the player answers or closes it.

Parts that change with the data come from the script: a `<slot>` in the XML, or a whole gump
built in Lua. New to gumps? Follow [Your first gump](gump-tutorial.md).

## The layout

```xml
<?xml version="1.0" encoding="utf-8"?>
<gump id="release_pet" x="100" y="100"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
  <background x="0" y="0" gump="5054" width="270" height="140" />
  <html x="20" y="15" width="230" height="40" cliloc="1070722" args="${pet_name}" />
  <text x="20" y="60" hue="1152">Release ${pet_name}?</text>
  <button x="20" y="100" up="4005" down="4007" on_click="release" />
  <html x="55" y="100" width="75" height="20" cliloc="1011011" />
  <button x="135" y="100" up="4005" down="4007" on_click="cancel" />
  <html x="170" y="100" width="75" height="20" cliloc="1011012" />
</gump>
```

With `xsi:noNamespaceSchemaLocation="gump.xsd"`, VS Code (with an XML extension) and Rider
complete the elements and attributes and flag mistakes as you type. The server checks every file
against the same schema at startup: a mistake stops it with the file, the line and the reason.

`<gump>` takes `id` (lower case, the same as the file and script), `x` and `y`, and `closable`,
`movable`, `disposable` and `resizable` (true unless set to false). Its controls show on every page;
the controls inside each `<page>` show on pages 1, 2, ... in order.

| Element | Shows |
| --- | --- |
| `background` | A resizable background (`gump`, `width`, `height`) |
| `alpha_region` | A see-through region |
| `image`, `image_tiled` | A gump image, with an optional `hue`, or repeated over a box |
| `item` | An item graphic (`item`, optional `hue`) |
| `text` | A line of text (`hue`) |
| `label_cropped` | Text cut to a box |
| `html` | HTML text in a box (`background`, `scrollbar`), or a client message (`cliloc`, `color`, `args`) |
| `button` | A button: `on_click` (a function of the script), `id` (to `on_button`), `page` (turns the page) or `open` (opens another gump) |
| `checkbox` | A checkbox (`switch` id, `checked`, `bind`) |
| `group` with `radio` | Radio buttons of which one can be on (`switch`, `checked`, `bind`) |
| `text_entry` | A text field (`entry` id, `max_length` up to 239, `bind`; its inner text is the starting text) |
| `tooltip` | The tooltip of the control before it (`cliloc`, `args`) |
| `item_property` | The tooltip of a real item (`serial`) |
| `slot` | Where the script adds controls when the gump opens (`name`, `x`, `y`); see [Slots](#slots) |

### Texts

A text comes from one of three places:

| Source | How | Translated by |
| --- | --- | --- |
| The element | `<text ...>Hello ${name}</text>` | Nobody |
| A server message | `message="30083"` on `text`, `label_cropped`, `html` | The server, from `data/messages`, in the server language |
| A client message | `cliloc="1011011"` on `html` and `tooltip` | The client, in the player's language |

The client only reads client messages in `html` and `tooltip`: the schema refuses `cliloc` on
`text`. Use client messages for the standard texts the client already has (CONTINUE 1011011,
CANCEL 1011012, ...) and server messages for your own texts, which the shard can translate. Both in
one gump can mix two languages until per-player languages exist.

### Placeholders

`${name}` in a text or a number is filled from the arguments the gump is opened with; a missing
one is empty. The ids (`id`, `page`, `switch`, `entry`) and `max_length` take plain numbers only, so
the schema can check them. `args` (tab separated) fills a client message's `~1_NAME~` markers. `@`, `{` and `}`
are removed from client message arguments, so a player's name cannot break the layout.

### Gumps in a row

A gump can lead to another, with the same arguments, as a wizard does:

- `bind="name"` on a `text_entry`, `checkbox` or `radio` writes the answer into the argument `name`
  when the player answers: the text, `true`/`false`, or the `switch` of the radio that is on;
- `open="other_gump"` on a button opens that gump with the arguments, bound values included.

```xml
<text_entry x="20" y="40" width="200" height="20" entry="1" bind="name">${name}</text_entry>
<button x="20" y="80" up="4005" down="4007" open="step2" />
```

`step2` can show `${name}`, and every callback of either gump finds `args.name`. The server
checks at startup that the gump an `open` names exists. A script decides by itself where to go with
`gump.open(player, "step2", args)`.

### Slots

`<slot name="rows" x="20" y="45" />` (plain numbers, no placeholders) is filled when the gump opens: the server calls the function
`rows` of the gump's script with a builder, `rows(g, player, args)`, and puts what it adds at the
slot, coordinates counted from the slot. Pages the function makes (`g:page()`, `g:paginate`) are
added as the gump's pages, so a slot cannot be in a gump with `<page>` elements: the server refuses
it at startup. A missing slot function leaves the slot empty with a warning; one that fails or calls
`wait()` keeps the gump from opening. What the slot adds is checked like a gump file.

## Gumps built in Lua

`gump.create(id, x, y)` gives a builder whose methods add the controls of the XML elements of the
same name, with the same attributes; `gump.send(player, g, args)` opens it:

```lua
local g = gump.create("pet_list", 100, 100)
g:background{ x = 0, y = 0, gump = 9200, width = 300, height = 300 }
g:text{ x = 20, y = 15, hue = 1152, text = "Your pets" }
g:pager{ previous = { x = 20, y = 260 }, next = { x = 250, y = 260 } }

for i, pet in ipairs(pets) do
    local row = g:paginate(i, 10)
    g:button{ x = 20, y = 45 + row * 22, up = 4005, down = 4007, on_click = function(player, response, args)
        npc.say(pet.serial, "*follows*")
    end }
    g:text{ x = 55, y = 45 + row * 22, text = pet.name }
end

gump.send(player, g, {})
```

| Method | Adds |
| --- | --- |
| `g:background{}`, `g:alpha_region{}`, `g:image{}`, `g:image_tiled{}`, `g:item{}` | The element of the same name |
| `g:text{}`, `g:label_cropped{}`, `g:html{}`, `g:text_entry{}` | The element, `text = "..."` being its text |
| `g:button{}`, `g:checkbox{}`, `g:radio{}`, `g:tooltip{}`, `g:item_property{}` | The element; `on_click` may be a function |
| `g:group()` | A radio group: the radios after it belong to it |
| `g:page()` | A new page: what follows shows on it |
| `g:pager{ previous = {...}, next = {...} }` | Where `g:paginate` puts its buttons (`x`, `y`, `up`, `down`) |
| `g:paginate(index, per_page)` | A new page every `per_page` items, with the buttons between pages; returns the item's row on its page, from 0 |

Every method but `g:paginate` returns `g`, so calls can be chained. A button whose `on_click` is a
function calls it with `(player, response, args)`; the function sees the variables around it, such
as `pet` above, and belongs with the script that sent the gump. `gump.send` checks the gump as a
file is checked and fails with the reason, such as a `checked` that is not `true` or `false`. A built
gump answers like an XML one: texts, `bind`, `open` and `on_close` are the same. Placeholders are
escaped in `html`; text you join yourself, such as `"Hi " .. name`, is not, so write
`text = "Hi ${name}"` and pass `name` in the arguments.

`on_click` names starting with `__` are reserved for the server.

## The script

```lua
-- scripts/gumps/release_pet.lua
release_pet = {}

-- An on_click button: the player, the answer and the arguments the gump was opened with.
function release_pet.release(player, response, args)
    npc.say(args.pet, "*leaves*")
end

function release_pet.cancel(player, response, args) end

-- Buttons with an id instead of on_click.
function release_pet.on_button(player, button, response, args) end

-- The gump went away without a button: "player" (closed by the player), "replaced" (opened again),
-- "server" (gump.close, or too many gumps open) or "disconnect".
function release_pet.on_close(player, args, reason) end
```

`response` is `{ button = n, switches = { [id] = true }, text = { [id] = "..." } }`: the switches that
are on and the text of each text entry, already checked by the server.

Open and close a gump from any script:

```lua
gump.open(player, "release_pet", { pet_name = "Fido", pet = serial })  -- false for an unknown gump
gump.close(player, "release_pet")
```

In game, [`.gump release_pet pet_name=Fido`](commands/gump.md) opens it on yourself to try it.

The arguments take strings, numbers and booleans, and come back to every callback.

## Safety

The server keeps every gump it opens on each player, and checks every answer:

- an answer for a gump the player was not sent, or already answered, is dropped;
- an answer with a button, switch or text entry the gump does not have, or the same text entry
  twice, is dropped;
- a text longer than 239 characters is dropped (the schema keeps `max_length` at 239 or less);
- opening a gump with the same id closes the one already open, and a player keeps at most 64
  gumps.

A forged answer never reaches a script. Clients from 5.0.0a get the compressed packet (0xDD),
older ones the plain one (0xB0); see the [packet reference](packets.md).

## From C#

A plugin opens the gumps of `templates/gumps` with `IGumpTemplateService`:

```csharp
// On the game loop, with a callback:
templates.Open(session, "release_pet", new Dictionary<string, string> { ["pet_name"] = "Fido" },
    (session, answer) => { /* answer.Click is the on_click name, answer.Response the answer */ });

// Off the loop, waiting for the choice (null when closed):
var choice = await templates.AskAsync(session, "decorate_confirm", new Dictionary<string, string>());
```

`.decorate` asks this way with `templates/gumps/decorate_confirm.xml`.

A layout built in code goes through `IGumpService`, with one entry per command:

```csharp
var layout = new GumpLayout()
    .Add(new GumpBackground { X = 0, Y = 0, GumpId = 5054, Width = 270, Height = 120 })
    .Add(new GumpButton { X = 20, Y = 80, Up = 4005, Down = 4007, ButtonId = 1 });

gumps.Open(session, new GumpInstance
{
    Id = "my_gump", Layout = layout, X = 100, Y = 100,
    OnResponse = (session, response) => { /* response.ButtonId, Switches, Texts */ },
    OnClosed = (session, reason) => { /* replaced, server or disconnect */ }
});
```

The entries are `GumpPage`, `GumpGroup`, `GumpBackground`, `GumpAlphaRegion`, `GumpImage`,
`GumpImageTiled`, `GumpItem`, `GumpButton`, `GumpCheckbox`, `GumpRadio`, `GumpText`,
`GumpLabelCropped`, `GumpHtml`, `GumpHtmlLocalized`, `GumpTextEntry`, `GumpTooltip`,
`GumpItemProperty` and `GumpFlag`.
