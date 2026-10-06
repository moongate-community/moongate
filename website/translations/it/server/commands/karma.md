<!-- translation: {"sourceHash":"663678c0a1b30398f19b8d452b3be29ddeb0c04ab11224c9c2351f200c3e6c59","title":"karma"} -->

# karma

Imposta il karma del personaggio o NPC selezionato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `karma <-32000..32000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

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
- [`fame`](fame.md)
