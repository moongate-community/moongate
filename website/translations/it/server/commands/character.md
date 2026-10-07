<!-- translation: {"sourceHash":"fa49932219ab5296b9e1f6765d23f6aa9538707cd4a5eb1441134b718257aed1","title":"character"} -->

# character

Elenca i personaggi in attesa di eliminazione e li ripristina.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `character pending [account-serial]` / `character restore <character-serial>` | Sì | Sì | GameMaster | Game |

```text
character pending [account-serial]
character restore <character-serial>
```

Un personaggio che un giocatore elimina dall'elenco viene solo marcato per l'eliminazione:
scompare dall'elenco, libera il proprio slot e non conta più nel limite
`ultima.characters.max_per_account`, quindi il giocatore può creare un nuovo personaggio al suo
posto. Resta ripristinabile fino alla rimozione; dopo
`ultima.characters.deletion_delay_hours` (valore predefinito 24) diventa idoneo alla rimozione,
tramite un job non ancora realizzato. `character pending` elenca ogni
personaggio in attesa, o quelli di un account, con il momento della richiesta di eliminazione
e quello in cui diventa idoneo alla rimozione. `character restore` annulla
l'eliminazione e assegna al personaggio il primo slot libero; se nel frattempo l'account si è
riempito, resta senza slot, può superare il limite di uno e appare nell'
elenco quando uno slot si libera. I seriali
sono esadecimali con `0x` (`0x0000002A`) o decimali.

## Vedi anche

- [Tutti i comandi](../commands.md)
