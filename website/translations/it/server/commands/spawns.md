<!-- translation: {"sourceHash":"66a43ad9c9d88c075c079a69ac7a5cea3981d9f264016cb0983cf8739ce71779","title":"spawns"} -->

# spawns

Elenca le regioni di spawn dove ti trovi, con gli NPC attivi e i minuti al loro prossimo spawn.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `spawns` | No | Sì | GameMaster | Game |

```text
.spawns
```

Solo in gioco. Stampa una riga per regione di spawn la cui area copre la tua posizione, nell'ordine del
file:

```text
The Hammer And Anvil (felucca_0): 1/1, next spawn in 312 min.
```

Prima viene il nome (l'id quando la regione non ne ha uno), poi l'id, gli NPC attivi (o gli oggetti per una regione di oggetti come casse del tesoro) rispetto al
`max` della regione e i minuti fino al successivo controllo di spawn lì. Una regione al proprio `max` ha comunque
un prossimo spawn: non genera nulla in quel momento e attende di nuovo. Una regione il cui ultimo controllo non ha trovato un punto
lo segnala con `no spot found, retrying in 1 min.`, finché non genera di nuovo. `No spawn region here.`
significa che nessuna regione copre il punto. Vedi [Spawn degli NPC](../spawns.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`spawn`](spawn.md)
- [`remove`](remove.md)
