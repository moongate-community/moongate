<!-- translation: {"sourceHash":"5bf1fdfbc9223d70146518586e8bf3083481eb22797144fef8ef17dbfb814fb3","title":"tame"} -->

# tame

Assegna a te stesso, o a un personaggio che indichi per nome, la creatura che scegli come bersaglio. Il
proprietario può cavalcarla.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `tame [name]`, then target a creature | No | Sì | GameMaster | Game |

```text
.tame
.tame Aria
```

Solo in gioco. Scegli come bersaglio una creatura che si può cavalcare (vedi
[Cavalcature](../mounts.md#making-a-creature-rideable)). Senza nome ne diventi il proprietario e leggi
`a horse now belongs to Giachi.`; con il nome di un personaggio online, maiuscole e minuscole comprese, lo
diventa quel personaggio.

- Una creatura senza oggetto cavalcatura, o un giocatore: `an orc cannot be tamed.`
- Un nome che nessuno ha: `No character is named Nobody.`
- Un oggetto, o un mobile che non esiste più: `That is not an NPC.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Cavalcature](../mounts.md)
