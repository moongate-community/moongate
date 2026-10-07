<!-- translation: {"sourceHash":"add621f87cbd304b466a5f85eabac0b43ccacfce54f10c718f26fc6bf70fb95c","title":"Il tuo primo gump"} -->

# Il tuo primo gump

Questo tutorial costruisce tre gump passo dopo passo: una registrazione in due passaggi che chiede un nome e poi
lo saluta, e un elenco riempito e paginato dallo script. I file completati sono distribuiti con il server, quindi
puoi aprirli subito e confrontarli con i tuoi (si chiamano `tutorial_*` invece
di `my_*`):

- `templates/gumps/tutorial_name.xml`, `templates/gumps/tutorial_greeting.xml` e
  `scripts/gumps/tutorial_greeting.lua`;
- `templates/gumps/tutorial_list.xml` e `scripts/gumps/tutorial_list.lua`.

Servono un account game master e un personaggio nel mondo. Ogni gump viene aperto con
[`.gump`](commands/gump.md), che apre qualsiasi gump su di te.

## 1. Un gump che chiede un nome

Crea `templates/gumps/my_name.xml`:

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

- L'`id` dovrebbe essere il nome del file; è il nome dello script del gump e della sua tabella.
- `xsi:noNamespaceSchemaLocation="gump.xsd"` offre completamento e controlli nell'editor: un elemento
  sconosciuto o un attributo mancante viene sottolineato mentre scrivi.
- `bind="name"` inserisce ciò che il giocatore digita nell'argomento `name`. Il campo inizia con
  `${name}`, quindi mostra di nuovo il nome quando il giocatore vi ritorna.
- `open="my_greeting"` fa aprire al pulsante il gump successivo, con gli stessi argomenti.

## 2. Il gump che lo saluta

Crea `templates/gumps/my_greeting.xml`:

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

`${name}` è il nome associato nel passaggio 1. In un `html` il valore viene sottoposto a escape, quindi un nome come
`<a href=...>` appare come testo e non può aggiungere un link. Back riapre il passaggio 1 con il nome ancora
presente; Done chiama la funzione `done` dello script del gump.

## 3. Lo script

Crea `scripts/gumps/my_greeting.lua`. La tabella prende il nome del gump:

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

`args` è la stessa tabella in entrambi i gump e in ogni callback: contiene `name` dal passaggio 1.

## 4. Provalo

Riavvia il server affinché carichi i nuovi file, poi in gioco:

```text
.gump my_name
```

Digita un nome, premi Next, poi Done: il log del server mostra il nome. Un errore nell'XML arresta il
server indicando file e riga, come
`my_name.xml: line 6: a button needs exactly one of on_click, id, page or open.`

`.gump` può anche riempire i segnaposto, per provare un gump da solo:

```text
.gump my_greeting name=Aria
```

Da uno script, lo stesso gump si apre con `gump.open(player, "my_name", {})`, per esempio dall'
`on_use` di un oggetto.

## 5. Un elenco riempito dallo script

Un elenco cambia con i dati, quindi le righe provengono dallo script. L'XML conserva la cornice e indica
 dove vanno le righe con uno `<slot>`. Crea `templates/gumps/my_list.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<gump id="my_list" x="120" y="80"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
  <background x="0" y="0" gump="9200" width="300" height="300" />
  <text x="20" y="15" hue="1152">The cities of Britannia</text>
  <slot name="rows" x="20" y="45" />
</gump>
```

All'apertura del gump, il server chiama `my_list.rows(g, player, args)`. Qualsiasi cosa aggiunta dalla funzione
al builder `g` appare nello slot, con coordinate relative allo slot. Crea
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

- I metodi del builder accettano gli attributi degli elementi XML con lo stesso nome:
  `g:text{ x = 30, y = 0, text = "Britain" }` è `<text x="30" y="0">Britain</text>`.
- `g:paginate(i, 8)` inizia una nuova pagina ogni 8 elementi, aggiunge i pulsanti precedente e successivo dove
  indica `g:pager` e restituisce la riga dell'elemento nella sua pagina, da 0.
- L'`on_click` di un pulsante può essere una funzione. Vede le variabili circostanti, come `city`, quindi ogni
  riga sa quale città rappresenta.

`.gump my_list` lo apre. Una funzione slot non deve chiamare `wait()`: il gump viene costruito mentre viene eseguita.

## 6. Un gump costruito interamente in Lua

Quando anche la cornice dipende dai dati, costruisci l'intero gump nello script e invialo:

```lua
local g = gump.create("my_dynamic", 100, 100)
g:background{ x = 0, y = 0, gump = 9200, width = 260, height = 120 }
g:text{ x = 20, y = 20, text = "You have " .. count .. " pets" }
g:button{ x = 20, y = 70, up = 4005, down = 4007, on_click = function(player, response, args)
    log.info("Pressed")
end }
gump.send(player, g, {})
```

Un gump costruito risponde come uno XML: `bind`, `open`, testi, `on_close` e controlli di sicurezza
si comportano allo stesso modo. Vedi [Gump](gumps.md) per ogni elemento e attributo.
