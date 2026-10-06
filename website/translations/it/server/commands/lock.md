<!-- translation: {"sourceHash":"c1db478a5385e12dbbd5dc0131667cbd3f61235ad93148e0ce7874a37d1f8bb0","title":"lock"} -->

# lock

Blocca la porta selezionata e quella collegata a essa.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `lock`, poi seleziona una porta | No | Sì | GameMaster | Game |

```text
.lock
.unlock
```

Solo in gioco. Si apre un cursore di selezione; la porta scelta e quella collegata
(una porta doppia) ottengono o perdono la proprietà `locked`: `The door is now locked.`
Una porta chiusa e bloccata non si apre per i giocatori, che leggono "È chiusa a chiave.";
game master e amministratori la aprono comunque (vedi lo
[script della porta](../scripting/shipped-scripts.md#doorlua)). Il blocco assegna
anche a entrambe le porte un numero di chiave (proprietà `key.value`) se non ne
hanno uno. Selezionare qualcosa che non è una porta stampa `That is not a door.`
Il blocco viene salvato con la porta dal successivo salvataggio del mondo.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`unlock`](unlock.md)
- [`key`](key.md)
