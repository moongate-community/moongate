<!-- translation: {"sourceHash":"09dbffd1fd4aebabee705bf2b6884d8328d9aca98d157836a3cf1b53d4ae3d2e","title":"remove"} -->

# remove

Rimuove l'NPC o l'oggetto a terra che selezioni.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `remove`, poi seleziona un NPC o un oggetto a terra | No | Sì | GameMaster | Game |

```text
.remove
```

Solo in gioco. Seleziona un NPC: scompare per tutti, con ciò che indossa e trasporta, e
la sua riga viene eliminata al successivo salvataggio del mondo: `Removed 0x00000123.` Selezionare un personaggio giocante
stampa `That is not an NPC.` e non rimuove nulla.

Seleziona un oggetto a terra, come una cassa collocata con [`add`](add.md): scompare
per tutti con tutto ciò che contiene, a qualsiasi profondità, e il successivo salvataggio del mondo elimina le righe:
`Removed 0x40001234.` Un oggetto di una regione di spawn libera il proprio posto, quindi la regione ne crea un altro
alla successiva occasione. Un oggetto trasportato, indossato o dentro un contenitore rimane dov'è:
`Only an item lying on the ground can be removed.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`kill`](kill.md)
- [`spawn`](spawn.md)
- [`add`](add.md)
