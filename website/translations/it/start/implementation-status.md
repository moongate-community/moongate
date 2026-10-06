<!-- translation: {"sourceHash":"5c574d3f418100d87b076f62152c60201995c7f3a60f703c9d2bf9872b184239","title":"Stato dell'implementazione"} -->

# Stato dell'implementazione

Moongate è in sviluppo attivo: **il mondo non è ancora un gioco**. Questa pagina indica cosa
fa oggi il server e cosa non fa. Descrive l'albero dei sorgenti attuale; il
[changelog](../CHANGELOG.md) registra le aggiunte di ogni release, e la
[checklist delle funzionalità](feature-checklist.md) passa in rassegna, sistema per sistema, ciò che normalmente offrono gli emulatori UO.
La [roadmap](roadmap.md) indica l'ordine in cui vengono realizzati i sistemi mancanti.

## A colpo d'occhio

| Area | Stato | In breve |
| --- | --- | --- |
| Login, realm e account | ✅ Funziona | Server di login, realm di gioco, trasferimento tra essi |
| Personaggi | ✅ Funziona | Creazione, eliminazione e ripristino, ingresso nel mondo, camminata e corsa |
| Altri giocatori | ✅ Funziona | Vedersi e parlare |
| Oggetti | 🟡 Parziale | Zaino, paperdoll, terreno, tooltip; i contenitori a terra si aprono e gli oggetti entrano ed escono |
| NPC | 🟡 Parziale | Regioni di spawn, script Lua, movimento casuale, camminata lungo un percorso; nessun combattimento |
| Mondo | 🟡 Parziale | Decorazione, porte e chiavi, teletrasporti e moongate pubblici (anche tra mappe), giorno e notte, meteo, stagioni, forzieri del tesoro dei dungeon che ricompaiono e contenitori cittadini che si riempiono; nessuna casa |
| Combattimento, morte, incremento delle abilità | ❌ Non ancora | |
| Scripting Lua | ✅ Funziona | Script per NPC e oggetti, isolati |
| Persistenza | ✅ Funziona | PostgreSQL, salvataggi del mondo, migrazioni, backup SQL a rotazione |
| Amministrazione | 🟡 Parziale | Comandi da console e in gioco, API gRPC; nessun pannello web |

## Cosa può fare un giocatore

- Effettuare il login, scegliere un realm, creare un personaggio (con i suoi oggetti iniziali) ed entrare nel mondo.
- Camminare e correre, con il server che controlla terreno, elementi statici, oggetti sul percorso (una porta
  chiusa, una cassa, un muro) e velocità.
- Vedere gli altri giocatori entro 18 caselle e parlare con loro.
- Aprire lo zaino, spostare gli oggetti al suo interno, dividere e unire pile, lasciare oggetti a terra e
  raccoglierli; gli oggetti lasciati a terra decadono.
- Aprire la paperdoll, vestirsi e svestirsi (incluse armi a due mani).
- Leggere tooltip e nomi di ciò che è visibile.
- Leggere pergamene personalizzate e libri nativi dai [template di testo](data-files/books.md): il testo resta fisso quando vengono scambiati.
  Scrivere titoli, autori e pagine nei libri scrivibili portati nello zaino o in una cassetta di banca aperta.
  Gli [oggetti iniziali](data-files/starting-items.md#personalized-starting-letters) possono consegnarli in modo transazionale; il set comune fornito include una lettera di benvenuto e un libro scrivibile vuoto.
- Aprire le porte, e quelle chiuse a chiave portando la loro chiave; accendere e spegnere le luci.
- Vedere passare giorno e notte, dungeon bui, e meteo, stagione e musica di ogni regione
  (pioggia, neve, temporali).
- Camminare su un teletrasporto, o pronunciare la parola di uno che risponde a una parola, e arrivare altrove, anche su
  un'altra mappa; entrare in un moongate.
- Leggere l'ora di gioco e le fasi lunari nel luogo in cui si trova: `.time`.
- Incontrare NPC che vagano intorno alla propria casa, salutano e rispondono e, quando lo prevede lo script, raggiungono un
  luogo o seguono qualcuno aggirando gli ostacoli.
- Aprire la cassetta di banca da un banchiere dicendo *bank*, in qualsiasi lingua del client; chiedere il *balance*,
  *withdraw* e *deposit* dell'oro tramite parlato, farsi emettere un *check* bancario e incassarlo con un doppio
  clic, oppure consegnare oro e assegni al banchiere per depositarli. La cassetta contiene un numero limitato di oggetti: [Banca](bank.md).
- Recuperare punti vita, mana e stamina nel tempo, avere fame e sete, mangiare e bere; stancarsi correndo o trasportando troppo peso.
- Aprire i forzieri del tesoro dei dungeon e le casse dei negozi che si riempiono; leggere un orologio; cambiare modalità guerra.
- Tingere i vestiti: le tinture danno alla vasca il colore scelto nel selettore del client, e la vasca lo trasferisce agli abiti.

## Cosa può fare un game master

- Generare e rimuovere singoli NPC: `.spawn`, `.remove`; ucciderne uno, che lascia un cadavere con ciò che
  trasportava: [`.kill`](commands/kill.md), vedi [Morte e resurrezione](death.md).
- Vedere le regioni di spawn nel luogo in cui si trova: `.spawns`; ricevere un messaggio quando le regioni generano NPC.
- Raggiungere qualsiasi punto di qualsiasi mappa, o uno dei 558 luoghi nominati, per nome o da un gump che
  li elenca per mappa e categoria: [`.go`](commands/go.md); attraversare le porte.
- Chiudere e aprire le porte a chiave e crearne le chiavi: `.lock`, `.unlock`, `.key`.
- Forzare luce, meteo o stagione, provare un brano musicale: `.globallight`, `.weather`,
  `.season`, `.music`.
- Provare qualsiasi gump su di sé: `.gump`.
- Mandare un giocatore o un NPC in una cella per alcuni giorni, o liberarlo, da un gump che elenca
  le celle: [`.jail`](commands/jail.md). La pena termina automaticamente, con una multa e una nota di rilascio.
  `.jail <name>` incarcera un giocatore offline: la cella viene riservata e i giorni iniziano al login;
  vedi [Prigione](jail.md).
- Leggere, pubblicare, rispondere e rimuovere sulle bacheche cittadine, ciascuna con i propri messaggi;
  le discussioni scadono e una bacheca piena elimina la più vecchia: [Bacheche](bulletin-boards.md).
- Impostare fama e karma, ispezionare ciò che seleziona un cursore bersaglio: `.fame`, `.karma`, `.where`.
- Creare pergamene e libri personalizzati nel proprio zaino: [`.book`](commands/book.md).
- Creare oro e assegni bancari dal nulla: `.add_gold`, `.create_check`.
- Ripristinare un personaggio in attesa di eliminazione: `.character`.

Un amministratore posiziona anche la decorazione, riempie di nuovo le regioni di spawn (`.initial_spawn`),
salva, esegue un backup SQL (`.sql_backup`), invia messaggi globali, spegne il server e gestisce gli account.

Vedi tutti i comandi in [Comandi](commands.md).

## Non ancora realizzato

- Combattimento, morte, cadaveri e incremento delle abilità.
- Un'IA integrata: gli NPC eseguono soltanto il proprio script Lua (`on_think`, `on_speech`, `on_spawn`,
  `on_mobile_in_range`), che può farli camminare lungo un [percorso](scripting/mobile-scripts.md#walking-a-path) con
  `npc.walk_to`; nulla insegue, fugge o combatte autonomamente.
- Recall e gate travel, e cavalcature.
- Case e barche (posizionamento, e multi nel movimento e nella linea di vista).
- Limiti di peso dei contenitori a terra, vestire altri personaggi, requisiti di forza.
- Regole delle regioni: guardie e case. Le regioni controllano meteo, luce dei dungeon, musica e stagione.
- Oggetti spawner (le [regioni di spawn](spawns.md) si occupano della rigenerazione).
- Lingua per giocatore, comando di ripristino dei backup SQL, pannello web di amministrazione.
- Vecchia cifratura AES/E3 di Kingdom Reborn. L'Enhanced Client effettua il login, crea un personaggio, entra nel mondo e
  cammina; il resto è parziale: vedi [Enhanced Client](enhanced-client.md).

## Per area

### Rete e login

- TCP con framing, pipeline per connessione, registri delle sessioni e arresto ordinato.
- Cifratura del client compatibile con POL con politiche `Disabled`, `Optional` e `Required`
  ([configurazione](server-configuration.md#uo-client-encryption)).
- `mode` esegue server di login e gioco separati oppure un processo standalone. I realm di gioco si annunciano
  tramite lease Redis; il login li filtra per livello dell'account e trasferisce il giocatore
  con un ticket monouso.
- I pacchetti che il server gestisce e invia sono elencati nella [guida di riferimento dei pacchetti](packets.md).

### Mondo

- **Movimento e vista:** percorribilità e altezza di arrivo da terreno ed elementi statici (come
  ModernUO), linea di vista (come POL e ModernUO). Gli oggetti a terra bloccano il movimento (una porta chiusa, una cassa); mobile e
  multi non fanno ancora parte di questi controlli, e la linea di vista ignora gli oggetti. Vedi [Interrogazioni del mondo](world-queries.md).
- **Settori delle mappe:** giocatori, NPC e oggetti a terra si vedono entro la portata visiva; gli NPC lontani da
  ogni giocatore restano inattivi.
- **Luce:** un orologio di gioco con giorno e notte per mappa e longitudine e le fasi delle due lune;
  dungeon bui e prigioni poco illuminate.
- **Regioni, meteo, musica e stagioni:** viene seguita la regione di ogni giocatore; ogni regione ha
  il meteo UOX3, estratto ogni ora di gioco (asciutto al chiuso), il proprio brano musicale e, se impostata, la stagione;
  le stagioni delle mappe possono ruotare con i giorni di gioco.
- **Decorazione:** decorazione del mondo di ModernUO (e New Haven di ServUO) posizionata da `.decorate`: porte
  (quelle cittadine lette dai telai delle porte della mappa), serrature e chiavi, insegne dei negozi, luci e
  teletrasporti, sia quelli su cui un giocatore cammina sia quelli che rispondono a una parola; i lampioni cittadini
  si accendono di notte.
- **Effetti:** effetti grafici in un punto, su un mobile o su un oggetto a terra, in volo dall'uno all'altro,
  e fulmini, dagli script con il modulo `effect`; particelle per l'Enhanced Client.
- **Spawn degli NPC:** regioni di spawn su ogni mappa, dai dati UOX3 e da quelli di ModernUO per New Haven, Malas,
  Tokuno e TerMur. Ogni regione si riempie fino al massimo al primo spawn dopo l'avvio, poi
  rigenera gli NPC gradualmente, su terra e acqua. Vedi [Spawn degli NPC](spawns.md).

### Scripting

- Lua 5.2 isolato con un budget di istruzioni, `wait`, timer, eventi, ricaricamento a caldo e definizioni
  per l'editor. Vedi [Scrivere script Lua](scripting.md).
- Moduli: `engine`, `log`, `timer`, `events`, e nel plugin Ultima `dice`, `localization`,
  `npc`, `item`, `world`, `mobile`, `target`, `prompt`, `gump`, `bank`, `effect`, `moongates`, `locations`, `jail`, `board`, `book` e `commands`.
  Ogni funzione offerta agli script è elencata nella [guida di riferimento API Lua](https://moongate.sh/lua/),
  generata dal codice del server.
- Gump: layout XML verificati da `gump.xsd`, uno script Lua per gump per le risposte, slot e gump interi
  costruiti in Lua, e gump concatenati con `bind` e `open`; vedi [Gump](gumps.md) e
  [Il tuo primo gump](gump-tutorial.md).
- Gli script di mobile e oggetti sono associati dai template tramite `script_id`. Script forniti:
  `door.lua`, `light.lua`, `potion.lua`, `teleporter.lua`, `keyword_teleport.lua`, `public_moongate.lua`, `moongate.lua`, `clock.lua`, `fillable.lua`, `jail_note.lua`, `readable_book.lua`, `readable_scroll.lua`, `bulletin_board.lua`, `gumps/go.lua`, `gumps/jail_sentence.lua`, `wander.lua`, `monster.lua`, `guard.lua`,
  `banker.lua`, e i gatti Orione e Vega; i gump del tutorial hanno `gumps/tutorial_greeting.lua`
  e `gumps/tutorial_list.lua`.
- Non ancora: timer sui mobile, eventi di combattimento e abilità.

### Dati e template

- Dati dello shard in `data/` (mappe, città iniziali, abilità, professioni, razze, nomi, contenitori,
  corpi, regioni, meteo, messaggi), validati all'avvio. Vedi [File di dati](data-files.md).
- Template in `templates/`: oggetti, bottino, mobile, elenchi NPC, regioni di spawn e decorazione, con
  ereditarietà `base_id`, e i gump XML di `templates/gumps`. Vedi
  [Caricare template TOML](templates.md) e [Gump](gumps.md).
- Sorgenti di documenti semplici in `templates/books`, con variabili nominate risolte e salvate su singole
  pergamene e libri alla creazione. Gli [allegati alle lettere](data-files/books.md#letter-attachments) sono congelati per lettera e consegnati una sola volta a chi la porta nello zaino, con peso differito e controlli atomici della capienza. Il convertitore fornisce [62 libri di lore in otto lingue](book-content-import.md) da ModernUO. I libri nativi hanno copertine e pagine sfogliabili; quelli scrivibili consentono al portatore di modificare titolo, autore e pagine, con modifiche salvate sull'oggetto. Vedi [Template di testo leggibile](data-files/books.md).
- File del client letti da `ultima.ultima_path`: dati delle caselle, mappe (MUL o UOP) e multi.
- Messaggi in 8 lingue, portati da UOX3; una lingua può essere suddivisa in più file
  toml. Vedi [Localizzazione](localization.md).

### Persistenza

- Due database PostgreSQL (account e mondo) con transazioni e migrazioni SQL versionate,
  applicate da `mgctl migrate`.
- Personaggi, relativi oggetti, oggetti a terra e NPC vengono mantenuti in memoria e scritti dal salvataggio
  periodico del mondo; i personaggi vengono salvati anche quando escono.
- Non ancora: un comando di ripristino; il ripristino di un [backup SQL](persistence-operations.md#database-backups) è un passaggio manuale con `psql`.

### Amministrazione e strumenti

- [Comandi](commands.md) da console e in gioco, tradotti in ogni lingua fornita.
- Un'eccezione registrata è una riga sulla console e un rapporto Markdown in `logs/errors`, pronto
  per una issue GitHub; vedi [Quando qualcosa non funziona](getting-started.md#when-something-fails).
- [API di amministrazione](admin-api.md) gRPC facoltativa con TLS: account e informazioni sul server. Nessun pannello web
  o operazione sui personaggi ancora.
- I plugin in `plugins/` registrano servizi, comandi, moduli Lua, metriche, entità, SQL e
  la propria sezione di configurazione. Vedi [Scrivere un plugin](plugins.md).
- Strumenti: [`mgctl`](mgctl.md) prepara la root del server, applica le migrazioni del database e converte
  contenuti UOX3 e ModernUO. Un [esempio Docker](docker-login-realms.md) esegue un server di login e
  due server di gioco.

## Impostazioni ancora senza effetto

Le opzioni da riga di comando `--log-level` e `--log-packets` vengono analizzate e validate ma nulla
le usa ancora; vedi la
[guida di riferimento della configurazione](server-configuration.md#settings-and-validation).
