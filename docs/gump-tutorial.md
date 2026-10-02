# Your first gump

This tutorial builds three gumps step by step: a two-step registration that asks a name and then
greets it, and a list that the script fills and pages. The finished files ship with the server, so
you can open them right away and compare them with your own (they are named `tutorial_*` instead
of `my_*`):

- `templates/gumps/tutorial_name.xml`, `templates/gumps/tutorial_greeting.xml` and
  `scripts/gumps/tutorial_greeting.lua`;
- `templates/gumps/tutorial_list.xml` and `scripts/gumps/tutorial_list.lua`.

You need a game master account and a character in the world. Every gump is opened with
[`.gump`](commands/gump.md), which opens any gump on yourself.

## 1. A gump that asks a name

Create `templates/gumps/my_name.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<gump id="my_name" x="120" y="120"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
  <background x="0" y="0" gump="9200" width="320" height="150" />
  <text x="20" y="18" hue="1152">What is your name?</text>
  <image_tiled x="20" y="50" width="280" height="24" gump="2624" />
  <text_entry x="25" y="52" width="270" height="20" hue="1152" entry="1" max_length="30" bind="name">${name}</text_entry>
  <button x="20" y="100" up="4005" down="4007" open="my_greeting" />
  <text x="55" y="101">Next</text>
</gump>
```

- The `id` should be the file's name; it is the name of the gump's script and of its table.
- `xsi:noNamespaceSchemaLocation="gump.xsd"` gives your editor completion and checks: an unknown
  element or a missing attribute is underlined as you type.
- `bind="name"` puts what the player types into the argument `name`. The field starts with
  `${name}`, so it shows the name again when the player comes back to it.
- `open="my_greeting"` makes the button open the next gump, with the same arguments.

## 2. The gump that greets it

Create `templates/gumps/my_greeting.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<gump id="my_greeting" x="120" y="120"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
  <background x="0" y="0" gump="9200" width="320" height="150" />
  <html x="20" y="18" width="280" height="60">Hello, &lt;basefont color=#FFD700&gt;${name}&lt;/basefont&gt;! Welcome to the shard.</html>
  <button x="20" y="100" up="4014" down="4015" open="my_name" />
  <text x="55" y="101">Back</text>
  <button x="180" y="100" up="4005" down="4007" on_click="done" />
  <text x="215" y="101">Done</text>
</gump>
```

`${name}` is the name step 1 bound. In an `html` the value is escaped, so a name such as
`<a href=...>` shows as text and cannot add a link. Back opens step 1 again with the name still in
it; Done calls the function `done` of the gump's script.

## 3. The script

Create `scripts/gumps/my_greeting.lua`. The table is named after the gump:

```lua
my_greeting = {}

-- The Done button: on_click="done".
function my_greeting.done(player, response, args)
    log.info("Player {Player} chose the name {Name}", player, args.name)
end

-- The gump went away without a button: "player", "replaced", "server" or "disconnect".
function my_greeting.on_close(player, args, reason)
    log.info("The greeting was closed: {Reason}", reason)
end
```

`args` is the same table in both gumps and in every callback: it carries `name` from step 1.

## 4. Try it

Restart the server, so it loads the new files, then in game:

```text
.gump my_name
```

Type a name, press Next, then Done: the server log shows the name. A mistake in the XML stops the
server with the file and the line, such as
`my_name.xml: line 6: a button needs exactly one of on_click, id, page or open.`

`.gump` can also fill the placeholders, to try a gump on its own:

```text
.gump my_greeting name=Aria
```

From a script, the same gump opens with `gump.open(player, "my_name", {})`, for instance from an
item's `on_use`.

## 5. A list the script fills

A list changes with the data, so its rows come from the script. The XML keeps the frame and marks
where the rows go with a `<slot>`. Create `templates/gumps/my_list.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<gump id="my_list" x="120" y="80"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
  <background x="0" y="0" gump="9200" width="300" height="300" />
  <text x="20" y="15" hue="1152">The cities of Britannia</text>
  <slot name="rows" x="20" y="45" />
</gump>
```

When the gump opens, the server calls `my_list.rows(g, player, args)`. Whatever the function adds
to the builder `g` appears at the slot, its coordinates counted from the slot. Create
`scripts/gumps/my_list.lua`:

```lua
my_list = {}

local cities = {
    "Britain", "Buccaneer's Den", "Cove", "Jhelom", "Magincia", "Minoc", "Moonglow",
    "Nujel'm", "Ocllo", "Serpent's Hold", "Skara Brae", "Trinsic", "Vesper", "Yew"
}

function my_list.rows(g, player, args)
    g:pager{ previous = { x = 0, y = 200 }, next = { x = 220, y = 200 } }

    for i, city in ipairs(cities) do
        local row = g:paginate(i, 8)

        g:text{ x = 30, y = row * 24, text = city }
        g:button{ x = 0, y = row * 24, up = 4005, down = 4007, on_click = function(player, response, args)
            log.info("Player {Player} picked {City}", player, city)
        end }
    end
end
```

- The builder's methods take the attributes of the XML elements of the same name:
  `g:text{ x = 30, y = 0, text = "Britain" }` is `<text x="30" y="0">Britain</text>`.
- `g:paginate(i, 8)` starts a new page every 8 items, adds the previous and next buttons where
  `g:pager` says, and returns the row of the item on its page, from 0.
- A button's `on_click` can be a function. It sees the variables around it, such as `city`, so every
  row knows which city it is.

`.gump my_list` opens it. A slot function must not call `wait()`: the gump is built while it runs.

## 6. A gump built entirely in Lua

When even the frame depends on the data, build the whole gump in the script and send it:

```lua
local g = gump.create("my_dynamic", 100, 100)
g:background{ x = 0, y = 0, gump = 9200, width = 260, height = 120 }
g:text{ x = 20, y = 20, text = "You have " .. count .. " pets" }
g:button{ x = 20, y = 70, up = 4005, down = 4007, on_click = function(player, response, args)
    log.info("Pressed")
end }
gump.send(player, g, {})
```

A built gump answers like an XML one: `bind`, `open`, texts, `on_close` and the safety checks all
behave the same. See [Gumps](gumps.md) for every element and attribute.
