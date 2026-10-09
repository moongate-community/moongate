<!-- translation: {"sourceHash":"3841179c6f454ec8f61e369e314838fad7426624606019b2de28db236fbf1525","title":"event"} -->

# event

Mostra gli [eventi stagionali](../schedule.md#seasonal-events) e ne forza uno acceso, spento, o di
nuovo alle sue date.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `event [list]` | Sì | Sì | Administrator | Game |
| `event on <id>` | Sì | Sì | Administrator | Game |
| `event off <id>` | Sì | Sì | Administrator | Game |
| `event auto <id>` | Sì | Sì | Administrator | Game |

```text
.event list
halloween: Halloween, 10-20 to 11-02, mode auto, active
winter: Winter, 12-20 to 01-06, mode auto, inactive

.event off halloween
Event halloween is now off.
```

`on` e `off` hanno la precedenza sulle date finché `auto` non le restituisce all'evento; la modalità
si conserva tra i riavvii. Un cambio che avvia o termina l'evento chiama il suo `on_start` o
`on_end`. Un id che non è in `data/schedule.toml` stampa `No event is called x. Events: halloween, winter`.
