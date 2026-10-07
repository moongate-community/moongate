<!-- translation: {"sourceHash":"f6cf9b9a28a9f3925dc226245b2155c46c28b4625b1c25757cfb772dc63d6c8a","title":"unlock"} -->

# unlock

Sblocca la porta che selezioni e quella collegata.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `unlock`, poi seleziona una porta | No | Sì | GameMaster | Game |

```text
.lock
.unlock
```

Solo in gioco. Si apre un cursore di selezione; la porta che scegli e quella collegata (una porta
doppia) ricevono o perdono la proprietà `locked`: `The door is now locked.` Una porta chiusa e bloccata non
si apre per i giocatori, che leggono "È chiusa a chiave."; i game master e gli amministratori possono comunque aprirla
(vedi lo [script della porta](../scripting/shipped-scripts.md#doorlua)). Il blocco assegna inoltre a entrambe le porte un numero di chiave (proprietà
`key.value`) se non ne hanno uno. Selezionare qualcosa che non è una porta stampa
`That is not a door.` Il blocco viene salvato con la porta al successivo salvataggio del mondo.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`lock`](lock.md)
- [`key`](key.md)
