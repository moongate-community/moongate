<!-- translation: {"sourceHash":"f36d3551ed42d949672e3f7743008b81820ee5a20cb0f2a64e3b9ad8b328920c","title":"Template di testi leggibili"} -->

# Template di testi leggibili

Inserisci un documento di testo semplice in `<root>/templates/books/<name>.toml`. Il file
`welcome_letter.toml` ha id `welcome_letter`; le sottodirectory sono consentite ma
i nomi dei file senza estensione devono essere univoci. Su questa macchina la directory radice del server è `~/moongate`.

Il server carica queste sorgenti all'avvio. Un GameMaster può crearne una con
[`.book <template> [name=value ...]`](../commands/book.md). Uno script crea una
pergamena o un libro personalizzato con `book.give`, oppure scrive su un oggetto leggibile esistente con `book.write`.
Titolo, autore e corpo vengono risolti una volta sola e salvati su quel singolo oggetto.
Scambiarlo, leggerlo come altro giocatore, rinominare il destinatario, modificare la
sorgente o riavviare il server non cambia mai il suo testo salvato. Un libro scrivibile può essere
modificato da chi lo trasporta, come descritto sotto.

## Una lettera di benvenuto

L'esempio distribuito è `templates/books/welcome_letter.toml`:

```toml
title = "Welcome $player_name"
author = "Lord British"
content = """
Dear $player_name,

Welcome to $server_name.
Bring this letter to $contact_name.
"""
variables = ["contact_name"]

[translations.ita]
title = "Benvenuto $player_name"
content = """
Caro $player_name,

Benvenuto a $server_name.
Porta questa lettera a $contact_name.
"""
```

Creala da uno script Lua che dispone del seriale del giocatore:

```lua
local letter = book.give(player, "welcome_letter", { contact_name = "Vega" })
if letter then
    book.open(letter, player)
end
```

Il template di oggetto `readable_scroll` usa la grafica `0x14ED`, non si impila ed
esegue `scripts/items/readable_scroll.lua`. Un doppio clic apre un gump di pergamena
con un corpo scorrevole. Il testo memorizzato è semplice; i caratteri HTML vengono sottoposti a escape solo
alla visualizzazione e gli a capo vengono conservati.

## Libri e pergamene

L'oggetto decide come si apre un documento:

| Template di oggetto | `script_id` | Un doppio clic apre |
| --- | --- | --- |
| `readable_scroll` (0x14ED) | `readable_scroll` | Il gump di pergamena, con un corpo scorrevole |
| `readable_book` (0x0FF1) | `readable_book` | Il libro nativo del client: una copertina con titolo e autore e pagine da sfogliare |

Le sue pagine derivano dal testo salvato quando viene aperto:

- Una riga vuota nel testo è un'interruzione di pagina. Per una riga vuota dentro una pagina scrivi una riga con un
  solo spazio.
- Gli a capo alla fine del testo non aggiungono righe né pagine.
- Una pagina contiene 8 righe. Una più lunga, come spesso accade a una pagina tradotta, continua nella pagina successiva:
  non viene tagliato nulla.
- Una riga più lunga di 79 unità UTF-16 va a capo all'ultimo spazio adatto entro quel limite.
  Se non esiste uno spazio del genere, viene divisa a 79 unità, anche dentro una parola.
- Un libro contiene 255 pagine; un testo che ne richiede di più non si apre e il log lo segnala.
- Sulla copertina il titolo viene tagliato a 60 byte e l'autore a 30, come consentono i campi del client.

Imposta `item_id` nella sorgente per dare all'oggetto un'altra grafica, come la copertina di un libro:
`0x0FEF` marrone, `0x0FF0` beige, `0x0FF1` rosso, `0x0FF2` blu.

```toml
title = "A Grammar of Orcish"
author = "Yorick of Yew"
item_template = "readable_book"
item_id = 0x0FEF
content = """..."""
```

Un libro non può contenere [allegati](#letter-attachments): non ha un pulsante con cui ritirarli, quindi
una sorgente del genere viene rifiutata all'avvio. Usa una pergamena per una lettera con un regalo.

### Libri su cui un giocatore scrive

Un libro è di sola lettura a meno che la sua sorgente non specifichi altrimenti:

```toml
title = "a book"
author = "$player_name"
item_template = "readable_book"
writable = true
pages = 20
```

- `writable = true` rende il libro scrivibile dal personaggio che lo trasporta, nel proprio zaino
  o nella propria cassa bancaria aperta: titolo, autore e pagine, nel libro nativo del client. Chiunque altro possa leggerlo lo apre in sola lettura,
  così come tutti mentre si trova a terra. Passalo a qualcuno e il nuovo portatore può scriverci.
- `pages` riserva un minimo da 1 a 255 pagine; 20 se non impostato. Il contenuto salvato può richiedere più
  pagine di questo minimo, fino al limite di 255 pagine. Una sorgente scrivibile può omettere `content` o
  lasciarlo vuoto; il testo fornito può essere sovrascritto.
- Valgono i limiti del client: un titolo di 60 byte, un autore di 30, 8 righe per pagina, una riga inferiore a 80
  unità UTF-16. Ciò che non rientra non viene salvato, né viene salvato il testo che supera le
  16.384 unità UTF-16 di un documento.
- Se non rimane un titolo, l'oggetto viene chiamato come il suo template, `a book`. In un titolo o in un autore
  `<`, `>` e `#` diventano `(`, `)` e `-`, come in ModernUO: il nome compare in un tooltip.
- Ciò che viene scritto è salvato sull'oggetto, come il testo di ogni documento, e rimane dopo un riavvio.
- `writable` e `pages` sono solo per un oggetto libro e non per una traduzione.

Il `blank_book` distribuito è questa sorgente. **Una directory radice creata prima dell'introduzione dei libri deve aggiungere
l'oggetto `readable_book` al proprio `templates/items/books.toml` prima di eseguire `mgctl init`** (i passaggi sono
in [Libri e pergamene](#books-and-parchments)): `mgctl init` copia `blank_book.toml` nella
directory radice, e una sorgente libro senza il suo oggetto interrompe l'avvio. Ogni nuovo personaggio ne riceve uno da
[`data/starting_items.toml`](starting-items.md), con il proprio nome come autore; `.book
blank_book` ne dà uno a un personaggio già esistente. Una directory radice che mantiene un proprio
`starting_items.toml` aggiunge la voce manualmente all'insieme comune:

```toml
[[set.items]]
items = ["readable_book"]
equip = false
book_template = "blank_book"
```

Copiare, firmare e sigillare un libro non sono funzionalità realizzate.

Una directory radice creata prima dell'introduzione dei libri mantiene i propri file, che `mgctl init` non sovrascrive, e
i suoi testi importati rimangono pergamene finché non esegui entrambe queste operazioni:

1. Aggiungi l'oggetto `readable_book` al tuo `templates/items/books.toml`, come nel file distribuito:
   `id = "readable_book"`, `item_id = 0x0FF1`, `name = "a book"`, `script_id = "readable_book"`,
   `stackable = false`, `weight = 1`. `mgctl init` aggiunge autonomamente `scripts/items/readable_book.lua`.
2. Sostituisci i file di `templates/books/modernuo` con quelli distribuiti, oppure esegui
   [`moongate-convert modernuo-books`](../book-content-import.md) su di essi.

Esegui la prima operazione prima della seconda: un catalogo che nomina `readable_book` senza l'oggetto interrompe
l'avvio. Le pergamene già consegnate rimangono pergamene; le nuove copie sono libri.

## Consegna alla creazione del personaggio

L'insieme comune degli [oggetti iniziali](starting-items.md#personalized-starting-letters)
distribuito consegna `welcome_letter` nello zaino di ogni nuovo personaggio con
`contact_name = "Vega"`. Imposta `book_template` e `book_values` in una voce di oggetto iniziale
per consegnare qualsiasi documento del catalogo. I valori e il nome del nuovo personaggio vengono
risolti una volta sola e il testo viene salvato con il personaggio e gli oggetti iniziali in
un'unica transazione. Le directory radice esistenti devono aggiungere la voce al proprio
`data/starting_items.toml` conservato.

## Campi

| Campo | Significato |
| --- | --- |
| `title` | Titolo sorgente obbligatorio e non vuoto; supporta variabili |
| `author` | Opzionale, vuoto per impostazione predefinita; supporta variabili |
| `content` | Corpo sorgente multilinea; obbligatorio e non vuoto salvo con `writable = true`, quando può essere vuoto oppure omesso; supporta variabili |
| `variables` | Array opzionale dei nomi dei valori personalizzati obbligatori |
| `item_template` | Template di oggetto esistente; predefinito `readable_scroll`; `readable_book` per [un libro](#books-and-parchments) |
| `item_id` | Grafica opzionale dell'oggetto creato, da 1 a 0xFFFF; se assente, l'oggetto conserva quella del template. Non consentito in una traduzione |
| `writable`, `pages` | Opzionali; un libro [su cui un giocatore scrive](#books-a-player-writes-in) e il suo numero minimo di pagine riservate |
| `attachments` | Voci opzionali di ricompensa; vedi [Allegati delle lettere](#letter-attachments) |
| `translations.<language>` | Sostituzioni opzionali di `title`, `author` e `content`; ogni campo mancante usa il valore di primo livello |

Gli id e i nomi delle variabili usano lettere minuscole, cifre e trattini bassi, iniziando
con una lettera. Le dichiarazioni personalizzate non possono ripetersi né nascondere quelle predefinite. Il
template di oggetto selezionato deve impostare esplicitamente `stackable = false` e usare
`script_id = "readable_scroll"`, `"readable_book"` o `"jail_note"`; i valori ereditati valgono.

La lingua di creazione è `[localization].language`. Le sostituzioni supportate sono
`eng`, `ita`, `fre`, `ger`, `spa`, `por`, `pol` e `cze`. Una lingua assente
usa i campi di primo livello.

## Testi dei libri importati

Il catalogo distribuito `templates/books/modernuo` contiene 62 libri statici importati da
ModernUO. Per esempio, `book.give(player, "grammar_of_orcish")` crea una copia leggibile
nella lingua di creazione configurata. Ogni libro importato conserva la sorgente inglese
e ha traduzioni di titolo/corpo in italiano, francese, tedesco, spagnolo, portoghese, polacco
e ceco; i nomi degli autori rimangono invariati. La reimportazione conserva i campi di traduzione esistenti.
[Importare i testi dei libri](../book-content-import.md)
documenta il convertitore, il confronto delle sorgenti e le esecuzioni ripetute. Queste voci sono
[libri](#books-and-parchments), ciascuno con la copertina assegnata da ModernUO.

## Allegati delle lettere

Aggiungi allegati a una sorgente di documento, fuori da qualsiasi tabella di traduzione:

```toml
[[attachments]]
item_template = "0x0eed_gold_coin"
amount = 100

[[attachments]]
item_template = "0x103b_bread_loaf"
amount = 3
newbie = false
```

Ogni voce richiede un `item_template` esistente. `amount` vale 1 per impostazione predefinita e usa
la sintassi condivisa di interi/dadi; il suo intero intervallo deve rimanere entro 1..65535.
`hue` è opzionale e usa la sintassi condivisa di valore fisso/intervallo/lista; lasciarlo non impostato
conserva la tinta della factory dell'oggetto. `newbie` è false per impostazione predefinita (bottino ordinario); true
contrassegna l'oggetto consegnato come conservato alla morte. Il gruppo può contenere al massimo 32
oggetti fisici, contando ogni possibile copia non impilabile al tiro massimo.
Le traduzioni modificano solo il testo e non possono aggiungere o sostituire allegati.

`book.give` e gli oggetti iniziali fissano un gruppo indipendente per ogni lettera
fisica alla creazione, comprese quantità estratte, tinta e proprietà della factory.
La lettera conserva quel gruppo attraverso modifiche alla sorgente, scambi e riavvii. Gli oggetti di
ricompensa entrano nel mondo e contribuiscono al peso solo quando vengono ritirati; fino ad allora pesa solo
la lettera. Vengono consegnati come pile/oggetti distinti senza unirsi alle
pile esistenti.

Un lettore vede **Ritira allegati** (**Claim attachments** in inglese) solo mentre
una lettera con allegati non ritirati si trova nel proprio zaino, comprese le borse annidate. Scambiare
quella lettera trasferisce le sue ricompense non ritirate. Le lettere a terra, in banca,
sul cursore e nell'inventario di un altro giocatore non hanno
l'azione di ritiro. La lettura rimane disponibile secondo le normali regole di accesso.

Il ritiro consegna l'intero gruppo una sola volta, oppure nulla se allo zaino manca capacità di
peso, capacità di oggetti o slot liberi nella griglia. Libera spazio e riapri la lettera per
riprovare. In caso di successo il pulsante scompare e la stessa lettera rimane leggibile.
Ricompense salvate non valide o un template di oggetto non disponibile/incompatibile rifiutano il
ritiro; modificare il TOML non ricrea il gruppo già emesso.

`book.write` conserva un contenuto di allegati esistente e valido, anche dopo
il ritiro. Non può aggiungere ricompense a una lettera semplice né riparare ricompense malformate.
Una riscrittura rifiutata lascia invariato il testo. Un errore di preparazione fa sì che
`book.give` in esecuzione restituisca nil prima di allocare la lettera e annulla la creazione del
personaggio quando la lettera è un oggetto iniziale.

La lettera di benvenuto distribuita non ha ricompense. Per attivarle, aggiungi le voci sopra al
tuo `templates/books/welcome_letter.toml` e riavvia. Se la tua directory radice
ha già un file modificato degli oggetti iniziali, associa quella sorgente usando la
[voce della lettera iniziale personalizzata](starting-items.md#personalized-starting-letters).
Solo le lettere appena emesse ricevono il gruppo configurato. Le cassette postali non sono realizzate. Copertine,
pagine e modifica native dei libri sono disponibili come descritto in [Libri e pergamene](#books-and-parchments).

## Variabili

| Predefinita | Valore alla creazione |
| --- | --- |
| `player_name` | Nome del destinatario specificato |
| `server_name` | Nome del server, come nel MOTD |
| `realm_name` | Nome del realm attuale |
| `version`, `codename` | Versione e nome in codice del server in esecuzione |
| `users_online` | Sessioni connesse che hanno un personaggio |

Funzionano sia `$player_name` sia `${player_name}`. Le parentesi graffe separano un nome da un
suffisso: `${player_name}_letter`; `$player_name_letter` indica una variabile
diversa. `$$` stampa un singolo dollaro letterale; `$${player_name}` stampa il valore letterale
`${player_name}`.

Un valore fornito è una stringa, un numero finito formattato senza separatori locali
oppure un booleano (`true` / `false`). Le stringhe vuote sono consentite. Ogni dichiarazione deve
essere fornita; chiavi mancanti o aggiuntive, tabelle, funzioni, nil o numeri non finiti
causano un errore. I valori inseriti sono letterali: un valore contenente `$server_name` non viene mai
espanso di nuovo.

Il formatter condiviso conserva la grammatica del [MOTD](../motd.md) con sole parentesi graffe e i suoi
resolver asincroni dei plugin. I documenti usano valori personalizzati espliciti.

## Operazioni Lua e campi salvati

| Chiamata | Risultato |
| --- | --- |
| `book.give(player, template_id, values?)` | Seriale del nuovo oggetto, oppure nil |
| `book.write(item, template_id, player, values?)` | True in caso di successo; false lascia invariati i campi precedenti |
| `book.open(item, player)` | True quando aperto o accodato per il turno successivo del loop; false in caso di rifiuto |

La creazione genera tutti i campi prima di prendere un seriale o dare un oggetto. Può
fallire per un template o destinatario sconosciuto, valori non validi, uno zaino mancante
o un pool di seriali esaurito. La scrittura rifiuta anche oggetti tenuti, pile e template
di oggetti leggibili non supportati.

La lettura usa l'accesso normale: oggetti trasportati dal lettore, compresa una banca aperta,
oppure un oggetto/contenitore vicino e raggiungibile a terra sulla stessa mappa. Lo zaino di un altro
giocatore, una banca chiusa, un oggetto/contenitore tenuto o una radice a terra distante vengono rifiutati.
Quando chiamata da Lua, l'apertura di un gump di pergamena viene accodata; accesso, esistenza dell'oggetto e
sessione connessa originale vengono controllati di nuovo prima dell'invio. I libri nativi inviano copertina
e pagine immediatamente, anche da Lua.

Le proprietà del testo salvato sono `book.template`, `book.title`, `book.author` e `book.content`.
I libri scrivibili salvano anche `book.writable = true` e `book.pages`, il numero minimo di pagine
riservate. Applicare una sorgente di sola lettura rimuove queste due proprietà di scrittura. Il nome visualizzato dell'oggetto
segue il titolo generato.

## Validazione e aggiornamenti

Una directory `templates/books` mancante significa un catalogo vuoto. TOML non valido,
id duplicati, token sconosciuti/non validi, dichiarazioni duplicate, traduzioni non supportate
e template di oggetto inadatti interrompono l'avvio indicando il percorso sorgente.

Titolo e autore generati sono limitati ciascuno a 128 unità UTF-16. Il contenuto sorgente e
quello generato sono limitati a 16.384 unità. NUL e caratteri di controllo diversi da
a capo e tabulazioni vengono rifiutati. L'escape HTML può rendere un corpo valido troppo
grande per un pacchetto client; la lettura allora fallisce e registra il motivo senza
troncare il testo.

Riavvia dopo aver modificato una sorgente. Solo i documenti appena creati o esplicitamente riscritti
la usano. Esegui `mgctl init` dopo un aggiornamento per copiare i file distribuiti mancanti
nella directory radice; i file modificati esistenti vengono conservati. Per una directory radice esistente,
completa le due modifiche sotto **prima di avviare il server aggiornato**.

### Directory radice esistenti

`mgctl init` aggiunge le nuove sorgenti dei libri, il template della pergamena e lo script della pergamena, ma
conserva i tuoi `templates/items/jail.toml` e
`scripts/items/jail_note.lua` esistenti. Non può integrare queste modifiche automaticamente.
Esegui un backup di entrambi i file e integra queste modifiche mantenendo i campi personalizzati,
i commenti e le altre funzioni:

1. In `templates/items/jail.toml`, aggiungi questo campo al `[[item]]` con
   `id = "jail_release_note"` (oppure cambia il suo valore esistente):

```toml
stackable = false
```

2. In `scripts/items/jail_note.lua`, sostituisci la funzione `on_use` esistente
   con questo delegato. Mantieni la dichiarazione `jail_note = {}` e le altre
   funzioni personalizzate:

```lua
function jail_note.on_use(serial, user)
    book.open(serial, user)
    return true
end
```

Senza la prima modifica, la nuova sorgente libro della prigione fallisce la validazione all'avvio.
Senza la seconda, il vecchio script legge solo `jail.text` e non può mostrare le nuove
note salvate come `book.content`. Il delegato gestisce entrambi i formati salvati. Sposta l'eventuale
testo personalizzato della nota di scarcerazione in `templates/books/jail_release_note.toml`;
le note già emesse conservano il loro testo salvato. Avvia il server dopo entrambe le integrazioni.

## Note della prigione

`jail_release_note.toml` contiene il testo esistente in otto lingue.
La prigione fornisce i giorni, la cella, le date UTC, la multa effettiva, il nome dello staff e il
motivo opzionale. Il suo `player_name` è il nome registrato con la pena. Le vecchie note con
solo `jail.text` rimangono leggibili, e `jail.cell`, `jail.days` e `jail.fine`
rimangono disponibili.

Un [libro](#books-and-parchments) è di sola lettura a meno che non sia uno [su cui un giocatore scrive](#books-a-player-writes-in).
