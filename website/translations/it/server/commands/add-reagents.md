<!-- translation: {"sourceHash":"f649f2f685d616cb2f4e6bd8f56d050d778ef620b591e176ff008b1f6aa6db59","title":"add_reagents"} -->

# add_reagents

Mette nello zaino i reagenti di un incantesimo, di un cerchio o di tutti gli incantesimi.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `add_reagents <key \| number \| circle N \| all> [amount]` | No | Sì | GameMaster | Game |

```text
.add_reagents recall
.add_reagents circle 4 100
.add_reagents all 500
```

Solo in gioco; non c'è nessun cursore di selezione. I reagenti sono quelli che gli incantesimi indicano in
[`spells.toml`](data-files/spells.md), una pila per ogni tipo di reagente, `amount` di ciascuno: 20 se lo ometti, da 1 a
1000. Un reagente che usano più incantesimi viene dato una volta sola. Il comando risponde che cosa ha dato:
`Reagents in your backpack, 20 of each: black pearl, blood moss, mandrake root.`

- L'incantesimo si indica come in [`add_spell`](add_spell.md): per chiave, per numero, `circle N` o `all`. Con `all` ottieni
  gli otto reagenti classici.
- Una pila si unisce alla pila dello stesso tipo che hai già nello zaino, come farebbe un rilascio.
- Una pila per cui lo zaino non ha spazio viene messa a terra ai tuoi piedi, e il comando lo dice:
  `20 of each did not fit the backpack and lie at your feet: garlic.`
- Un incantesimo o un cerchio sconosciuto, o una quantità fuori da 1 a 1000, riceve risposta prima che venga creato
  qualcosa.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`add_spell`](add_spell.md)
- [Magery](../magery.md)
