<!-- translation: {"sourceHash":"745d16d8fbc1bd5bb85df325cbf4e9ead1380576d1cf334868bb4e67e230a0f5","title":"animate"} -->

# animate

Fa eseguire al personaggio o all'NPC che selezioni un'azione del suo corpo.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `animate <action>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

```text
.animate 21
```

Solo in gioco. Chiunque veda il mobile lo vede eseguire l'azione una volta, e leggi `an orc plays
action 21.` L'azione è un numero da 0 a 65535 del corpo del mobile: un umano, un mostro e un
animale non le condividono. Per un corpo umano 21 cade morto in avanti e 22 all'indietro, 32 fa un inchino, 33
saluta, 34 mangia; i nomi sono quelli di `HumanAnimationType`, `MonsterAnimationType` e
`AnimalAnimationType` nel [riferimento Lua](../scripting.md). Non cambia nient'altro: un mobile che
esegue la propria morte resta vivo.

- Un oggetto, o qualcosa che non è un mobile: `That is not a character or an NPC.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`kill`](kill.md)
