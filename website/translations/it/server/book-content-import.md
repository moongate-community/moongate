<!-- translation: {"sourceHash":"dd2cb934d5c68701aca0e3f07ab0246ddbbfff4b4d41c38f7267b88b46ed81be","title":"Importare i testi dei libri"} -->

# Importare i testi dei libri

`mgctl convert modernuo-books` legge i testi statici dei libri distribuiti con ModernUO
e scrive [template di documenti leggibili](data-files/books.md) Moongate. Analizza la
sintassi C# senza compilare o eseguire codice dell'emulatore. Questi documenti sono
[libri](data-files/books.md#books-and-parchments): aprono il libro nativo del client,
in sola lettura, ciascuno con la copertina della propria sorgente.

## Convertire il catalogo

```sh
dotnet run --project src/Moongate.Ctl -c Release -- convert modernuo-books \
  --source <ModernUO>/Projects/UOContent \
  --destination moongate_root/templates/books/modernuo
```

Con un tool rilasciato, usa `mgctl convert modernuo-books` con le stesse opzioni.
`--source` è una cartella in cui cercare ricorsivamente i file `.cs`; usa l'intera
cartella `Projects/UOContent` per includere i diari Khaldun oltre alla biblioteca.
`--destination` è la cartella che riceve i TOML generati. Cartelle/file sorgenti e
file di output che sono link simbolici vengono rifiutati.

Il catalogo distribuito contiene **62 libri, 738 pagine sorgenti e 5.635 righe sorgenti**,
convertiti dalla [revisione ModernUO `35e3a31b`](https://github.com/modernuo/ModernUO/tree/35e3a31b4c3af5668f0f0e2d3045b328ffb26b57).

| Sorgente sotto `Projects/UOContent` | Libri | Pagine sorgenti |
| --- | ---: | ---: |
| `Items/Books/Defined/LibraryBooks.cs` | 28 | 486 |
| Gli altri sei file `Items/Books/Defined/*.cs` | 6 | 88 |
| `Engines/Khaldun/Books/GrimmochJournal.cs` | 9 | 46 |
| `Engines/Khaldun/Books/LysanderNotebook.cs` | 6 | 33 |
| `Engines/Khaldun/Books/TavarasJournal.cs` | 13 | 85 |

Le sei definizioni singole sono BlackthornWelcomeBook, DrakovsJournal, FropozJournal,
KaburJournal, NewAquariumBook e TranslatedGargoyleJournal. La
[licenza](https://github.com/modernuo/ModernUO/blob/35e3a31b4c3af5668f0f0e2d3045b328ffb26b57/LICENSE)
del repository sorgente e l'attribuzione narrativa esistente rimangono la provenienza
upstream. Nessun codice sorgente viene copiato nel convertitore.

## Mapping e riesecuzioni

Ogni campo static readonly `BookContent` fornisce titolo, autore e righe ordinate
`BookPageInfo`. Sono supportate le stringhe letterali, comprese stringhe C# con
escape/verbatim/raw e concatenazione di letterali. Espressioni runtime, metadati/pagine
mancanti, testo non valido, ID duplicati e testo che non rientra negli attuali pacchetti
di lettura fanno fallire la conversione con codice di uscita 2. Tutta l'analisi,
validazione e serializzazione termina prima di modificare qualsiasi file di output.
Un errore filesystem durante le scritture può lasciare alcuni file aggiornati;
correggi il percorso segnalato e riesegui.

Una classe diventa un nome file stabile `<snake_case_class>.toml`. Per esempio,
`GrammarOfOrcish` diventa `grammar_of_orcish`. I titoli vengono conservati e non sono
mai usati per deduplicare: gli episodi di Grimmoch, Lysander e Tavara rimangono distinti.
Spazi iniziali, ortografia e annotazioni restano come scritti. Le righe vengono unite
con un a capo e le pagine sorgenti con due a capo; una riga vuota all'interno di una
pagina ModernUO viene scritta come riga di uno spazio. Questo conserva nel testo
memorizzato le interruzioni di pagina sorgenti, comprese le pagine vuote, ma la
paginazione visualizzata segue i limiti nativi del libro Moongate: 79 unità di codice
UTF-16 per riga e 8 righe per pagina. Le righe più lunghe vanno a capo e le pagine
sorgenti più lunghe proseguono su un'altra pagina visualizzata. Gli a capo finali
vengono rimossi all'apertura, quindi le pagine sorgenti vuote finali non vengono
visualizzate. Per esempio, `children_tales_vol2` ha 10 pagine sorgenti e ne mostra 9;
il catalogo inglese distribuito mostra 737 pagine in totale.

Il corpo TOML UTF-8 è multilinea quando quella rappresentazione conserva esattamente
il testo; a capo iniziali e sequenze CR/CRLF usano stringhe basic con escape quando
necessario. Unicode non valido viene rifiutato, e i campi serializzati sono controllati
tramite deserializzazione prima della scrittura. L'importatore valida il limite della
sorgente con escape e raddoppia i caratteri `$` letterali affinché il
[formatter dei template](data-files/books.md#variables) li mostri letteralmente.
I libri importati non richiedono valori di variabili o allegati e usano
`item_template = "readable_book"`.

La copertina è la grafica che la classe passa al costruttore base, scritta come
`item_id`: un letterale (`base(0xFF2, false)`), il primo di una coppia casuale
(`Utility.Random(0xFEF, 2)` dà `0x0FEF`), oppure quella del tipo di libro da cui la
classe deriva (`RedBook`, `BlueBook`, `BrownBook`, `TanBook`). Una classe che non
indica nulla non riceve `item_id` e mostra la copertina rossa di `readable_book`.
Il catalogo distribuito ha 28 libri marroni, 31 rossi e 3 blu. Un testo che richiede
più di 255 pagine da 8 righe, in inglese o in una traduzione conservata, fa fallire la conversione.

Ogni libro distribuito conserva la sorgente inglese e include sostituzioni complete
di titolo/corpo per italiano (`ita`), francese (`fre`), tedesco (`ger`), spagnolo
(`spa`), portoghese (`por`), polacco (`pol`) e ceco (`cze`). I nomi degli autori
rimangono invariati. Il servizio documenti esistente sceglie `[localization].language`
alla creazione dell'oggetto; i campi di traduzione mancanti usano la sorgente inglese.
Cambiare quell'impostazione non riscrive i libri già emessi.

Rieseguire sostituisce i nomi file generati corrispondenti, conserva i file non correlati
come la lettera di benvenuto e non rimuove i file di un catalogo precedente. Metti
le modifiche personalizzate in un template con nome diverso se devono sopravvivere alle riesecuzioni.

I campi `translations.<language>` esistenti nei file generati corrispondenti vengono
conservati durante la reimportazione. Sono accettati tutti gli otto codici lingua
supportati, comprese le sostituzioni facoltative `eng`; le sostituzioni parziali
usano come fallback i campi inglesi aggiornati. L'importatore valida TOML esistente,
Unicode, testo letterale dei template, limiti della sorgente/rendering e pacchetti
di lettura prima di scrivere qualsiasi file. Traduzioni non valide arrestano la
conversione con codice di uscita 2 e lasciano invariato l'output esistente. Gli altri
campi modificati sono rigenerati da ModernUO; i file non correlati rimangono intatti.
Le traduzioni esistenti sono conservate esattamente quando cambia l'inglese upstream,
quindi verificane il significato dopo l'importazione di una revisione upstream diversa.
Nessun servizio di traduzione di rete viene eseguito durante l'importazione.

Le traduzioni distribuite seguono le interruzioni di pagina della sorgente inglese
e usano la larghezza della riga inglese più lunga del libro come obiettivo per gli
a capo. Una parola indivisibile può superare quell'obiettivo. Conservano il rientro
di quattro spazi dove la pagina inglese apre un paragrafo. Una pagina sorgente
tradotta può richiedere più righe e pagine visualizzate della controparte inglese;
all'apertura si applicano gli stessi limiti di paginazione nativi del libro.

Il normale flusso `mgctl init` copia i file distribuiti mancanti nelle radici esistenti
e conserva quelli già presenti. Le nuove sorgenti si caricano al normale avvio successivo.
Per una radice esistente il cui catalogo precede queste traduzioni, copia le tabelle
`[translations.<language>]` desiderate dal catalogo distribuito nei file corrispondenti.
Né `mgctl init` né una reimportazione inventano traduzioni mancanti nei file esistenti.
Una radice che conserva il catalogo con i primi nomi, `modernuo_<book>.toml`, riceve
di nuovo i libri con i nomi senza prefisso: elimina manualmente i file
`modernuo_*.toml` di `templates/books/modernuo`, o ogni libro appare due volte, e usa
i nuovi id in `book.give` e `.book`. I documenti già emessi mantengono il testo salvato.
Nessun convertitore si connette al database del mondo o riavvia il server.

Per creare una copia leggibile da Lua:

```lua
book.give(player, "grammar_of_orcish")
```

## Cosa usano gli altri emulatori

Il confronto ha esaminato checkout locali degli emulatori e repository ufficiali di
distribuzione. ModernUO è stato scelto perché il suo intero catalogo statico è
letterale e già adatto al flusso del convertitore Moongate. I cataloghi alternativi
si sovrappongono ma hanno varianti distinte; questo comando importa solo ModernUO.

| Emulatore | Sorgente effettiva dei testi | Risultati |
| --- | --- | --- |
| ModernUO | Definizioni C# del server `BookContent` | 62 libri letterali / 738 pagine; importazione distribuita completa |
| ServUO | [Definizioni C# del server](https://github.com/ServUO/ServUO/blob/658d6b71a3b43aa02839dd893ca4f22c88abbbd3/Scripts/Items/Books/LibraryBooks.cs) | 74 definizioni / 856 pagine: 72 statiche dopo il fallback del titolo, due guide alla pesca hanno testo runtime sulla posizione. Altri libri localizzati usano stringhe Cliloc del client |
| UOX3 | [Catalogo DFN del server](https://github.com/UOX3DevTeam/UOX3/blob/4560ae841bac898817143d7aa95ce59f47ab98e0/data/dfndata/misc/books.dfn) | 65 libri effettivi; punteggiatura CP1252, sezioni ripetute con precedenza dell'ultima definizione e anomalie nel conteggio delle pagine |
| POL | Pacchetti [ModernDistro](https://github.com/polserver/ModernDistro/blob/fbb200c57559e08545e5938b7a72c31f787542a4/pkg/items/sysbook/config/master_library.cfg) / [ClassicDistro](https://github.com/polserver/ClassicDistro/blob/0cb44d16aab859f836652f76a8c802287f308961/pkg/items/sysbook/config/books.cfg), non il checkout core | 46 libri per catalogo; chiavi `p<page>l<line>`, riempimento con righe vuote e varianti diverse |
| Sphere | [Scripts-X `sp_tm_book.scp`](https://github.com/Sphereserver/Scripts-X/blob/27e78bc896da239d3738fe02a6d6bf8e9045c16d/templates_special/sp_tm_book.scp), non Source-X stesso | 45 libri / 536 sezioni di pagina definite; intestazione malformata, pagine mancanti/fuori intervallo e discrepanze nei metadati |

Libri scritti dai giocatori nei salvataggi del mondo, libri vuoti, script per
insegnare abilità e documenti solo Cliloc sono fuori da questa importazione di
narrativa statica.
