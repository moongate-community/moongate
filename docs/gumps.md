# Gumps

Gumps are the dialogs the server opens on a player: a layout of backgrounds, texts, buttons,
checkboxes and text fields, and the player's answer coming back to the server.

This page describes the gump core, which C# plugins can use today. XML layouts with Lua scripts
and dynamic gumps built from Lua come next ([issue #215](https://github.com/moongate-community/moongate/issues/215)).

## Opening a gump from C#

Build a `GumpLayout` with one entry per command and open it through `IGumpService`, on the game
loop:

```csharp
var layout = new GumpLayout()
    .Add(new GumpBackground { X = 0, Y = 0, GumpId = 5054, Width = 270, Height = 120 })
    .Add(new GumpHtmlLocalized { X = 20, Y = 15, Width = 230, Height = 60, Cliloc = 1046257 })
    .Add(new GumpButton { X = 20, Y = 80, Up = 4005, Down = 4007, ButtonId = 1 })
    .Add(new GumpText { X = 55, Y = 80, Text = "Continue" });

gumps.Open(session, new GumpInstance
{
    Id = "release_pet",
    Layout = layout,
    X = 100,
    Y = 100,
    OnResponse = (session, response) =>
    {
        if (response.ButtonId == 1)
        {
            // The player pressed Continue.
        }
    }
});
```

`OnResponse` runs on the game loop with the player's answer: `ButtonId` (0 when the gump was
closed), `Switches` (the checkboxes and radios that are on) and `Texts` (the text of each text
entry, by id).

## Entries

| Entry | Command | What it shows |
| --- | --- | --- |
| `GumpPage` | `page` | A page; page 0 shows on every page |
| `GumpGroup` | `group` | A radio group |
| `GumpBackground` | `resizepic` | A resizable background |
| `GumpAlphaRegion` | `checkertrans` | A see-through region |
| `GumpImage` | `gumppic` | A gump image, with an optional hue |
| `GumpImageTiled` | `gumppictiled` | A tiled gump image |
| `GumpItem` | `tilepic`, `tilepichue` | An item graphic |
| `GumpButton` | `button` | A reply button (`ButtonId`) or a page button (`Page`) |
| `GumpCheckbox`, `GumpRadio` | `checkbox`, `radio` | A switch (`SwitchId`) |
| `GumpText` | `text` | A line of text |
| `GumpLabelCropped` | `croppedtext` | Text cut to a box |
| `GumpHtml` | `htmlgump` | HTML text in a box |
| `GumpHtmlLocalized` | `xmfhtmlgump`, `xmfhtmlgumpcolor`, `xmfhtmltok` | A client message (cliloc), coloured or with arguments |
| `GumpTextEntry` | `textentry`, `textentrylimited` | A text field (`EntryId`) |
| `GumpTooltip` | `tooltip` | The tooltip of the entry before it |
| `GumpItemProperty` | `itemproperty` | The tooltip of a real item |
| `GumpFlag` | `nomove`, `noclose`, `nodispose`, `noresize` | A flag of the whole gump |

## Safety

The server keeps every gump it opens on each player, and checks every answer:

- an answer for a gump the player was not sent, or already answered, is dropped;
- an answer with a button, switch or text entry the gump does not have is dropped;
- a text longer than 239 characters is dropped;
- opening a gump with the same id closes the one already open, and a player keeps at most 64
  gumps.

A forged answer never reaches `OnResponse`. Clients from 5.0.0 get the compressed packet (0xDD),
older ones the plain one (0xB0); see the [packet reference](packets.md).
