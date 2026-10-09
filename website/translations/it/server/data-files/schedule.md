<!-- translation: {"sourceHash":"b6316e2f23137352880d0a175716c2a359392ab4f9cbc233448a64429996bf35","title":"Calendario"} -->

# Calendario

`schedule.toml` è il calendario dei [task a orario e degli eventi stagionali](../schedule.md): uno
spegnimento con avvisi, un messaggio a tutti, la chiamata di una funzione Lua, e le date di eventi
come Halloween.

```toml
[[task]]
id = "nightly_restart"
when = { every = "day", at = "04:00" }
action = "shutdown"
warnings = [600, 300, 60, 10]

[[event]]
id = "halloween"
name = "Halloween"
from = "10-20"
to = "11-02"
```

| Campo | Significato |
| --- | --- |
| `[[task]]` | Uno per ogni task a orario: `id`, `when` (`every`, `at`, `days`), `action` e ciò che serve all'azione. |
| `[[event]]` | Uno per ogni evento stagionale: `id`, `name`, `from` e `to` come `MM-dd`, entrambi inclusi. |

Il file fornito ha i task come esempi nei commenti, quindi nessuno gira finché l'operatore non ne
attiva uno, e l'evento `halloween` acceso ([Feste](../holidays.md)). Il file può mancare: il
calendario è allora vuoto.

## Validazione all'avvio

Il server si ferma all'avvio, indicando il file e la voce, quando:

- una chiave è sconosciuta, oppure un `id` manca, è ripetuto (task ed eventi condividono lo spazio dei nomi) o non è
  `[a-z0-9_]+` di al massimo 40 caratteri;
- `when.every` non è `hour`, `day` o `week`, `at` non è un `HH:MM` valido (o `:MM` per `hour`), oppure
  `days` indica un giorno che non è da `mon` a `sun`;
- l'`action` è sconosciuta, un `broadcast` ha sia `message` sia `text` o nessuno dei due, un task `lua` non ha
  `script`, oppure `warnings` non sono secondi decrescenti da 1 a 86400;
- un evento ha un `from` o un `to` che non è una vera data `MM-dd`.

## Vedi anche

- [Calendario](../schedule.md)
- [`event`](../commands/event.md)
- [Configurazione del server](../server-configuration.md) per il fuso orario
