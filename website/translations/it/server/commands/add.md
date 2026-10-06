<!-- translation: {"sourceHash":"7b06fa4cca1daa62e98517dec8902d053684d58622db6ed3db43b50aa4f01063","title":"add"} -->

# add

Posiziona a terra un oggetto da un template oggetto nel punto selezionato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `add <template>`, poi seleziona una posizione | No | Sì | GameMaster | Game |

```text
.add treasure_chest_level_1
```

Solo in gioco. Viene prima controllato l'id del template (altrimenti
`Unknown item template: <id>`); poi si apre un cursore di selezione e l'oggetto appare
nel punto scelto: `Added treasure chest (0x40001234) at Trammel (1385, 1490, 10).`
Un contenitore arriva con l'oro e il bottino indicati dal template, come un forziere
di una regione di spawn, e il suo script esegue `on_create`. L'oggetto viene salvato
con il mondo e decade secondo il template: un forziere dopo 45 minuti, un oggetto
fisso senza tempo di decadimento mai. Una pila arriva come un oggetto di quantità 1.
Un errore stampa `The item could not be added. Check the server logs.`
I template oggetto sono in `templates/items` (vedi [Caricare template TOML](../templates.md)).
Per un forziere che ritorna dopo essere stato saccheggiato, usa una
[regione di spawn degli oggetti](../spawns.md#regions-of-items-treasure-chests).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`spawn`](spawn.md)
- [`where`](where.md)
