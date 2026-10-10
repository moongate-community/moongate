<!-- translation: {"sourceHash":"3199b9ce7d3d23f2e7f465f7e6e00735f30846663989afc39da1617669e81553","title":"mark_rune"} -->

# mark_rune

Segna la runa di richiamo che indichi con il punto in cui ti trovi.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `mark_rune`, poi indica una runa | No | Sì | GameMaster | Game |

```text
.mark_rune
```

Solo in gioco. Si apre un cursore di mira: scegli una runa di richiamo (l'oggetto `recall_rune`), nello zaino o a terra. La runa
conserva la tua mappa e il tuo punto (`rune.x`, `rune.y`, `rune.z`, `rune.map`) e prende il nome della regione in cui ti
trovi, `a recall rune for Britain`, o della mappa fuori da ogni regione. Il comando risponde `The rune is marked for
Britain.`; un altro oggetto risponde `That is not a recall rune.`

L'[incantesimo Recall](../magery.md#recall-e-rune) porta il lanciatore nel punto di una runa segnata, a meno che una regione
lo vieti. Una runa può essere segnata di nuovo: conserva l'ultimo punto.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Magery](../magery.md)
