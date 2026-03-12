# Gump Tutorial

Tutorial pratico per creare gump Lua in Moongate v2.

## Obiettivo

Alla fine di questa guida saprai:

- inviare un gump semplice a un player
- gestire i click dei bottoni
- usare il layout file-based (`gump.send_layout`)
- caricare testo esterno da `scripts/texts` dentro un `htmlgump`
- eseguire comandi server dal callback

## Prerequisiti

- script runtime attivo
- file `moongate_data/scripts/init.lua` caricato
- modulo `gump` disponibile (default runtime)
- modulo `text` disponibile (default runtime)

## 1) Gump base con runtime builder

Questo approccio costruisce il gump a runtime.

```lua
local SIMPLE_GUMP_ID = 0xB120
local BTN_HELLO = 101

local function open_simple_gump(session_id, character_id)
    local g = gump.create()
    g:background(0, 0, 320, 160, 9200)
    g:label(24, 20, 1152, "Moongate Gump Tutorial")
    g:button(BTN_HELLO, 24, 56, 4005, 4007)
    g:label(54, 58, 0, "Say hello")

    gump.send(session_id, g, character_id or 0, SIMPLE_GUMP_ID, 120, 80)
end
```

## 2) Callback bottone con `gump.on`

I callback ricevono `ctx` (session, character, button).

```lua
gump.on(0xB120, 101, function(ctx)
    if ctx.session_id ~= nil and ctx.session_id > 0 then
        speech.send(ctx.session_id, "Hello from gump callback.")
    end
end)
```

## 3) Layout file-based (consigliato)

Questo approccio è più pulito quando il gump cresce.

File: `moongate_data/scripts/gumps/tutorial_menu.lua`

```lua
local tutorial_menu = {}

local GUMP_ID = 0xB221
local BTN_SPAWN_DOORS = 201

function tutorial_menu.open(session_id, character_id)
    local layout = {
        ui = {
            { type = "background", x = 0, y = 0, gump_id = 9200, width = 420, height = 180 },
            { type = "alpha_region", x = 12, y = 12, width = 396, height = 156 },
            { type = "label", x = 24, y = 20, hue = 1152, text = "World Tools" },
            { type = "button", id = BTN_SPAWN_DOORS, x = 24, y = 58, normal_id = 4005, pressed_id = 4007, onclick = "on_click" },
            { type = "label", x = 54, y = 60, hue = 0, text = "Spawn doors" }
        },
        handlers = {}
    }

    layout.handlers.on_click = function(ctx)
        local button = tonumber(ctx.button_id) or 0
        if button ~= BTN_SPAWN_DOORS then
            return
        end

        local lines = command.execute("spawn_doors", 1)
        if lines ~= nil and ctx.session_id ~= nil then
            for _, line in ipairs(lines) do
                if type(line) == "string" and line ~= "" then
                    speech.send(ctx.session_id, line)
                end
            end
        end
    end

    return gump.send_layout(session_id, layout, character_id or 0, GUMP_ID, 120, 80)
end

return tutorial_menu
```

## 4) Aprire il gump da un comando GM

File: `moongate_data/scripts/commands/gm/tutorial_gump.lua`

```lua
local tutorial_menu = require("gumps.tutorial_menu")

command.register("tutorial_gump", function(ctx)
    if ctx.session_id == nil or ctx.session_id <= 0 then
        ctx:print_error("This command can only be used in-game.")
        return
    end

    local ok = tutorial_menu.open(ctx.session_id, ctx.character_id or 0)
    if not ok then
        ctx:print_error("Failed to open tutorial gump.")
    end
end, {
    description = "Open tutorial gump example.",
    minimum_account_type = "GameMaster"
})
```

Poi in `init.lua`:

```lua
require("commands/gm/tutorial_gump")
```

Uso in-game:

- `.tutorial_gump`

## 5) Testo esterno in `htmlgump`

File: `moongate_data/scripts/texts/welcome_player.txt`

```txt
# internal note
Welcome to {{ shard.name }}, {{ player.name }}.

Website: {{ shard.website_url }} # visible line
```

Uso da Lua:

```lua
local body = text.render("welcome_player.txt", {
    player = {
        name = "Tommy"
    }
}) or "Welcome."

local g = gump.create()
g:resize_pic(0, 0, 9200, 420, 240)
g:html(20, 20, 380, 180, body, true, true)
gump.send(session_id, g, character_id or 0, 0xB500, 120, 80)
```

Note:

- i file stanno sotto `moongate_data/scripts/texts/**`
- la sintassi è Scriban (`{{ ... }}`)
- `shard.name` e `shard.website_url` sono disponibili di default
- `#` commenta la riga o la parte finale della riga
- `\#` mantiene un `#` letterale

## 6) Troubleshooting rapido

- Errore “Failed to open ... gump”
  - verifica `ctx.session_id` valido
  - verifica che il file richiesto da `require` esista
- Click bottone non intercettato
  - controlla `onclick` nel componente (`"on_click"`)
  - controlla `layout.handlers.on_click`
  - verifica `button_id` usato nel confronto
- Gump vuoto o “rotto”
  - usa prima solo `background + label`
  - aggiungi componenti uno per volta

## Best Practices

- usa `gump.send_layout` per gump complessi
- usa `text.render(...)` per testo lungo, messaggi di benvenuto, regole, libri
- tieni `ui` e `handlers` nello stesso file modulo
- usa costanti per `gumpId` e `buttonId`
- non usare fallback “magici” su `sender_serial`
- logga i click importanti durante debug
