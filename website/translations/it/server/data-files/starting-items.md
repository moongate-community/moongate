<!-- translation: {"sourceHash":"0b2db168b5df03ff995e6d5c2660492c8ff3ddb902cdb53cc997511a36520406","title":"Oggetti iniziali"} -->

# Oggetti iniziali

`starting_items.toml` contiene gli oggetti ricevuti da un nuovo personaggio. Il file
distribuito è scritto da `newbie.dfn` UOX3, con voci specifiche dello shard come la
lettera di benvenuto. [`mgctl convert uox`](../uox3-migration.md#starting-items) può
rigenerare le sue voci UOX3. `IStartingItemsService.GiveAsync` lo applica a un nuovo personaggio.

Un personaggio riceve ogni `[[set]]` con `common = true`, più ogni insieme di cui
soddisfa i filtri.

```toml
[[set]]
skill = "alchemy"
[[set.items]]
items = ["0x0f7a_black_pearl"]
amount = 3
equip = false

[[set.items]]
items = ["0x1f03_robe"]
hue = 1226
equip = true
```

| Campo | Significato |
| --- | --- |
| `common` | `true` dà l'insieme a ogni personaggio, indipendentemente dai filtri |
| `skill` | Dato ai personaggi che iniziano con questa abilità tra le migliori |
| `race` | `human`, `elf` o `gargoyle`; non impostato indica tutte le razze |
| `gender` | `male` o `female`; non impostato indica entrambi |
| `items` | Id dei template oggetto; uno scelto casualmente |
| `amount` | Quantità, come dadi; non impostato indica 1 |
| `hue` | Tonalità da dare all'oggetto; non impostata mantiene la propria |
| `equip` | `true` mette l'oggetto sul personaggio, `false` nello zaino |
| `book_template` | Id facoltativo da `templates/books/<id>.toml`; scrive il testo sull'oggetto creato |
| `book_values` | Tabella inline dei valori personalizzati dichiarati dal documento; stringhe, numeri finiti o bool |
| `newbie` | `false` fa cadere l'oggetto alla morte; non impostato o `true` lo rende `Newbied` (conservato) |

`GiveAsync` lavora in una transazione sul database del mondo; se qualcosa fallisce,
il personaggio non riceve nulla:

1. Prende le `ultima.starting_items.best_skills` abilità più alte del personaggio
   (predefinito 3; a parità prevale l'id più basso; abilità a 0 non contano).
2. Applica, in ordine, gli insiemi di quelle abilità, quelli comuni, poi quelli di
   razza e genere del personaggio.
3. Crea lo zaino (`ultima.items.backpack_template`) e lo mette sul layer `Backpack`.
4. Per ogni voce sceglie un oggetto. Uno impilabile riceve l'intero `amount`;
   qualsiasi altro viene creato `amount` volte.
5. `equip = true` indossa l'oggetto sul suo layer: il `layer` del template, altrimenti
   quello del tiledata client quando la grafica è contrassegnata indossabile.
   Se non c'è layer, o è occupato, l'oggetto va invece nello zaino, quindi il primo
   oggetto su un layer prevale.
6. Camicie e tuniche indossate prendono la tonalità della camicia scelta alla creazione,
   pantaloni e gonne quella dei pantaloni; una tonalità 0 mantiene quella dell'oggetto.
7. Gli oggetti nello zaino vanno in una posizione casuale entro i limiti di `containers.toml`.

L'oro iniziale è una normale voce: il file distribuito dà 1000 monete tramite una
voce dell'insieme comune. Cambiane `amount` (massimo 65535) per darne più o meno,
o rimuovi la voce per non darne. Una radice preparata prima di questa modifica
mantiene il proprio `starting_items.toml`, che `mgctl` non sovrascrive: aggiungi
questa voce al suo insieme comune, o i nuovi personaggi inizieranno senza oro:

```toml
[[set]]
common = true
[[set.items]]
items = ["0x0eed_gold_coin"]
amount = 1000
equip = false
```

Anche cibo e bevande sono normali voci: l'insieme comune del file distribuito dà
 tre pagnotte e una brocca d'acqua, così un nuovo personaggio ha qualcosa contro
[fame e sete](../server-configuration.md). Né essi né l'oro fanno parte di
`newbie.dfn` UOX3: `mgctl convert uox` aggiunge da solo le tre voci all'insieme comune,
per gli oggetti presenti nella sorgente.

```toml
[[set.items]]
items = ["0x103b_bread_loaf"]
amount = 3
equip = false

[[set.items]]
items = ["0x1f9e_pitcher_of_water"]
equip = false
```

## Il libro vuoto

L'insieme comune distribuito dà anche a ogni nuovo personaggio un libro vuoto in
cui scrivere, come ModernUO: venti pagine, con il nome del personaggio come autore.
La sua sorgente è [`templates/books/blank_book.toml`](books.md#books-a-player-writes-in).
Una radice che mantiene il proprio `starting_items.toml` lo aggiunge al proprio insieme comune:

```toml
[[set.items]]
items = ["readable_book"]
equip = false
book_template = "blank_book"
```

## Lettere iniziali personalizzate

L'insieme comune distribuito dà a ogni nuovo personaggio una lettera di benvenuto
nello zaino. La sorgente è [`templates/books/welcome_letter.toml`](books.md), e il
nome del destinatario è quello del nuovo personaggio, anche prima del suo ingresso nel mondo.

Aggiungi questa voce **dentro l'insieme comune esistente**, accanto alle altre voci `[[set.items]]`:

```toml
[[set.items]]
items = ["readable_scroll"]
equip = false
book_template = "welcome_letter"
book_values = { contact_name = "Vega" }
```

Usa `book_template` senza suffisso `.toml`. Fornisci ogni variabile personalizzata
dichiarata, senza chiavi aggiuntive. I numeri usano formattazione indipendente dalla
lingua, i bool diventano `true` o `false`, e le stringhe inserite sono letterali:
`$player_name` dentro un valore personalizzato non viene espanso di nuovo. I valori
incorporati provengono dal contesto di creazione; non metterli in `book_values`.

Titolo, autore e corpo usano `[localization].language` e sono salvati come
`book.template`, `book.title`, `book.author` e `book.content` prima della persistenza
dell'oggetto. I libri scrivibili salvano anche `book.writable = true` e `book.pages`,
il numero minimo di pagine riservate. Il nome visualizzato segue il titolo renderizzato.
Scambiare la lettera, rinominare il destinatario o modificare la sorgente non può
cambiare il testo salvato. `amount` crea copie separate non impilabili; continuano
ad applicarsi le normali regole di tonalità, newbie e posizionamento nello zaino.

Gli [allegati](books.md#letter-attachments) della sorgente vengono congelati
separatamente per ogni lettera iniziale fisica e salvati prima della prima scrittura
dell'oggetto. Le ricompense restano nel diritto salvato finché il portatore non le
riscatta, quindi non aggiungono peso iniziale. Qualsiasi errore tardivo nella
preparazione degli allegati annulla il personaggio e tutti gli oggetti iniziali
precedenti. La sorgente di benvenuto distribuita non contiene ricompense; aggiungile
alla tua sorgente per abilitarle.

Ogni id oggetto della voce deve risolversi in `stackable = false` e usare
`script_id = "readable_scroll"`, `"readable_book"` o `"jail_note"`; i valori ereditati
contano. Una sorgente con allegati richiede che ogni oggetto candidato usi
`readable_scroll` o `jail_note`, che forniscono il gump di riscatto. Una sorgente
scrivibile richiede che ogni candidato usi `readable_book`. Gli id oggetto della
voce non devono necessariamente coincidere con `item_template` della sorgente:
una sorgente di sola lettura senza allegati può essere applicata a qualsiasi di
questi oggetti leggibili. Le voci di testo devono avere `equip = false`.
Se il rendering fallisce durante la creazione del personaggio, personaggio, zaino
e tutti gli oggetti iniziali vengono annullati nella stessa transazione.

Una radice esistente conserva il proprio `data/starting_items.toml` modificato
quando esegui `mgctl init`: aggiungi manualmente la voce sopra e riavvia il server.
Rimuoverla disabilita la lettera. `mgctl convert uox` rigenera gli oggetti iniziali
senza questa associazione specifica dello shard; aggiungila di nuovo dopo la conversione.

## Validazione all'avvio

Il server si arresta quando:

- `starting_items.toml` non esiste;
- un insieme non ha oggetti, o non è comune e non ha abilità, razza o genere;
- una voce non ha oggetti, nomina un oggetto che non è un template oggetto, o ha
  un `amount` che può produrre meno di 1 o più di 65535;
- `ultima.items.backpack_template` o `ultima.items.gold_template` non è un template oggetto;
- il template dell'oro non è impilabile;
- una voce di testo referenzia un documento sconosciuto, è equipaggiata, usa un
  oggetto inadatto, fornisce valori non validi/mancanti/aggiuntivi o non può
  renderizzare testo valido;
- una voce fornisce `book_values` senza `book_template`.

## Vedi anche

- [Panoramica dei file dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
