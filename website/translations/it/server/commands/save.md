<!-- translation: {"sourceHash":"d1b236575bb557f33760e92b2627041b63be1656b57ab5c284375bff96d820ae","title":"save"} -->

# save

Salva il mondo e avvisa tutti i giocatori quando ha finito.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `save` | Sì | Sì | Administrator | Game |

```text
save
```

In gioco, gli amministratori usano `.save`. Il comando richiede un salvataggio tramite il coordinatore
esistente dei salvataggi del mondo e attende il completamento della persistenza durevole. Una richiesta effettuata
durante un altro salvataggio si unisce a quel salvataggio invece di avviare un'operazione concorrente.
Ogni comando riuscito trasmette `The world has been saved in <seconds> seconds.`
(messaggio 30015, nella lingua del server) ai personaggi connessi attualmente presenti nel
mondo su questa istanza, su tutte le mappe. Anche la console stampa lo stesso messaggio di completamento;
chi esegue il comando in gioco lo riceve tramite la trasmissione. Il tempo trascorso
misura l'attesa del salvataggio, escludendo la consegna della trasmissione, in secondi con due
decimali (per esempio, `The world has been saved in 1.23 seconds.`).

Un salvataggio fallito produce un errore per chi esegue il comando e nessuna trasmissione di successo. Gli argomenti
aggiuntivi stampano l'uso senza salvare. I salvataggi automatici e durante l'arresto mantengono il
comportamento esistente; questo annuncio appartiene al comando `save`.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`shutdown`](shutdown.md)
