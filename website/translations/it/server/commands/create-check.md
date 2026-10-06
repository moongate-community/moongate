<!-- translation: {"sourceHash":"02a499806a599c1ba4876d3987aacbe1959738b18e56c5b1dfa92cd8319641c5","title":"create_check"} -->

# create_check

Mette nel tuo zaino un assegno bancario del valore d'oro indicato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `create_check <1..2000000000>` | No | Sì | GameMaster | Game |

```text
.create_check 2000
```

Solo in gioco. L'assegno è creato dal nulla, nessuna banca lo paga, e finisce nel
tuo zaino: `A bank check worth 2,000 gold is in your backpack.` È lo stesso oggetto
emesso da un banchiere ([assegni bancari](../bank.md#bank-checks)): mettilo in una
cassetta bancaria e fai doppio clic lì per convertirlo in monete.

- La quantità è un numero intero da 1 a 2.000.000.000, senza separatori: l'assegno
  minimo e massimo di un banchiere ([`min_check`, `max_check`](../bank.md#settings))
  non ti limitano.
- Senza zaino, o con lo zaino pieno, non viene creato nulla:
  `No check was made: you have no backpack,
or it is full.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`add_gold`](add_gold.md)
- [La banca](../bank.md)
