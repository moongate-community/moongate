<!-- translation: {"sourceHash":"eeeebe40888be5513f16859281b63ac46341cb2161d0526a4fcb65146095ee77","title":"Eventi e timer"} -->

# Eventi e timer

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). Le funzioni sono elencate nella guida di riferimento:
[`events`](https://moongate.sh/lua/events/) e [`timer`](https://moongate.sh/lua/timer/).

## Eventi

Gli script reagiscono agli eventi del server con il modulo integrato `events`:

```lua
local handle = events.on("character_created", function(e)
    log.info("New character {Name}", e.name)
end)

events.off(handle) -- returns false when the handle is unknown
```

- Ogni handler viene eseguito sul ciclo di gioco come coroutine, quindi può chiamare `wait()`.
- Gli handler di un evento vengono eseguiti nell'ordine di iscrizione. Iscriversi o
  annullare l'iscrizione dentro un handler ha effetto dall'evento successivo.
- Ogni handler riceve una propria tabella; modificarla non influisce sugli altri handler.
- Gli eventi sono notifiche: un handler non può annullare o modificare ciò che è accaduto. Un
  errore in un handler viene segnalato come qualsiasi errore di script, e gli altri handler
  vengono comunque eseguiti.
- Le iscrizioni appartengono al file che le ha create. Ricaricare o invalidare il
  file le rimuove, come i suoi timer; arrestare il motore le rimuove tutte.
- Un nome evento sconosciuto genera un errore in `events.on`. Il file generato
  `definitions.lua` elenca i nomi validi come alias `EventName`, così gli editor
  li completano.

### Eventi disponibili

| Evento | Campi |
| --- | --- |
| `character_created` | `serial`, `account_id`, `name`, `race` e `gender` (numeri di `RaceType` e `GenderType`), `map`, `x`, `y`, `z`. Generato dopo il salvataggio di un nuovo personaggio e dei suoi oggetti iniziali. |
| `character_deletion_requested` | `serial`, `account_id`, `name`. Generato dopo che un giocatore chiede di eliminare un personaggio; resta ripristinabile fino alla rimozione. |
| `character_entered_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Generato dopo che un personaggio è entrato nel mondo e il login del client è stato completato. |
| `player_say` | `serial`, `name`, `text`, `type`. Generato dopo che il personaggio di un giocatore ha detto qualcosa e i giocatori e NPC intorno lo hanno sentito; `text` è ciò che hanno sentito e `type` il modo in cui è stato detto, un numero come `SpeechType.Yell`. Un comando (testo che inizia con un punto) non genera nulla. |
| `character_left_world` | `serial`, `account_id`, `name`, `map`, `x`, `y`, `z`. Generato dopo che un personaggio ha lasciato il mondo perché la sessione si è chiusa, una volta tentato il salvataggio. |
| `player_region_changed` | `serial`, `name`, `previous`, `current`, `map`, `x`, `y`, `z`. Generato quando il personaggio di un giocatore cammina o viene teletrasportato da una regione a un'altra, o cambia mappa; `previous` e `current` sono i nomi delle regioni, `nil` fuori da ogni regione, e `previous` è anche `nil` quando il personaggio è appena entrato nel mondo |

### Pubblicare un evento da C#

Host e plugin pubblicano un evento del bus verso Lua con una registrazione esplicita,
prima che il motore si avvii:

```csharp
container.AddScriptEvent<MyEvent>(
    "my_event",
    e => new Dictionary<string, object?> { ["name"] = e.Name, ["amount"] = e.Amount });
```

Il nome deve essere snake_case e univoco, e ogni tipo di evento viene pubblicato una volta.
La mappatura viene eseguita sul thread di pubblicazione e deve solo leggere l'evento. Può
restituire stringhe, booleani, numeri, enum (inviati come numeri) o null. Una mappatura
che fallisce viene registrata nel log, e l'evento viene saltato solo per Lua. Gli eventi pubblicati
quando nessuno script è iscritto costano una ricerca e non vengono accodati.

## Timer e wait

`timer.after` esegue una funzione una volta, `timer.every` la ripete e `timer.cancel` arresta entrambi; `wait` sospende
la coroutine che lo chiama.

Chiama `wait` da una coroutine pianificata, come una callback di timer, non al livello
principale di `init.lua` o di un modulo richiesto. Serve un ritardo positivo e finito che
rientri nell'intervallo del timer. Cede la coroutine invece di bloccare il thread.
Ogni occorrenza di un timer ripetuto avvia una coroutine: se una chiama `wait` per un tempo superiore
all'intervallo di ripetizione, più invocazioni sospese possono coesistere. Annullare
il timer impedisce avvii successivi; non annulla una coroutine già avviata.
Per sequenze che non devono sovrapporsi, usa una callback singola che pianifica la propria
esecuzione successiva solo dopo aver terminato il lavoro.
