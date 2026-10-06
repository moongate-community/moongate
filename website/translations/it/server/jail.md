<!-- translation: {"sourceHash":"791fc403e00c21765ec73c3c0adef30fd1ec06a9517419fdf6a7124ae3a3bc99","title":"Prigione"} -->

# Prigione

Un game master manda un personaggio in una cella per un certo numero di giorni. Quando i giorni terminano, il
detenuto torna dove è stato arrestato, paga una multa in oro e trova nello zaino una nota
che indica la pena scontata e ciò che ha pagato. Ogni cella ha una cassa di pane e acqua. Giocatori e
NPC vengono imprigionati allo stesso modo.

La cella scelta da un gump, la pena in giorni, la multa, la nota e le razioni sono
propri di Moongate: ModernUO sceglie una cella casuale per un tempo deciso autonomamente, UOX3 prende un
numero di secondi da un comando e nessuno dei due applica una multa o nutre i detenuti.

## Mandare qualcuno in prigione

[`.jail`](commands/jail.md), per game master e gradi superiori, apre il gump della prigione:

```text
Jail
Days: [ 3 ]   Reason: [ Stole a horse          ]

[>] Target: Lord Pippo
Jail                              Release  Go
[>] Cell 1   free                          [>]
    Cell 2   Gino - 2d 4h left    [x]      [>]
[>] Cell 3   free                          [>]
    ...
```

1. Premi `Target` e scegli il personaggio con il cursore: un giocatore o un NPC. Il gump si apre
   di nuovo con il suo nome. Fino ad allora le celle vengono solo elencate: nessuna ha un pulsante per imprigionare.
2. Digita i giorni della pena e, se vuoi, il motivo: fino a 60 caratteri nel gump.
   Scegliere un personaggio già in prigione riempie il campo con il suo motivo, quindi spostarlo in un'altra
   cella lo conserva.
3. Premi il pulsante di una cella libera.

Il personaggio si trova subito in quella cella e riceve `You have been jailed for 3 days: Stole a
horse`, oppure `You have been jailed for 3 days.` senza motivo; tu ricevi `Lord Pippo is in cell 3
for 3 days.` Il motivo viene conservato con la pena come una riga di testo semplice, scritta nella
[console](#in-the-console-and-the-log) e sulla [nota di scarcerazione](#the-release-note): gli a capo
diventano spazi, `<` e `>` vengono rimossi e ciò che supera i 100 caratteri viene tagliato. Il campo del gump
ne accetta 60; i 100 sono il limite per uno script che chiama `jail.send`.

- Una cella contiene un detenuto. Una cella occupata mostra il suo nome e il tempo rimasto e non ha
  un pulsante per imprigionare.
- I giorni sono un numero intero da 1 a [`ultima.jail.max_days`](#settings). Qualsiasi altro valore
  non imprigiona nessuno e riapre il gump.
- Non puoi imprigionare te stesso né un giocatore il cui account ha il tuo grado o uno superiore.
- Seleziona un personaggio già in prigione e il gump mostra la sua scarcerazione in alto; il pulsante
  di una cella libera lo sposta lì con una pena che inizia ora. Torna comunque dove
  era stato arrestato la prima volta.
- Un altro `Target` sceglie qualcun altro; un cursore chiuso con Escape conserva il personaggio che il gump
  aveva. Un cursore sostituito da un altro, come quello di un secondo `.jail`, non apre nulla.
- Un clic destro chiude il gump.

## Imprigionare un giocatore offline

`.jail Pippo` cerca il giocatore con quel nome e apre il gump su di lui, che sia nel
mondo oppure no. Il nome viene digitato per intero, senza distinzione tra maiuscole e minuscole.

```text
[>] Target: Pippo (offline)
Jail                              Release  Go
[>] Cell 1   free                          [>]
    Cell 2   Gino - waits for login  [x]   [>]
```

Digita i giorni e il motivo e premi il pulsante di una cella libera, come per chiunque altro. Un giocatore
online si trova subito nella cella. Uno offline non viene spostato e ricevi
`Pippo will be in cell 3 for 3 days from its next login.`:

- La cella viene riservata per lui: mostra `Pippo - waits for login` e nessun altro può esservi mandato.
- Entro dieci secondi dal suo prossimo accesso il giocatore viene portato nella cella e informato della pena.
  **I suoi giorni iniziano allora**, non nel giorno in cui hai assegnato la pena: tre giorni sono tre giorni nella
  cella.
- Quando i giorni terminano torna dove ha effettuato l'accesso, con la multa e la nota come ogni
  altro detenuto.
- `Release` su una pena in attesa la elimina: nessuno è stato spostato, quindi non ci sono multa né nota.
- Una pena in attesa non scade mai. Se il giocatore non torna, la cella si libera di nuovo
  solo con `Release`.
- Premere un'altra cella libera per un giocatore con pena in attesa sposta la cella a lui riservata.
- Il grado conta anche offline: non puoi imprigionare un personaggio il cui account ha il tuo grado o uno
  superiore.

Quando più giocatori hanno lo stesso nome, il gump li elenca al posto delle celle:

```text
[>] Target: nobody. Pick one of these, or press the button.
[>] Pippo - account mario - online
[>] Pippo - account luigi - offline
```

Premi il pulsante di quello desiderato: il gump si apre su di lui, con le celle. Ne vengono elencati al
massimo dieci; con più di dieci giocatori dello stesso nome il gump indica quanti ne ha esclusi, e questi possono
essere scelti solo con `Target` mentre sono online.

Un giocatore imprigionato mentre era online e poi disconnesso può essere spostato allo stesso modo: `.jail
<name>`, poi una cella libera. La sua pena torna in attesa e ricomincia al prossimo accesso, e
 torna comunque dove è stato arrestato la prima volta.

Un giocatore intercettato nei pochi secondi del suo accesso viene trattato come offline: la pena
attende e il controllo successivo lo porta nella cella.

Gli NPC non vengono trovati per nome: imprigionali con `Target`.

## Visitare le celle

`Go`, su ogni cella del gump, ti porta al suo interno, sulla mappa della prigione, e lascia il gump
aperto; non richiede un bersaglio. È il modo di vedere un detenuto: i luoghi da `Cell 1` a `Cell 10` di
[`locations.toml`](data-files/locations.md) esistono su Felucca e su Trammel, e
[`.go cell 1`](commands/go.md) prende quello della mappa su cui ti trovi, una stanza vuota ovunque tranne
sulla mappa della prigione. Le celle sono stanze chiuse: esci con `.go` o con un altro `Go`.

## La pena

I giorni sono giorni reali. Trascorrono mentre il detenuto è offline e mentre il server è fermo:
una pena di 3 giorni assegnata lunedì a mezzogiorno finisce giovedì a mezzogiorno.

Il server cerca ogni dieci secondi le pene terminate.

- Un detenuto nel mondo viene liberato subito.
- Un giocatore offline viene liberato entro dieci secondi dal prossimo accesso. La sua cella è libera
  dal momento in cui la pena termina.
- Una pena assegnata a un giocatore offline non è iniziata:
  [attende il suo accesso](#jail-a-player-who-is-offline) e non termina mai prima.
- La pena di un NPC rimosso nel frattempo viene eliminata.
- Un detenuto con un oggetto sul cursore attende nella cella finché non lo rilascia: l'oro sul
  cursore non può essere preso e sollevarlo non permette di evitare la multa.

Le pene vengono conservate nella tabella `world.jail_sentences` e scritte dal salvataggio del mondo, quindi un
riavvio non ne dimentica nessuna.

In prigione non è ancora vietato nulla: incantesimi, abilità e viaggi non esistono. Le celle sono stanze
chiuse senza porta e la regione è [poco illuminata](server-configuration.md).

## La scarcerazione

Quando una pena termina:

1. Viene prelevata la multa: fino a [`ultima.jail.fine_gold`](#settings) monete, dalle pile d'oro nello
   zaino e nelle borse al suo interno, poi dalla cassa bancaria. Quando il detenuto ha meno, viene preso ciò
   che ha e lascia comunque la prigione. Un NPC senza oro non paga nulla.
2. Il detenuto torna dove è stato arrestato. Quando quella mappa non è più caricata va al
   punto `release` di [`jail.toml`](data-files/jail.md).
3. Una nota di scarcerazione viene inserita nello zaino.
4. Un giocatore riceve `You have served your sentence. A fine of 500 gold was taken.`

Una pena viene conclusa prima che la scarcerazione venga eseguita, quindi nessuno paga due volte. Quando una scarcerazione fallisce a metà,
come un teletrasporto su un punto non più esistente, il log mostra `The release of <serial> from jail
failed` e il detenuto può rimanere nella cella senza pena: spostalo manualmente con
[`.go`](commands/go.md) o un teletrasporto. Anche una nota che non è stato possibile creare compare nel log.

`Release` nel gump conclude una pena in anticipo: nessuna multa, nessuna nota. Un detenuto nel mondo torna indietro
subito, un giocatore offline al prossimo accesso.

### La nota di scarcerazione

La nota è l'oggetto `jail_release_note`. Un doppio clic mostra ciò che la prigione vi ha scritto:

```text
Lord Pippo served 3 days in cell 2, from 2026-10-04 to 2026-10-07, and paid a fine of 500 gold.
Jailed by Giachi. Reason: Stole a horse
```

Il motivo compare quando è stato fornito (messaggio 30149).

La multa sulla nota è l'oro realmente prelevato. Il testo viene scritto nella lingua del server
da [`templates/books/jail_release_note.toml`](data-files/books.md), con le date in UTC.
Il nome del destinatario è quello registrato con la pena, anche dopo una rinomina. I valori generati di
`book.title`, `book.author` e `book.content` rimangono fissi dopo uno scambio o una lettura. Le vecchie note `jail.text`
rimangono leggibili. La nota
conserva anche le proprietà `jail.cell`, `jail.days` e `jail.fine` per gli script; il suo script è
[`jail_note.lua`](scripting/shipped-scripts.md#jail_notelua).

## La cassa delle razioni

Ogni cella ha una cassa che non può essere spostata, con 2–4 pagnotte e una brocca d'acqua.
Un detenuto vi mangia e beve, così una lunga pena non lo lascia
[affamato e assetato](server-configuration.md).

| File | Contenuto |
| --- | --- |
| `templates/items/jail.toml` | La cassa `jail_chest` e la nota `jail_release_note` |
| `templates/loots/jail.toml` | Le tabelle di bottino `jail_bread` e `jail_water`, una voce ciascuna, così una cassa contiene sempre entrambi |
| `templates/spawns/felucca/jail.toml` | Una [regione di oggetti](spawns.md#regions-of-items-treasure-chests) per cella: una singola casella nell'angolo nord-est della cella |

La cassa decade dopo 60 minuti, consumata oppure no, e la sua regione ne rimette una piena uno o
due minuti dopo. Per nutrire i detenuti con qualcos'altro, modifica le due tabelle del bottino. Un mondo esistente
riceve le casse al successivo controllo di spawn; [`.initial_spawn`](commands/initial_spawn.md) le riempie
subito.

## Nella console e nel log

Il server indica chi entra e chi esce, al livello informativo:

```text
Lord Pippo (0x00000A12) is jailed in cell 3 for 3 days by Giachi: Stole a horse
Aria (0x00000A40) will be jailed in cell 4 for 2 days at its next login, by Giachi: Insulted the staff
The sentence of Aria (0x00000A40) for cell 4 is dropped before it began
Lord Pippo (0x00000A12) is released from cell 3 after 3 days, with a fine of 500 gold
Gino (0x00000B07) is released early from cell 2
The sentence of an orc (0x0000E258) in cell 1 is dropped: it is no longer in the world
```

Un personaggio spostato in un'altra cella viene indicato come nuovamente imprigionato nella nuova cella. La multa è
l'oro realmente prelevato. L'ultima riga è un NPC rimosso mentre scontava la pena. La seconda riga è
una pena assegnata a un giocatore offline; al suo accesso il server scrive la consueta riga `is
jailed in cell` e la terza riga è quella pena revocata prima del ritorno del giocatore.
Una pena in attesa di una cella rimossa da `jail.toml`, o la cui mappa non è caricata, scrive
un avviso a ogni controllo dopo l'accesso, `waits for cell 4, which cannot be reached`, e continua ad
attendere.

## Impostazioni

```toml
[ultima.jail]
fine_gold = 500   # Coins taken when a sentence ends; 0 takes nothing.
max_days = 30     # The longest sentence the gump accepts.
```

`fine_gold` va da 0 a 1.000.000.000 e `max_days` da 1 a 3650. L'oro è il template di
oggetto di `ultima.items.gold_template`.

## Le celle

Le celle sono in [`data/jail.toml`](data-files/jail.md): le dieci della prigione di Felucca, le stesse
usate da ModernUO e UOX3. Per aggiungere una cella, aggiungi lì il suo `[[cell]]` e la regione della sua cassa in
`templates/spawns/felucca/jail.toml`. Senza il file la prigione è disattivata e `.jail` lo segnala.

## Per gli script

Il [modulo `jail`](https://moongate.sh/lua/jail/) è ciò che usa il gump:

```lua
if jail.send(target, 2, 3, who, "Stole a horse") == JailResultType.Ok then
    mobile.message(who, "Done.")
end

for _, cell in ipairs(jail.cells()) do
    if cell.prisoner then
        log.info(cell.name .. " leaves cell " .. cell.number .. " in " .. cell.seconds_left .. " seconds")
    end
end
```

`jail.release(serial)` conclude una pena in anticipo e `jail.sentence(serial)` ne legge una. Una pena
che attende l'accesso del giocatore ha `pending` impostato in `jail.sentence` e nella cella a lui
riservata, e `seconds_left` è allora la sua intera durata. `jail.send` risponde `JailResultType.Pending` per un
giocatore offline, e solo per uno trovato da `.jail <name>`: uno script non può imprigionare qualsiasi seriale
che non si trova nel mondo. Il modulo
non controlla chi lo chiama: uno script per lo staff verifica prima `world.is_staff`, come fa lo script del gump
[`jail_sentence.lua`](scripting/shipped-scripts.md#jail_sentencelua). Il gump è
[`templates/gumps/jail_sentence.xml`](gumps.md); entrambi possono essere modificati da te.

## Cosa non fa ancora

- Vietare qualcosa in prigione: non ci sono incantesimi, abilità o recall da vietare.
- Conservare un registro delle pene passate: il motivo vive con la pena e sulla sua nota.
- Imprigionare un intero account: una pena riguarda un solo personaggio.

## Vedi anche

- [`jail`](commands/jail.md): il comando.
- [`jail.toml`](data-files/jail.md): le celle.
- [Gump](gumps.md) e [spawn degli NPC](spawns.md).

Durante l'aggiornamento di una directory radice esistente, `mgctl init` conserva i file degli oggetti e degli script della prigione. Completa le [due integrazioni di file richieste](data-files/books.md#existing-roots) prima di avviare il server aggiornato.
