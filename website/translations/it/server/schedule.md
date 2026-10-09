<!-- translation: {"sourceHash":"3a63f39989f8ed01cc04cecea1368a3c9f50248654401f9b02d1ecc7fe9486b4","title":"Calendario"} -->

# Calendario

`data/schedule.toml` è il calendario del server. Contiene due cose: i **task**, che girano a un'ora
del giorno o della settimana, come uno spegnimento con avvisi, e gli **eventi stagionali**, attivi
tra due date, come Halloween. Nessun task gira finché l'operatore non ne scrive uno: il file
fornito ha i task come esempi nei commenti, e gli eventi `halloween` e `christmas` accesi (vedi [Feste](holidays.md)).

```toml
[[task]]
id = "nightly_restart"
when = { every = "day", at = "04:00" }
action = "shutdown"
warnings = [600, 300, 60, 10]

[[task]]
id = "sunday_tip"
when = { every = "week", days = ["sun"], at = "18:00" }
action = "broadcast"
text = "Remember to visit the bank."

[[task]]
id = "daily_cleanup"
when = { every = "day", at = "06:00" }
action = "lua"
script = "cleanup"

[[event]]
id = "halloween"
name = "Halloween"
from = "10-20"
to = "11-02"
```

## Task

| Chiave | Significato |
| --- | --- |
| `id` | Il nome del task e dell'evento. Lettere minuscole, cifre e `_`, al massimo 40, unico nel file. |
| `when.every` | `hour`, `day` o `week`. |
| `when.at` | `"HH:MM"` (24 ore); per `hour`, `":MM"`. |
| `when.days` | Per `week`: da `mon` a `sun`; ogni giorno se manca. |
| `action` | `shutdown`, `broadcast` o `lua`. |
| `warnings` | Per `shutdown`: i secondi prima dello stop a cui tutti vengono avvisati, ognuno minore del precedente, da 1 a 86400. |
| `message` o `text` | Per `broadcast`: l'id di un [messaggio localizzato](localization.md), oppure un testo semplice. Uno dei due. |
| `script`, `function` | Per `lua`: il file `scripts/events/<script>.lua` e la funzione da chiamare, `run` se manca. |

Non ci sono espressioni cron. Un task la cui ora è passata mentre il server era spento non viene
eseguito in ritardo. Un task che fallisce viene scritto nel log con il suo id e non ferma gli altri;
due task nello stesso momento girano entrambi.

### Spegnimento

L'`at` di un task `shutdown` è l'ora **dello stop**; gli avvisi vengono prima. Con il task qui sopra
tutti leggono `The server will shut down in 10 minutes.` alle 03:50, poi 5 minuti, `60 seconds` e
`10 seconds`, e alle 04:00 `The server is shutting down now.`, il mondo viene salvato e il server si
ferma, come con [`shutdown`](commands/shutdown.md). Se il server parte dentro la finestra, partono
solo gli avvisi ancora davanti, con il tempo che resta davvero. Uno spegnimento già in corso,
chiesto dal comando o da un altro task, non viene chiesto due volte.

Il server si ferma; non riparte da solo. Lo fa ciò che lo esegue:

```yaml
# docker compose
restart: unless-stopped
```

```ini
# systemd unit
Restart=always
```

### Task Lua

`scripts/events/cleanup.lua` definisce una tabella globale con il nome del file, e il task chiama la
sua funzione con l'id del task e l'ora Unix a cui era previsto:

```lua
cleanup = {}

function cleanup.run(id, scheduled)
  -- ...
end
```

Uno script o una funzione mancante viene scritto nel log una volta e riprovato alla corsa
successiva.

## Fuso orario

Le ore si leggono nel fuso di `[ultima.schedule] time_zone` (vedi
[Configurazione del server](server-configuration.md)): un id IANA come `Europe/Rome`, oppure vuoto
per il fuso del sistema. Un container Docker è in UTC finché non si imposta `TZ` o questa opzione
non indica un fuso; su Linux i fusi richiedono il pacchetto `tzdata`. Un id sconosciuto ferma
l'avvio.

L'ora legale segue il fuso: un'ora che non esiste il giorno in cui le lancette vanno avanti gira al
primo minuto che esiste, e un'ora che esiste due volte gira una volta sola, la prima.

## Eventi stagionali

Un `[[event]]` ha un `id`, un `name` e una finestra `from` e `to` come `MM-dd`, ogni anno. Entrambi
i giorni sono dentro la finestra, e una finestra può scavalcare il capodanno (da `12-20` a `01-06`).
Le date si leggono nel fuso qui sopra.

Ogni evento ha una **modalità**: `auto` segue le date, `on` e `off` lo forzano. Lo staff la cambia
con [`.event`](commands/event.md), uno script con `schedule.set_event`; la modalità si salva con il
mondo e sopravvive a un riavvio.

Quando un evento inizia o finisce, a mezzanotte del fuso o perché è cambiata la modalità, il server
chiama una funzione di `scripts/events/<id>.lua`, con l'id e il nome:

```lua
halloween = {}

function halloween.on_start(id, name) end
function halloween.on_end(id, name) end
```

Finché un evento è attivo, una terza funzione viene chiamata ogni volta che un personaggio entra nel
mondo: `on_login(id, name, player)`, con il serial del personaggio. Uno shard che non la definisce
non perde nulla. Il [Natale](holidays.md#christmas-snowballs-and-gifts) dà lì il suo regalo.

Uno script o una funzione mancante va bene. L'ultimo stato comunicato si salva con il mondo, quindi
un evento iniziato o finito mentre il server era spento chiama la sua funzione una volta al
prossimo avvio; uno shard che non ha mai avuto l'evento attivo non chiama niente.

## Da Lua

Il [modulo `schedule`](https://moongate.sh/lua/schedule/): `schedule.is_active(id)`,
`schedule.active()`, `schedule.events()`, `schedule.set_event(id, mode)` e `schedule.next(id)`
(l'ora Unix della prossima corsa di un task). Uno script non può aggiungere voci al calendario; un'azione
personalizzata è un task con `action = "lua"`.

## Cosa non c'è

Espressioni cron, esecuzione in ritardo dei task persi mentre il server era spento, un riavvio che
fa ripartire il processo, e il contenuto degli eventi (decorazioni, mostri, dolcetti): gli eventi
dicono solo agli script quando iniziare e finire.
