<!-- translation: {"sourceHash":"fcd7af9c04adb0ef57d385a50128e5d27337ee52c50a4e9a729a0d996d38fd80","title":"broadcast"} -->

# broadcast

Invia un messaggio di sistema a ogni giocatore in questo mondo.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `broadcast <text>` | Sì | Sì | Administrator | Game |

```text
broadcast Server maintenance in five minutes.
```

In gioco, gli amministratori usano `.broadcast Server maintenance in five minutes.`.
Il testo viene consegnato come messaggio chat di sistema Unicode ai personaggi
connessi attualmente nel mondo di questa istanza, indipendentemente da mappa o
distanza. Sessioni di selezione del personaggio e client disconnessi sono esclusi.
Le altre istanze server non ricevono il messaggio.

Il testo dopo il nome del comando viene inviato senza richiedere virgolette.
Gli spazi iniziali e finali vengono rimossi; quelli interni conservati. Un input
vuoto stampa l'utilizzo e non invia nulla. Il chiamante riceve il numero di giocatori
le cui code in uscita hanno accettato il messaggio; non è una conferma di ricezione
del client. L'input in gioco mantiene il limite esistente di 128 caratteri del
parlato, compresi punto e comando. I messaggi console devono rientrare sia nel
pacchetto Unicode del parlato sia nel limite del trasporto compresso. I messaggi
troppo grandi vengono rifiutati prima che qualsiasi giocatore li riceva.

## Vedi anche

- [Tutti i comandi](../commands.md)
