<!-- translation: {"sourceHash":"d4c40902bf021c4490b17f7cd80ad50cb97b4484642e32cf66c3046d0fc5221c","title":"shutdown"} -->

# shutdown

Arresta il server in modo ordinato, subito o dopo un ritardo.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `shutdown [seconds]` | Sì | Sì | Administrator | Game |

```text
shutdown
shutdown 60
```

Gli amministratori in gioco usano `.shutdown` o `.shutdown 60`. Senza argomento o
con `0`, il server annuncia `The server is shutting down now.` (messaggio 30016)
e richiede un arresto ordinato. Un numero positivo annuncia
`The server will shut down in <seconds>
seconds.` (30017) e pianifica l'arresto.
Il comando ritorna senza attendere il conto alla rovescia; input console e gameplay
restano disponibili fino alla scadenza. Il ritardo parte dopo l'accodamento
dell'annuncio ed è arrotondato per eccesso alla risoluzione del timer del server.

Il server accetta una richiesta di arresto. Le successive riportano un errore
senza cambiare scadenza o ripetere annuncio. I secondi devono essere un numero
intero da `0` a `2147483647`; argomenti negativi, frazionari, in overflow o aggiuntivi
vengono rifiutati. Un arresto pianificato sopravvive alla disconnessione del giocatore chiamante.

Il comando arresta questo processo, compresi entrambi i ruoli in modalità Standalone.
Usa la stessa pulizia ordinata del percorso di arresto dell'host: i servizi si
arrestano, il salvataggio finale del mondo termina e la persistenza viene rilasciata.
Non termina forzatamente il processo. Le altre istanze non sono interessate.
Un arresto manuale dell'host durante il ritardo prevale e il timer viene scartato
con il game loop. Non ci sono sottocomandi di annullamento o riavvio.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`save`](save.md)
