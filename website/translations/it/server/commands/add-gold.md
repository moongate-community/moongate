<!-- translation: {"sourceHash":"6a1cf32c758725555ff1533fcec41f573fa222e2cefd24057fbb04b6438e6310","title":"add_gold"} -->

# add_gold

Mette una pila d'oro nello zaino del personaggio o NPC selezionato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `add_gold <1..60000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

```text
.add_gold 1000
```

Solo in gioco. Viene prima controllata la quantità; poi si apre un cursore di selezione
e il personaggio o NPC scelto, incluso te stesso, riceve nello zaino una nuova pila
 di quel numero di monete, creata dal nulla: `1,000 gold is in the backpack of Bran.`

- Una pila contiene 60000 monete, quindi è il massimo per volta. Per una somma maggiore
  crea un [assegno bancario](create_check.md).
- L'oro pesa, una moneta 0,02 stone: chi lo riceve potrebbe allontanarsi sovraccarico.
- Selezionare un oggetto dice `That is not a character or an NPC.`; annullare non
  cambia nulla; un mobile senza zaino, o con lo zaino pieno, non riceve nulla:
  `Bran has no backpack, or it is full.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`create_check`](create_check.md)
- [`add`](add.md)
