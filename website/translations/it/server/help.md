<!-- translation: {"sourceHash":"705fd0c2cb0f05e4884148b6bc4a434ae2e48e32a35db9409869602ed99bdd51","title":"Aiuto"} -->

# Aiuto

Il pulsante Help del paperdoll apre un piccolo menu. Tre dei suoi pulsanti rispondono subito al giocatore;
il quarto manda una richiesta ai game master, che la smaltiscono da una [coda](#the-queue-of-the-staff).

```text
Help
[>] I am stuck
[>] Useful commands
[>] Server rules
[>] Call a game master
```

Un clic destro chiude il menu. Anche un fantasma può aprirlo, ed è quando serve di più. Il pacchetto è
lo 0x9B (vedi [Pacchetti](packets.md)); il menu è il gump `help_menu`
(`templates/gumps/help_menu.xml`, script `scripts/gumps/help_menu.lua`).

## Sono bloccato

Il pulsante porta alla città di partenza più vicina un personaggio che non riesce a uscire da una
trappola, da una casa o da una grotta.

1. Al personaggio viene detto `Stand still for 5 seconds and you will be taken to Britain.`
2. Se non si muove per tutta l'attesa, compare in quella città e gli viene detto `You have been taken to
   Britain.` Se si muove non succede nulla: `You moved: you stay where you are.`
3. La città è quella di [`data/starting_cities.toml`](data-files/starting-cities.md) più vicina al
   personaggio sulla mappa in cui si trova. Se su quella mappa non c'è nessuna città, è la prima del file.

Il pulsante rifiuta, con un testo, quando:

- il personaggio è in [prigione](jail.md);
- il personaggio sta combattendo;
- ha già chiesto e l'attesa non è finita: `You already asked to be moved: stand still.`;
- ha chiesto meno tempo fa della pausa: `You can ask to be moved again in 7 minutes.` La
  pausa è conservata con il personaggio, quindi un nuovo accesso non la azzera, e una richiesta che non
  ha spostato nessuno non la consuma.

Prigione e combattimento vengono controllati di nuovo alla fine dell'attesa. Un personaggio che esce
dal gioco durante l'attesa non viene spostato. Se la mappa della città non è caricata il personaggio resta
dove è, gli viene detto `There is no
city to take you to.` e non consuma la pausa. Ogni spostamento viene registrato nel log a livello
Information con il giocatore, dove era e la città. Game master e superiori non hanno pausa.

## Comandi utili

Esegue [`.help`](commands/help.md) come il giocatore: i comandi che il suo account può usare.

## Regole del server

Dice le regole del server in un messaggio di sistema, il messaggio 30200. Cambia il testo in
`data/messages/<language>/moongate.toml` (vedi [Localizzazione](localization.md)).

## Chiamare un game master

Il pulsante apre un secondo gump, `help_page_kind`, che chiede di cosa si tratta:

```text
What is it about?
[>] Question
[>] Bug
[>] Suggestion
[>] Harassment
```

1. Il giocatore sceglie un genere e gli viene detto `Type what you need in the journal line.`
2. Scrive una riga, fino a 128 caratteri; Esc o una riga vuota non inviano nulla: `Nothing was sent.`
3. La richiesta va nella coda e al giocatore viene detto `Your request was sent to the game masters.`
   Ogni game master e amministratore nel mondo legge `Gino asks for help (Bug): The door is stuck.`

Un giocatore ha una richiesta alla volta: finché ce n'è una aperta o in carico legge `You already asked for help: wait
for an answer.` e aspetta [`page_cooldown_seconds`](#settings) dopo aver chiesto prima
della successiva: `Wait 60 seconds before asking again.` Entrambi i casi vengono detti prima di scrivere la
riga, e di nuovo dopo se nel frattempo è cambiato qualcosa. Anche un giocatore in [prigione](jail.md) può
chiamare un game master.

## La coda dello staff

[`.pages`](commands/pages.md), per game master e superiori, apre la coda: le richieste aperte e in
carico, dalla più vecchia, dieci per pagina.

```text
Help requests
[>] #1 Gino, Bug, 3 min, open
[>] #2 Pina, Harassment, now, taken by Gino
```

Una riga apre la richiesta: chi l'ha fatta, di cosa tratta, dove, quanto tempo fa, e il testo del giocatore.

- **Go to the player** porta il game master dove il giocatore si trova ora, o dove ha chiesto se è offline.
- **Take** segna la richiesta come presa in carico da questo game master; un altro può rilevarla.
- **Send the answer** manda al giocatore la riga scritta nel campo, fino a 128 caratteri, e chiude la
  richiesta.
- **Close without an answer** la chiude senza nulla da consegnare.
- **Back** torna alla coda.

Una richiesta che un altro game master ha chiuso nel frattempo dice `Request 1 is already closed.` e
non cambia nulla. I game master e gli amministratori che entrano nel mondo sanno quante richieste
aspettano: `Help requests waiting: 2.
Type .pages.`

## La risposta

Il giocatore legge `Game master Gino answers: Go north.` subito se è nel mondo. Se è offline la risposta
aspetta, resta salvata anche se il server si riavvia, e gli viene detta una sola volta al login successivo.
Le richieste stanno nella tabella `world.help_pages` e le scrive il salvataggio del mondo. Una richiesta
chiusa viene conservata per [`page_history_days`](#settings) e poi l'avvio la cancella.

## Impostazioni

```toml
[ultima.help]
stuck_wait_seconds = 5        # The seconds a character must stand still before "I am stuck" moves it.
stuck_cooldown_minutes = 10   # The minutes before a player can use "I am stuck" again; 0 allows it at once.
page_cooldown_seconds = 60    # The seconds between two requests of one player to the game masters; 0 allows it at once.
page_history_days = 30        # The days a closed request is kept before it is deleted at startup.
```

`stuck_wait_seconds` va da 1 a 60, `stuck_cooldown_minutes` da 0 a 1440, `page_cooldown_seconds`
da 0 a 3600 e `page_history_days` da 1 a 3650. Un valore fuori intervallo ferma il server all'avvio e
l'errore indica l'impostazione.

## Per gli script

Il modulo Lua `help` dà a uno script ciò che usa il menu:

| Funzione | Cosa restituisce |
| --- | --- |
| `help.settings()` | `{ wait_seconds, cooldown_minutes }` |
| `help.nearest_city(player)` | `{ town, x, y, z, map }` della città di partenza più vicina, oppure nil |
| `help.can_page(player)` | `{ ok }`, oppure `{ ok = false, reason, seconds }` con `reason` `open`, `wait` o `gone` |
| `help.create_page(player, kind, text)` | `{ id }`, oppure `{ reason, seconds }` con `reason` `open`, `wait`, `text` o `gone`; `kind` è un `HelpPageKindType` |
| `help.pages()` | le richieste aperte e in carico, dalla più vecchia, ciascuna `{ id, player, name, account, kind, status, text, taken_by, answer, age_seconds, map, x, y, z, online }` |
| `help.page(id)` | una richiesta in quella forma, anche chiusa, oppure nil |
| `help.take(id, staff)`, `help.answer(id, staff, text)`, `help.close(id, staff)` | true quando la richiesta era aperta o in carico; il nome del game master è quello del mobile `staff` |
| `help.waiting()` | quante richieste sono aperte o in carico |

Gli enum `HelpPageKindType` (`Question`, `Bug`, `Suggestion`, `Harassment`) e `HelpPageStatusType`
(`Open`, `Taken`, `Closed`) sono pubblicati a Lua. Il modulo non controlla chi lo chiama: uno script per lo
staff controlla prima `world.is_staff`.

I testi che leggono i giocatori e lo staff sono i messaggi da 30192 a 30220, in tutte le lingue distribuite.
Le etichette e i messaggi dei gump dello staff sono in inglese, come gli altri gump dello staff.

## Non ancora

- Una conversazione tra giocatore e game master, o più di una risposta a una richiesta.
- Azioni contro il giocatore segnalato per molestie.
