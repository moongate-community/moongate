<!-- translation: {"sourceHash":"9ef58d5871b1181e691463cb2a7db364282632a257397205e4a4828e7e8c8466","title":"Gump"} -->

# Gump

I gump sono le finestre di dialogo aperte dal server su un giocatore: un layout di sfondi, testi, pulsanti,
caselle di selezione e campi di testo, con la risposta del giocatore che torna al server.

Un gump è composto da due file:

- `templates/gumps/<id>.xml`: il layout, verificato rispetto a `templates/gumps/gump.xsd`;
- `scripts/gumps/<id>.lua`: ciò che accade quando il giocatore risponde o lo chiude.

Le parti che cambiano con i dati provengono dallo script: uno `<slot>` nell'XML, oppure un intero gump
costruito in Lua. Inizi con i gump? Segui [Il tuo primo gump](gump-tutorial.md).

## Il layout

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

Con `xsi:noNamespaceSchemaLocation="gump.xsd"`, VS Code (con un'estensione XML) e Rider
completano elementi e attributi e segnalano gli errori mentre scrivi. Il server controlla ogni file
rispetto allo stesso schema all'avvio: un errore lo arresta indicando file, riga e motivo.

`<gump>` accetta `id` (minuscolo; dovrebbe corrispondere al nome file, e lo script viene trovato tramite
ID, non tramite nome file), `x` e `y`, e `closable`,
`movable`, `disposable` e `resizable` (true salvo impostazione a false). I suoi controlli appaiono su ogni pagina;
i controlli dentro ciascuna `<page>` appaiono sulle pagine 1, 2, ... in ordine.

| Elemento | Mostra |
| --- | --- |
| `background` | Uno sfondo ridimensionabile (`gump`, `width`, `height`) |
| `alpha_region` | Una regione trasparente |
| `image`, `image_tiled` | Un'immagine gump (`gump`), con `hue` facoltativo, oppure ripetuta in un riquadro |
| `item` | Una grafica di oggetto (`item`, `hue` facoltativo) |
| `text` | Una riga di testo (`hue`, `message`) |
| `label_cropped` | Testo tagliato entro un riquadro (`hue`, `message`) |
| `html` | Testo HTML in un riquadro (`background`, `scrollbar`, `message`), oppure un messaggio client (`cliloc`, `color`, `args`) |
| `button` | Un pulsante con le sue due immagini (`up`, `down`, entrambe obbligatorie): `on_click` (una funzione dello script), `id` (per `on_button`), `page` (cambia pagina) oppure `open` (apre un altro gump) |
| `checkbox` | Una casella di selezione con le due immagini (`off`, `on`, entrambe obbligatorie; ID `switch`, `checked`, `bind`) |
| `group` con `radio` | Pulsanti di opzione di cui uno può essere attivo (`off`, `on`, entrambi obbligatori; `switch`, `checked`, `bind`) |
| `text_entry` | Un campo di testo (ID `entry`, `hue`, `max_length` fino a 239, `bind`; il testo interno è quello iniziale) |
| `tooltip` | Il tooltip del controllo precedente (`cliloc`, `args`) |
| `item_property` | Il tooltip di un oggetto reale (`serial`) |
| `slot` | Dove lo script aggiunge controlli all'apertura del gump (`name`, `x`, `y`); vedi [Slot](#slots) |

### Testi

Un testo proviene da una di tre fonti:

| Fonte | Come | Tradotto da |
| --- | --- | --- |
| L'elemento | `<text ...>Hello ${name}</text>` | Nessuno |
| Un messaggio server | `message="30083"` su `text`, `label_cropped`, `html` | Il server, da `data/messages`, nella lingua del server |
| Un messaggio client | `cliloc="1011011"` su `html` e `tooltip` | Il client, nella lingua del giocatore |

Il client legge i messaggi client solo in `html` e `tooltip`: lo schema rifiuta `cliloc` su
`text`. Usa messaggi client per i testi standard già presenti nel client (CONTINUE 1011011,
CANCEL 1011012, ...) e messaggi server per i tuoi testi, traducibili dallo shard. Entrambi in
un gump possono mescolare due lingue finché non esistono lingue per giocatore.

### Cosa rifiuta il server

Oltre a ciò che indica lo schema, il server si arresta all'avvio, indicando file e riga, per:

- un `button` senza esattamente uno tra `on_click`, `id`, `page` o `open`;
- un `html` con sia `cliloc` sia `message`, oppure con `cliloc` e testo proprio;
- un `html` con `color` ma senza `cliloc`;
- un `text`, `label_cropped` o `html` con sia `message` sia testo proprio;
- lo stesso `id` di pulsante, lo stesso `switch` (checkbox e radio insieme) o lo stesso `entry`
  due volte in un gump;
- un pulsante `page` che passa a una pagina assente nel gump;
- uno `<slot>` in un gump con elementi `<page>`;
- un `open` che indica un gump inesistente;
- lo stesso `id` di gump in due file.

`gump.send` verifica un gump costruito in Lua con le stesse regole.

### Segnaposto

`${name}` in un testo o numero viene riempito dagli argomenti con cui si apre il gump; uno
mancante è vuoto. Gli ID (`id`, `page`, `switch`, `entry`) e `max_length` accettano solo numeri semplici, così
lo schema può verificarli. `args` (separati da tabulazioni) riempie i marcatori `~1_NAME~` di un messaggio client. `@`, `{` e `}`
vengono rimossi dagli argomenti dei messaggi client, così il nome di un giocatore non può compromettere il layout.

### Gump in sequenza

Un gump può condurre a un altro, con gli stessi argomenti, come una procedura guidata:

- `bind="name"` su un `text_entry`, `checkbox` o `radio` scrive la risposta nell'argomento `name`
  quando il giocatore risponde: il testo, `true`/`false`, oppure lo `switch` del radio attivo (`false`
  quando nessuno del gruppo è attivo);
- `open="other_gump"` su un pulsante apre quel gump con gli argomenti, inclusi i valori associati.

```xml
<text_entry x="20" y="40" width="200" height="20" entry="1" bind="name">${name}</text_entry>
<button x="20" y="80" up="4005" down="4007" open="step2" />
```

`step2` può mostrare `${name}`, e ogni callback di entrambi i gump trova `args.name`. Il server
verifica all'avvio che il gump indicato da un `open` esista. Uno script decide autonomamente dove andare con
`gump.open(player, "step2", args)`.

### Slot

`<slot name="rows" x="20" y="45" />` (numeri semplici, nessun segnaposto) viene riempito all'apertura del gump: il server chiama la funzione
`rows` dello script del gump con un builder, `rows(g, player, args)`, e colloca ciò che aggiunge nello
slot, con coordinate relative allo slot. Le pagine create dalla funzione (`g:page()`, `g:paginate`) vengono
aggiunte come pagine del gump, quindi uno slot non può stare in un gump con elementi `<page>`: il server lo rifiuta
all'avvio. Una funzione slot mancante lascia lo slot vuoto con un avviso; una che fallisce o chiama
`wait()` impedisce l'apertura del gump. Le aggiunte dello slot vengono verificate come un file di gump. Solo
`gump.open` da uno script riempie gli slot: un gump aperto [da C#](#from-c) li lascia vuoti, con
un avviso nel log.

## Gump costruiti in Lua

`gump.create(id, x, y)` fornisce un builder i cui metodi aggiungono i controlli degli elementi XML con lo
stesso nome e gli stessi attributi; `gump.send(player, g, args)` lo apre:

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

| Metodo | Aggiunge |
| --- | --- |
| `g:background{}`, `g:alpha_region{}`, `g:image{}`, `g:image_tiled{}`, `g:item{}` | L'elemento con lo stesso nome |
| `g:text{}`, `g:label_cropped{}`, `g:html{}`, `g:text_entry{}` | L'elemento, con `text = "..."` come testo |
| `g:button{}`, `g:checkbox{}`, `g:radio{}`, `g:tooltip{}`, `g:item_property{}` | L'elemento; `on_click` può essere una funzione |
| `g:group()` | Un gruppo radio: i radio successivi vi appartengono |
| `g:page()` | Una nuova pagina: ciò che segue appare su di essa |
| `g:pager{ previous = {...}, next = {...} }` | Dove `g:paginate` colloca i pulsanti (`x`, `y`, `up`, `down`); chiamalo prima di `g:paginate` |
| `g:paginate(index, per_page)` | Una nuova pagina ogni `per_page` oggetti, con pulsanti tra pagine; restituisce la riga dell'oggetto nella pagina, da 0 |

`g:paginate` legge il pager quando viene eseguito, quindi `g:pager` viene prima. In sua assenza, o per un valore
omesso, il pulsante successivo è a (260, 340) con immagini 4005 e 4007, e il precedente
a (20, 340) con 4014 e 4015.

Ogni metodo tranne `g:paginate` restituisce `g`, quindi le chiamate possono essere concatenate. Un pulsante il cui `on_click` è una
funzione la chiama con `(player, response, args)`; la funzione vede le variabili circostanti, come
`pet` sopra, e appartiene allo script che ha inviato il gump. `gump.send` verifica il gump come un
file e fallisce indicando il motivo, come un `checked` che non è `true` o `false`. Un gump
costruito risponde come uno XML: testi, `bind`, `open` e `on_close` sono gli stessi. I segnaposto vengono
sottoposti a escape in `html`; il testo concatenato da te, come `"Hi " .. name`, no, quindi scrivi
`text = "Hi ${name}"` e passa `name` negli argomenti. L'escape copre `&`, `<`, `>` e `"`, così un
segnaposto in un attributo tra virgolette, come `href="${link}"`, non può uscirne.

I nomi `on_click` che iniziano con `__` sono riservati al server.

## Lo script

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

`response` è `{ button = n, switches = { [id] = true }, text = { [id] = "..." } }`: gli switch
attivi e il testo di ogni campo, già verificati dal server.

Apri e chiudi un gump da qualsiasi script:

```lua
gump.open(player, "release_pet", { pet_name = "Fido", pet = serial })  -- false for an unknown gump
gump.close(player, "release_pet")
```

In gioco, [`.gump release_pet pet_name=Fido`](commands/gump.md) lo apre su di te per provarlo.

Gli argomenti accettano stringhe, numeri e booleani e ritornano a ogni callback.

## Sicurezza

Il server conserva ogni gump aperto su ciascun giocatore e verifica ogni risposta:

- una risposta a un gump non inviato al giocatore, o a cui ha già risposto, viene scartata;
- una risposta con un pulsante, switch o campo di testo assente nel gump, oppure lo stesso campo di testo
  due volte, viene scartata;
- un testo più lungo di 239 caratteri viene scartato (lo schema mantiene `max_length` a 239 o meno);
- aprire un gump con lo stesso ID chiude quello già aperto, e un giocatore mantiene al massimo 64
  gump.

Una risposta contraffatta non raggiunge mai uno script. I client da 5.0.0a ricevono il pacchetto compresso (0xDD),
i precedenti quello semplice (0xB0); vedi la [guida di riferimento dei pacchetti](packets.md).

## Da C#

Un plugin apre i gump di `templates/gumps` con `IGumpTemplateService`:

```csharp
// On the game loop, with a callback:
templates.Open(session, "release_pet", new Dictionary<string, string> { ["pet_name"] = "Fido" },
    (session, answer) => { /* answer.Click is the on_click name, answer.Response the answer */ });

// Off the loop, waiting for the on_click name (null when closed, or for an id button):
var choice = await templates.AskAsync(session, "decorate_confirm", new Dictionary<string, string>());
```

`.decorate` chiede in questo modo con `templates/gumps/decorate_confirm.xml`; gli altri gump forniti sono
`templates/gumps/go.xml`, il menu di viaggio di [`.go`](commands/go.md), `templates/gumps/jail_sentence.xml`,
il gump di [`.jail`](commands/jail.md), e `templates/gumps/gmtools.xml`, gli strumenti di
[`.gmtools`](commands/gmtools.md): un gump con due slot, barra laterale e pannello.

La risposta contiene anche `answer.Open`, il gump indicato da un pulsante `open` (null per qualsiasi altro
pulsante), e `answer.Bound`, i valori dei controlli con `bind` per nome. `AskAsync` segue
autonomamente i pulsanti `open`, aggiungendo i valori associati agli argomenti; con `Open` la callback deve
aprire il gump successivo. `Open` restituisce `false` quando la sessione è terminata. Uno `<slot>` resta vuoto in
un gump aperto da C#: solo `gump.open` da uno script chiama le funzioni degli slot.

Un layout costruito nel codice passa da `IGumpService`, con una voce per comando:

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

Le voci sono `GumpPage`, `GumpGroup`, `GumpBackground`, `GumpAlphaRegion`, `GumpImage`,
`GumpImageTiled`, `GumpItem`, `GumpButton`, `GumpCheckbox`, `GumpRadio`, `GumpText`,
`GumpLabelCropped`, `GumpHtml`, `GumpHtmlLocalized`, `GumpTextEntry`, `GumpTooltip`,
`GumpItemProperty` e `GumpFlag`.

## Lettere leggibili

La [pergamena dei documenti](data-files/books.md) mostra testo semplice salvato con un
corpo scorrevole. Una lettera idonea nello zaino del lettore aggiunge un pulsante localizzato
**Ritira allegati**. Il piè di pagina riduce l'altezza del corpo per mantenerlo
all'interno della pergamena. La chiusura è di sola lettura. Le risposte seguono gli stessi controlli di seriale,
tipo e pulsante offerto degli altri gump; proprietà e sessione originale
vengono ricontrollate prima del ritiro. Un ritiro riuscito riapre lo stesso
testo senza pulsante; un rifiuto per capienza lo riapre per un nuovo tentativo.
