<!-- translation: {"sourceHash":"528bc3c6495c951fcc572cd48161790876920c77ea1937f64b188fed01b31a23","title":"script"} -->

# script

Ricarica uno script Lua oppure stampa i contatori del motore degli script.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `script reload <file>` / `script metrics` | Sì | No | — | Game |

```text
script reload ai/guard.lua
script metrics
```

`script reload` ricarica un file relativo alla directory `scripts/` configurata.
Esegue la ricarica sul game loop e segnala un errore se lo script non viene
caricato. `script metrics` stampa i contatori correnti del motore Lua.

## Vedi anche

- [Tutti i comandi](../commands.md)
