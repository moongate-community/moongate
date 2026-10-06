<!-- translation: {"sourceHash":"6e65adcad33a863e8436134427b1fb775c9e3fe724e4fbc4bef08e0579f9784e","title":"spawn"} -->

# spawn

Genera un NPC da un template di mobile nel punto che selezioni.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `spawn <template>`, poi seleziona un punto | No | Sì | GameMaster | Game |

```text
.spawn orione
```

Solo in gioco. L'id del template viene verificato per primo (altrimenti `Unknown mobile template: <id>`);
poi si apre un cursore di selezione e l'NPC appare nel punto scelto, vestito e con il suo
bottino, ed esegue il proprio `on_spawn` Lua: `Spawned Orione (0x00000123) at Trammel (1496, 1628, 10).`
L'NPC viene salvato con il mondo. Un template con `movement = "water"`, come `dolphin`,
viene generato solo in acqua: sulla terra stampa `dolphin lives in the water: target the water.` Un errore
stampa `The spawn failed. Check the server logs.`
I template dei mobile si trovano in `templates/mobiles` (vedi [Caricamento dei template TOML](../templates.md)).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`remove`](remove.md)
- [`where`](where.md)
