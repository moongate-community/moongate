<!-- translation: {"sourceHash":"8d9cbf284f646ba1429289cfae269102face64808a706a9d455593638113fcd2","title":"resurrect"} -->

# resurrect

Rialza l'NPC di cui selezioni il cadavere, oppure il fantasma di un giocatore che selezioni.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `resurrect`, poi seleziona un cadavere o un fantasma | No | Sì | GameMaster | Game |

```text
.resurrect
```

Solo in gioco. Seleziona il cadavere di un NPC: uno dello stesso template di mobile nasce dove giace il
cadavere, con il nome e l'orientamento di chi è morto, e leggi `Tiara is back.` Un corpo umano, elfico o
di gargoyle si rialza con la sua caduta riprodotta al contrario. Il cadavere sparisce, con ciò che vi era rimasto dentro.

L'NPC arriva con l'equipaggiamento del suo template, come uno nuovo: ciò che è stato preso dal
cadavere resta preso, e ciò che vi era rimasto non viene restituito. Uno che apparteneva a una regione di spawn
torna a contare per essa. Vedi [Morte e resurrezione](../death.md#raising-who-died).

Se invece selezioni un giocatore morto, viene rialzato dove si trova, come da un ankh: 10 punti ferita, stamina piena, niente mana e una veste della morte al posto del sudario. Vedi [Morte di un giocatore](../death.md#death-of-a-player).

- Né un cadavere a terra né un fantasma: `That is not a corpse.`
- Il cadavere di un NPC che non aveva un template, o il cui template non c'è più, o che qualcuno sta già
  rialzando: `That corpse cannot be raised.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Morte e resurrezione](../death.md)
- [`kill`](kill.md)
