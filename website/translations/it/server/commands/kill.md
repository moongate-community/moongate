<!-- translation: {"sourceHash":"882334e2cb762a20a45a21967923e288941f26e8d56b373695bfaf40f94a2b9d","title":"kill"} -->

# kill

Uccide l'NPC o il giocatore che selezioni: muore dove si trova e lascia il suo cadavere.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `kill`, poi seleziona un NPC | No | Sì | GameMaster | Game |

```text
.kill
```

Solo in gioco. Seleziona un NPC o un giocatore: muore come quando qualcosa lo uccide, con il suo cadavere, la morte
vista e sentita intorno e il suo script avvisato, e leggi `an orc is dead.` Resti registrato come suo uccisore sul
cadavere. Vedi [Morte e resurrezione](../death.md) per ciò che il cadavere contiene e per quanto resta a terra.

- Un giocatore già morto, o il cui corpo non ha un fantasma: `Aria cannot die.` Un giocatore muore come un NPC, ma resta come fantasma: vedi [Morte di un giocatore](../death.md#death-of-a-player).
- Un oggetto, o un mobile che non c'è più: `That is not an NPC.`

Per togliere un NPC senza cadavere, usa [`remove`](remove.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Morte e resurrezione](../death.md)
- [`resurrect`](resurrect.md)
- [`remove`](remove.md)
- [`spawn`](spawn.md)
