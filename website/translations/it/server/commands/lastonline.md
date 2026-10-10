<!-- translation: {"sourceHash":"e3d952735b669430e6e17dcf5c1479be93798b60f4caf7e0a131e707c26b8ac8","title":"lastonline"} -->

# lastonline

Stampa quando un personaggio giocatore è stato online l'ultima volta, che sia nel mondo o no.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `lastonline <name>` | Sì | Sì | GameMaster | Game |

```text
.lastonline Aria
Aria was last online on 2026-10-09 18:05 (UTC).
```

Il nome si cerca senza distinguere maiuscole, e un nome di più parole si legge come uno solo. Un
personaggio nel mondo risponde `Aria is online now.` Più personaggi con lo stesso nome vengono elencati
tutti; uno in attesa di cancellazione resta fuori, e un nome che nessuno ha stampa `No character is named x.`

La data si scrive quando il personaggio esce dal mondo (una connessione chiusa, un cambio di personaggio
o l'arresto del server), in UTC. Un personaggio che non è uscito dal mondo da quando la data viene
registrata non ne ha ancora una: `Aria has no last online date yet: it has not left the world since the date began to be recorded.`
