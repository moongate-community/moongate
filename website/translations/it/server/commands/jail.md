<!-- translation: {"sourceHash":"f997e03f8faca4a47546965c1c1b550fe34c80a65458c6933492ef32b05a1192","title":"jail"} -->

# jail

Apre il gump della prigione: elenca le celle, ti porta in una di esse e imprigiona o libera un personaggio; jail <name> lo apre sul giocatore con quel nome, online oppure no.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `jail [name]` | No | Sì | GameMaster | Game |

```text
.jail
.jail Pippo
.jail Lord Pippo
```

Solo in gioco. Il nome è il nome completo di un personaggio giocante, scritto senza distinzione tra maiuscole e minuscole; un nome composto da
più parole non richiede virgolette.

## Cosa succede

Il gump della prigione si apre subito, con le celle e chi vi si trova. Da lì:

- `Target` mostra un cursore: scegli un giocatore o un NPC e il gump si riapre su di lui. Poi inserisci
  i giorni e, se vuoi, il motivo, e premi il pulsante di una cella libera: il personaggio viene
  trasferito lì.
- `Release`, su una cella occupata, pone fine alla pena senza multa.

Con un nome, il gump si apre sul giocatore che lo possiede, presente nel mondo oppure no:

- Un giocatore online diventa il bersaglio, come se lo avessi scelto con il cursore.
- Anche un giocatore offline diventa il bersaglio, mostrato come `Pippo (offline)`. Premi il pulsante di una cella libera
  e leggi `Pippo will be in cell 3 for 3 days from its next login.`: la cella viene riservata
  per lui e i giorni iniziano quando accede. Vedi
  [Imprigionare un giocatore offline](../jail.md#jail-a-player-who-is-offline).
- Più giocatori con quel nome vengono elencati con il loro account e lo stato online. Premi
  il pulsante di quello desiderato e il gump si apre su di lui, con le celle.

- `Go`, su qualsiasi cella, ti porta al suo interno sulla mappa della prigione. Usalo per visitare un detenuto:
  [`.go cell 1`](go.md) porta al luogo con quel nome sulla tua mappa, che è una stanza vuota
  su tutte le mappe tranne quella della prigione. Le celle non hanno porta: esci con `.go` o con un altro `Go`.

[Prigione](../jail.md) descrive tutto: la pena, la multa, la nota di scarcerazione e la cassa
 delle razioni.

| Cosa vedi | Perché |
| --- | --- |
| `No character is named Pippo.` | Nessun personaggio giocante ha quel nome. Gli NPC e i personaggi in attesa di eliminazione non vengono cercati. |
| `That is not a character.` | Con il cursore di `Target` hai scelto un oggetto o il terreno, oppure il personaggio se n'è andato nel frattempo. Il gump conserva il personaggio precedente. |
| `That cell cannot be reached.` | `Go` non è riuscito a portarti lì: la mappa della prigione non è caricata. |
| `The jail is not set up: data/jail.toml is missing.` | Manca [`jail.toml`](../data-files/jail.md). |
| `The jail gump is missing: templates/gumps/jail_sentence.xml.` | Il file del gump è stato rimosso. |

Nel gump, `You cannot jail yourself or the staff of your rank.` risponde a un bersaglio che sei tu
oppure un giocatore di grado uguale o superiore al tuo, e `Type the days as a whole number from 1 to 30.` a un numero di giorni
non accettato dalla prigione.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Prigione](../jail.md)
- [`jail.toml`](../data-files/jail.md)
