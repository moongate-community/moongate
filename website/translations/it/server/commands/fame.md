<!-- translation: {"sourceHash":"0212bc1654cc9a19d123c1a40de89f7ad41c6df7fc51798a49b6dd34a72004fa","title":"fame"} -->

# fame

Imposta la fama del personaggio o NPC selezionato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `fame <0..32000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

```text
.fame <0..32000>
.karma <-32000..32000>
```

Solo in gioco. Viene prima controllato il valore; poi si apre un cursore di selezione
e il personaggio o NPC scelto lo riceve: `Bran now has 10000 fame.` Selezionare un
oggetto o annullare non cambia nulla. Il titolo del paperdoll si aggiorna subito:
il [prefisso di fama e karma](../data-files/titles.md), che dice `Lord` o `Lady` da
10.000 di fama. Il valore viene salvato con il mobile al suo salvataggio successivo.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`karma`](karma.md)
