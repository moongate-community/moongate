<!-- translation: {"sourceHash":"b73aac7cb32351b4b391573f8ca9fa559501c2b0d57672b9af0b26128fb8fffa","title":"globallight"} -->

# globallight

Assegna a ogni giocatore la stessa luce, oppure torna all'ora del giorno.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `globallight [0-31]` | Sì | Sì | GameMaster | Game |

```text
globallight 26
globallight
```

In gioco i game master usano `.globallight 26`. Con un livello da 0 (massima luminosità) a 31 (massima oscurità),
ogni giocatore nel mondo riceve subito quella luce: `The global light is now 26.` Senza
livello, la luce torna a seguire l'ora del giorno (vedi
[il ciclo della luce](../server-configuration.md)). La forzatura non viene salvata: un riavvio torna
all'ora del giorno.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`weather`](weather.md)
- [`gmtools`](gmtools.md): gli stessi livelli come pulsanti
