<!-- translation: {"sourceHash":"f4138279debec7d363b87caedee9820dc8bf7b4de7cb58f851d12f4cc3df78c8","title":"set"} -->

# set

Imposta punti vita, mana, stamina, fame o sete del mobile selezionato, oppure lo rende criminale.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `set <hits\|mana\|stamina\|hunger\|thirst\|criminal> <value>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

```text
.set hits 10
.set hunger 0
.set thirst 0
.set criminal 1
```

Solo in gioco. Si apre un cursore di selezione: scegli un personaggio o NPC, incluso
te stesso. Punti vita, mana e stamina sono mantenuti tra 0 e il massimo del mobile,
e il suo giocatore e quelli attorno vedono muoversi la barra; la fame è mantenuta
tra 0 (affamato) e 20 (sazio), la sete tra 0 (disidratato) e 20 (dissetato).
Il comando risponde con il valore attuale: `Aria: hits is now 10.` Selezionare un
oggetto stampa `That is not a character or an NPC.`

È il modo per vedere la [rigenerazione](../server-configuration.md) all'opera:
abbassa una barra e risale un punto per volta; imposta fame a 0 e i punti vita
restano fermi finché il mobile non mangia; imposta sete a 0 e la stamina resta
ferma finché non beve.

`set criminal 1` rende il mobile [criminale](../server-configuration.md) per il
tempo configurato, con il nome grigio; `set criminal 0` lo perdona subito.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`fame`](fame.md)
- [`karma`](karma.md)
