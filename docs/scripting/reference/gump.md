# gump

Server-drawn dialogs: a window the server describes and the client renders, from a
confirmation box to a bank window.

## gump.show

```lua
gump.show(serial, id, build, on_response) -> boolean
```

Draws a gump for the player behind `serial` and sends it. Returns `false` — and draws
nothing — when the serial names nobody online.

`id` is your name for this gump. It decides the *type* the client uses to tell whether an
incoming gump replaces one already on screen, and it is what [`gump.close`](#gumpclose)
takes. Keep it stable: two gumps sharing an id are the same window as far as the client is
concerned.

`build` receives a builder. Every element is one call taking **one named table**, so the
order of the numbers is in the field names rather than in your memory:

```lua
gump.show(player, "bank", function(g)
    g.background { x = 0,   y = 0,   w = 300, h = 200, art = 5054 }
    g.label      { x = 20,  y = 20,  hue = 1153, text = "Bank of Britain" }
    g.item       { x = 30,  y = 60,  id = 0x0EED }
    g.button     { x = 250, y = 60,  art = 4005, pressed = 4007, id = 1 }
    g.entry      { x = 20,  y = 100, w = 200, h = 20, id = 1 }
end, function(r)
    if r.button == 1 then
        chat.say(player, "You typed " .. (r.text[1] or ""))
    end
end)
```

Any key you leave out takes its default, so `g.image { x = 10, y = 10, art = 100 }` is an
unhued image without you having to say so.

> [!IMPORTANT]
> **There is no blocking form, and there cannot be one.** Script handlers run on the
> game-loop thread, so a call that waited for the player's answer would stop the world for
> everyone. If you are coming from POL, where `SendDialogGump` returns the pressed button,
> the callback is where that value arrives instead.

## The response

The callback receives one table:

| field | meaning |
|---|---|
| `button` | the id of the button pressed; `0` is the client's own close button |
| `switches` | a list of the ids of every ticked checkbox and selected radio |
| `text` | what was typed, keyed by the `id` you gave each `entry` |

**A gump answers once.** Pressing a reply button closes it on the client, so the server
forgets it as the answer arrives. A window that should stay up is simply sent again from
its own callback — which is also how you refresh one after acting on it.

A response naming a button, switch or text field the gump never drew is treated as
fabricated rather than mistaken: it is logged and the connection is closed.

## gump.close

```lua
gump.close(serial, id) -> boolean
```

Forgets a named gump for that player, so a late answer to it is no longer accepted.
Returns `false` when no gump with that id was open.

## Elements

All twenty-one, with the fields each reads. Omitted fields fall back to the value shown.

### Frames and decoration

| call | fields |
|---|---|
| `g.background` | `x`, `y`, `w`, `h`, `art` |
| `g.alpha` | `x`, `y`, `w`, `h` — a translucent panel |
| `g.image` | `x`, `y`, `art`, `hue` (0) |
| `g.image_tiled` | `x`, `y`, `w`, `h`, `art` |
| `g.sprite` | `x`, `y`, `art`, `w`, `h`, `sx`, `sy` — a cropped piece of a larger image |
| `g.item` | `x`, `y`, `id` (an item id), `hue` (0) |
| `g.master_gump` | `art` — overrides the frame art the client wraps the gump in |

### Text

| call | fields |
|---|---|
| `g.label` | `x`, `y`, `hue`, `text` |
| `g.label_cropped` | `x`, `y`, `w`, `h`, `hue`, `text` — clipped to the box |
| `g.label_html` | `x`, `y`, `w`, `h`, `text`, `hue` (`"#FFFFFF"`), `size` (4), `center` (false) |
| `g.html` | `x`, `y`, `w`, `h`, `text`, `background` (false), `scrollbar` (false) |
| `g.html_localized` | `x`, `y`, `w`, `h`, `cliloc`, `args`, `color`, `background` (false), `scrollbar` (false) |
| `g.tooltip` | `cliloc`, `args` |
| `g.item_property` | `serial` — binds the client's item tooltip to that item |

`g.html_localized` picks its command from what you give it: the plain form with neither
`args` nor `color`, the coloured form with `color`, and the argument form with `args`.

### Interaction

| call | fields |
|---|---|
| `g.button` | `x`, `y`, `art`, `pressed`, `id`, `type` (1), `page` (0) |
| `g.button_art` | `x`, `y`, `art`, `pressed`, `id`, `type` (1), `page` (0), `item`, `hue`, `w`, `h` |
| `g.check` | `x`, `y`, `art`, `pressed`, `id`, `checked` (false) |
| `g.radio` | `x`, `y`, `art`, `pressed`, `id`, `checked` (false) |
| `g.entry` | `x`, `y`, `w`, `h`, `id`, `hue` (0), `text` (`""`) |
| `g.page` | `index` |
| `g.group` | `index` — radios in the same group are mutually exclusive |

A button's `type` decides what pressing it does. `1`, the default, sends the response and
closes the gump. `0` makes the client jump to the page in `page` **without contacting the
server at all** — which is why multi-page gumps cost nothing and need no bookkeeping:

```lua
gump.show(player, "help", function(g)
    g.background { x = 0, y = 0, w = 400, h = 300, art = 5054 }

    g.page { index = 1 }
    g.label  { x = 20, y = 20, hue = 1153, text = "Page one" }
    g.button { x = 20, y = 260, art = 4005, pressed = 4007, id = 0, type = 0, page = 2 }

    g.page { index = 2 }
    g.label { x = 20, y = 20, hue = 1153, text = "Page two" }
end)
```

## A complete example

The fragments above are fragments. `example_signpost.lua` ships with the server as a whole
working script: double-clicking the item opens a menu of destinations with
a checkbox, and the response teleports the player and optionally makes them announce
themselves. It shows the whole loop — draw, wait, answer, act — including the two things that
are easy to get wrong the first time:

- `ctx.actor` can be **nil**, and there is then nobody to show a gump to;
- the mobile can be **gone by the time the player answers**, which is why the callback goes
  through [`mobile.ref`](mobile.md#mobileref) rather than assuming the target is still there.

Point an item template at it with `ScriptId: items.example_signpost` — see
[item scripts](item-scripts.md).

> [!NOTE]
> Item scripts are seeded **lazily**, the first time the server resolves a `ScriptId`. On a
> bare first boot `scripts/items/` does not exist yet, so do not go looking for the file
> before an item that uses one has been loaded.

## Drawing gumps with an editor

Positions are pixels, and pixels are easier dragged than typed. The Ultima Online community
has visual gump editors — GumpForge, UOGumpEditor, UOGumpy — that let you place art on a
canvas and export code. They emit C# against the RunUO `Add*` API, which Moongate's element
set deliberately mirrors argument for argument, so an exported line maps onto its call here
by name.
