<!-- translation: {"sourceHash":"a2eaf92c4e83d90dc2501da94954a0ee1e40fd232ea1f78fe43ffbbcee411920","title":"Migrazioni: generare, revisionare e applicare"} -->

# Migrazioni: generare, revisionare e applicare

Le modifiche allo schema raggiungono PostgreSQL come file SQL versionati. Per impostazione
predefinita il server non li applica mai: all'avvio verifica che ogni file del catalogo sia stato
applicato senza modifiche e, in caso contrario, rifiuta di avviarsi. Un eseguibile separato,
il runner delle migrazioni, li applica mentre il server interessato è fermo. Uno shard può
scegliere invece l'[applicazione all'avvio](#apply-at-startup).

Questa pagina tratta la struttura del catalogo, i comandi, le regole dei file applicati e
la modalità di sviluppo facoltativa che genera SQL dalle modifiche alle entità. La mappatura
delle entità è in [Entità e accesso ai dati](persistence.md); stringhe di connessione e
ruoli sono in [Gestire PostgreSQL](persistence-operations.md). I due database e i
nomi dei relativi target sono elencati in
[Due database, quattro nomi ciascuno](persistence.md#two-databases-four-names-each).

## Directory delle migrazioni

Nella versione 0.6.0, server e runner leggono entrambi l'SQL core dalla directory
`migrations/` accanto all'eseguibile del server; il runner accetta anche
`--migrations-directory`. Non c'è nulla da configurare.

Dalla release successiva alla 0.6.0, il server legge `<root>/migrations` a meno che
`persistence.migrations_directory` selezioni un'altra directory, e `mgctl` vi copia
l'SQL core della release e scrive quel percorso assoluto in una nuova
configurazione; vedi [mgctl](mgctl.md#prepare-a-server-root). Il runner usa
`--migrations-directory` se specificato, poi `migrations_directory` configurato, poi
la directory accanto all'eseguibile del server. Mantieni esplicito `migrations_directory`
perché server e runner leggano lo stesso catalogo.

## File SQL versionati

```text
migrations/
  auth/0001_create_accounts.sql
  world/0001_create_characters.sql
plugins/MyPlugin/migrations/
  manifest.json
  world/0001_create_guilds.sql
```

I file usano `NNNN_description.sql`: sequenze da `0001` a `9999`, lettere minuscole,
cifre e underscore nella descrizione, nessuna sottodirectory. Una sequenza
è univoca all'interno di un componente e target. I file non SQL in una directory target vengono
ignorati.

Un plugin distribuisce il proprio SQL nel bundle con un `manifest.json` che dichiara un ID
stabile del componente, indipendente dalla cartella del bundle o dal nome CLR:

```json
{ "id": "my-plugin" }
```

Usa un ID minuscolo univoco di massimo 63 lettere, cifre o trattini, che inizi con una
lettera; `core` è riservato. Non rinominare mai un componente dopo averne applicato l'SQL.
L'appartenenza al componente segue il bundle del plugin caricato, non il suo namespace C# o
schema PostgreSQL.

L'ordine di esecuzione è prima core, poi i plugin per ID del componente in ordine ordinale, poi
la sequenza crescente di ciascun componente. Non c'è un risolutore delle dipendenze: progetta
l'SQL tra plugin in base a quell'ordine, oppure sposta le modifiche condivise nel core. Auth e World hanno
cataloghi e cronologie indipendenti, anche se configurati sullo stesso database.

## Generare, revisionare e applicare

Genera rispetto a un **database di riferimento alla versione precedente dell'applicazione**.
Un database nuovo e vuoto produce la creazione iniziale delle tabelle, non una modifica incrementale.
Carica le stesse registrazioni di entità e plugin della versione in sviluppo:

```sh
mgserver --root-directory /srv/moongate/reference --persistence-schema preview
mgserver --root-directory /srv/moongate/reference \
  --persistence-schema generate --migration-target world \
  --migration-output ./migrations/world/0001_create_characters.sql
```

`preview` stampa la bozza DDL di FreeSql. `generate` salva la bozza del target selezionato
e rifiuta un file esistente o un diff vuoto. Nessuno dei due esegue SQL o avvia
servizi host, socket, protezioni PID o certificati. La generazione include **tutti i
moduli registrati per il target selezionato**. Per SQL specifico di un plugin, usa una
root di riferimento contenente solo quel plugin e le dipendenze necessarie, e verifica
che il file risultante tocchi solo gli schemi di sua proprietà. I moduli registrati in
altri target richiedono comunque connessioni di riferimento risolvibili per il confronto degli schemi.

Revisiona l'SQL, aggiungi trasformazioni dei dati deliberate, provalo, poi includi il
file nel commit con la modifica all'entità. Distribuisci gli stessi file revisionati a ogni database
interessato. Arrestane i processi di runtime e usa il runner:

```sh
./mgctl migrate status \
  --root-directory /srv/moongate/realm-1 --target world
./mgctl migrate apply \
  --root-directory /srv/moongate/realm-1 --target world
```

Il runner legge `config/moongate.toml` e `plugins/` di quella root; `--plugins-directory` indica
un'altra directory di bundle di plugin in cui cercare le migrazioni. `--target auth`
seleziona `[persistence.accounts]`; `--target world` seleziona `[persistence.realm]`.
Viene risolta solo la connessione selezionata. `MOONGATE_ROOT` è un'alternativa a
`--root-directory`; senza nessuno dei due, `mgctl` usa la propria directory, quella del server, come
root dei dati. `mgctl` è un processo autonomo, con un proprio driver PostgreSQL, isolato
dal driver Npgsql di FreeSql; non carica mai DLL dei plugin. Entrambi i comandi restituiscono codice di uscita 0 in caso di successo e 1 in caso di errore; `status`
segnala i file in attesa senza applicarli.

Da un checkout dei sorgenti, specifica esplicitamente il percorso del catalogo:

```sh
dotnet run --project src/Moongate.Ctl -- migrate status \
  --root-directory /srv/moongate/reference --target world \
  --migrations-directory ./migrations
```

Dalla versione 0.6.0, `mgserver --persistence-schema apply` non è supportato:
indirizza al runner. Per l'output del server dipendente dal framework usa
`dotnet mgserver.dll ...`.

## Regole dei file applicati

[DbUp](https://dbup.readthedocs.io/en/latest/) esegue i file revisionati. Moongate
aggiunge un registro di checksum in `moongate_migrations.history`, registrando target,
componente, nome file, SHA-256 e momento dell'applicazione. I checksum normalizzano CRLF in LF
e ignorano un BOM UTF-8, quindi un checkout Windows non cambia l'identità di una migrazione.
Per il resto, **i file applicati sono immutabili**: non modificarli, rinominarli o rimuoverli mai.
Aggiungi una sequenza superiore per le correzioni. Un componente installato con un file
applicato mancante, un checksum diverso o una sequenza precedente inserita arresta il runner
prima che venga eseguito nuovo SQL. Cronologia e dati di un plugin rimosso restano intatti.

Ogni applicazione acquisisce un advisory lock PostgreSQL esteso all'intero database sulla propria transazione
di esecuzione, verifica la cronologia, poi applica **tutti gli script in attesa e le righe del registro
in una sola transazione**. Un errore SQL annulla il batch. I runner concorrenti
vengono serializzati e una seconda applicazione non fa nulla. I comandi hanno un timeout di esecuzione
di 60 secondi. Un errore di connessione intorno al commit può lasciare il risultato incerto: controlla
`status` e la cronologia prima di riprovare. Non esiste una transazione tra Auth e
World o tra realm. L'advisory lock non sospende il gioco: arresta i runtime interessati
prima di un intervento di manutenzione.

Gli script devono essere SQL PostgreSQL transazionale. I blocchi `DO` con dollar quoting funzionano;
la sostituzione delle variabili DbUp è disabilitata. Non inserire `BEGIN`, `COMMIT`, `ROLLBACK`,
`SAVEPOINT`, `PREPARE`, `SET`, `RESET` o altri controlli di transazione o sessione
al livello principale. Usa nomi qualificati con lo schema invece di `SET search_path`. Comandi
come `CREATE INDEX CONCURRENTLY`, `VACUUM` e i metacomandi psql richiedono procedure separate
gestite dall'operatore; il runner non allenta la garanzia di transazionalità.

FreeSql confronta il modello corrente con attributi con il database corrente; non può
dedurre il significato applicativo o il modello precedente dell'applicazione. Usa `OldName` per
le rinomine supportate e revisionane il DDL. Scrivi SQL esplicito per riempimenti dei dati esistenti,
suddivisioni di valori, conversioni di unità, fusioni di dati e nuove invarianti. I file di soli dati seguono
le stesse regole di numerazione e cronologia e bloccano l'avvio normale fino all'applicazione.

I commenti di tabelle e colonne sono esclusi dal confronto. FreeSql li ricava dalla documentazione XML
(`/// <summary>`) di un'entità e delle sue proprietà, e il server pubblicato come file unico
non può leggere quei file, quindi vedrebbe tutti i commenti come rimossi. L'SQL revisionato
scrive i commenti conservati dal database, come fa `migrations/world/0003_world_column_comments.sql`;
modificare la documentazione di un'entità persistita non richiede migrazioni, e un'istruzione `COMMENT ON`
in un file revisionato è il modo per cambiare un commento. Con la generazione automatica attiva, una
bozza contiene comunque i nuovi commenti che una build con la propria documentazione XML vede, e mai uno che
rimuoverebbe un commento (`IS NULL` o `IS ''`).

Prova con dati rappresentativi. Applica i file revisionati, conferma che `status`
non segnali modifiche in attesa e che `preview` non segnali modifiche allo schema, poi avvia il
nuovo server. Downgrade e manutenzione non transazionale sono gestiti dall'operatore.
Non esiste una migrazione inversa automatica; esegui un
[backup SQL](persistence-operations.md#database-backups) prima dell'applicazione.

## Il catalogo auth core

Il catalogo auth core contiene `0001_account_id_sequence.sql`, `0002_accounts.sql`,
`0003_account_serial_ownership.sql` e `0004_account_admin_api_access.sql`. Il terzo
associa `auth.account_id_seq` esistente ad `auth.accounts.id` senza azzerarne
il valore; una sequenza generata in sviluppo già associata viene mantenuta.
Il quarto aggiunge la colonna `can_access_api`, `false` per ogni account esistente. Nomi utente
duplicati esistenti, o nomi utente o hash delle password nulli, devono essere risolti prima che
la migrazione dei vincoli possa essere applicata; nessun account viene eliminato silenziosamente. Il catalogo
world core contiene `0001_mobiles.sql`, `0002_items.sql`, `0003_world_column_comments.sql` e
`0004_item_rarity.sql`, che aggiunge la colonna `rarity` (gli oggetti esistenti diventano Common) e
il relativo controllo `ck_items_rarity`, e `0005_mobile_npc_fields.sql`, che aggiunge le colonne NPC
di `world.mobiles` (ID template, titolo, notorietà, punti vita, mana, stamina, fama, karma,
armatura, resistenze, props; le righe esistenti ricevono 0 o null) e il relativo controllo `ck_mobiles_notoriety`.
`0006_mobile_slot.sql` aggiunge `slot` dell'elenco personaggi con il relativo controllo e un indice univoco
su account e slot, `0007_mobile_deletion.sql` la colonna `deletion_requested_at`,
`0008_mobile_slot_int.sql` amplia `slot` a `INT4`, `0009_mobile_direction.sql` aggiunge `direction`
(i mobile esistenti guardano a sud) con il relativo controllo, `0010_item_ground_index.sql` l'indice parziale sugli
oggetti a terra e `0011_item_grid_index.sql` la colonna `grid_index` della griglia contenitori
dell'Enhanced Client, numerando gli oggetti già in un contenitore. Il DDL di tabelle e sequenze proviene
dal generatore di sviluppo; chiavi esterne, vincoli CHECK e indici parziali
sono scritti a mano, perché il generatore produce solo colonne e sequenze, e il
controllo dello schema all'avvio li accetta. Il plugin di esempio distribuisce
`world/0001_create_notes.sql`. `0012_mobile_flags.sql` aggiunge le colonne `hidden` e `frozen` dei mobile, e
`0013_world_state.sql` la tabella a riga singola `world.state` (`0014_world_state_one_row.sql` fa rifiutare al database una seconda riga; `0015_mobile_hunger.sql` e `0016_mobile_thirst.sql` aggiungono le colonne `hunger` e `thirst` dei mobile, `0017_mobile_criminal.sql` il momento `criminal_until`; `0018_jail_sentences.sql` aggiunge la tabella `world.jail_sentences` della [prigione](jail.md), `0019_jail_sentence_reason.sql` la relativa colonna `reason` e `0020_jail_sentence_pending.sql` la colonna `pending`; `0021_bulletin_messages.sql` aggiunge la tabella `world.bulletin_messages` delle [bacheche](bulletin-boards.md); `0022_items_container_deferred.sql` consente al salvataggio del mondo di scrivere un oggetto prima del contenitore in cui si trova, rinviando quel controllo alla fine del salvataggio), con le props che gli script conservano per l'intero
shard.

## Applicare all'avvio

Per impostazione predefinita il server verifica soltanto: con SQL in attesa, o una root che manca dei file
distribuiti da una nuova release, si ferma e indica cosa eseguire. Per uno shard con un solo operatore, o un container la cui immagine
viene sostituita a ogni aggiornamento, il server può compiere autonomamente quei due passaggi:

```toml
[persistence]
auto_apply_migrations = true
```

Prima del controllo dello schema, un avvio quindi:

1. Aggiunge alla directory delle migrazioni l'SQL core incluso nel server che manca nella directory,
   come fa `mgctl init`. Un file già presente non viene mai sostituito; uno con il numero di un file incluso
   ma nome o contenuto diverso interrompe l'avvio senza copiare nulla.
2. Applica l'SQL revisionato in attesa dei database usati da questo processo (auth per un server di login,
   world per un server di gioco, entrambi in modalità standalone), tramite `mgctl migrate apply`: stesso advisory lock,
   stessa cronologia, e una bozza marcata `-- moongate:review-required` lo arresta comunque.

Non viene generato nulla dalle entità. Viene eseguito ogni file in attesa del catalogo: l'SQL
distribuito con la release, ogni file inserito dall'operatore nella directory delle migrazioni e l'SQL dei
bundle dei plugin in `plugins/`, come con `mgctl migrate apply`. L'aggiunta di un bundle di plugin esegue quindi
le sue migrazioni all'avvio successivo. Il ruolo database della connessione deve poter modificare lo schema; dove il
ruolo di runtime non può farlo, come nell'[esempio con login e realm](docker-login-realms.md), mantieni l'opzione
disattivata e applica con i job dello schema. Non c'è ritorno automatico: esegui un
[backup SQL](persistence-operations.md#database-backups) prima di un aggiornamento. Con
`auto_generate_migrations` anch'esso attivo, viene eseguita la copia e si applica l'avvio di sviluppo descritto sotto. L'opzione
è in conflitto con `auto_sync_schema`.

## Migrazioni automatiche di sviluppo

Per un database di sviluppo eliminabile, il server può generare e applicare SQL dalle
modifiche alle entità all'avvio. Attivale esplicitamente:

```toml
[persistence]
auto_sync_schema = false
auto_generate_migrations = true
migrations_directory = "${MOONGATE_ROOT}/migrations"
```

Imposta `MOONGATE_ROOT` prima di avviare il server, oppure usa una directory assoluta.
`migrations_directory` supporta variabili d'ambiente e `~`. Puoi indirizzarlo alla
directory `migrations/` del repository perché i file generati siano pronti per il
commit. La directory viene creata se manca. La generazione richiede questo percorso
esplicito; non scrive mai silenziosamente nella directory di output della build. Mantieni
`auto_generate_migrations` disabilitato in distribuzione. È in conflitto con
`auto_sync_schema`, che applica modifiche allo schema non versionate e non registra nulla.

### Cosa fa un avvio di sviluppo

1. Controlla le connessioni PostgreSQL configurate e verifica i checksum delle migrazioni.
2. Applica l'SQL revisionato in attesa tramite il runner isolato delle migrazioni.
3. Confronta le entità registrate, salvando le modifiche come `NNNN_auto_schema.sql` per target
   e componente, senza sovrascrivere file precedenti.
4. Applica le modifiche additive, ricontrolla il database e poi pubblica `PersistenceReady`.

### Cosa gestisce la generazione

Una nuova entità crea la migrazione iniziale, inclusa la sua sequenza Serial appartenente
alla colonna. Le tabelle esistenti senza sequenza ricevono una migrazione additiva che
la inizializza sopra il massimo ID memorizzato. L'aggiunta di una proprietà nullable crea
la migrazione successiva. Un riavvio senza modifiche non crea file. Le aggiunte obbligatorie richiedono
un valore predefinito letterale esplicito nel database, per esempio
`[Column(IsNullable = false, DbType = "int4 NOT NULL DEFAULT 7")]` con valore iniziale
della proprietà pari a `7`. L'inizializzazione null generata e i riempimenti costanti
riconosciuti sono incorporati nel DDL della nuova colonna.

Gli indici semplici sulle colonne, inclusi indici univoci e composti con ordinamento facoltativo
`ASC`/`DESC`, vengono applicati automaticamente quando la tabella viene creata nella
stessa migrazione generata. Aggiungere un indice a una tabella esistente richiede comunque
revisione, anche se la tabella è vuota, così come espressioni degli indici, predicati e altre
opzioni degli indici non supportate.

### Bozze da revisionare

Rinomine, rimozioni, modifiche di colonne esistenti e SQL non riconosciuto producono bozze
marcate con `-- moongate:review-required` e interrompono l'avvio. L'intero batch generato
per quel target resta bloccato, anche al riavvio successivo e nel
runner. Revisiona e modifica l'SQL **non applicato**, rimuovi esplicitamente il marcatore e
riavvia per applicarlo. Non modificare mai una migrazione già applicata. Le rinomine di tabelle ed entità
richiedono migrazioni manuali esplicite: le tabelle non correlate vengono conservate, non
eliminate. Includi nei commit i file SQL con le corrispondenti modifiche alle entità.

### SQL dei plugin durante lo sviluppo

I plugin su disco conservano l'SQL in `plugins/<Bundle>/migrations/auth/` o `world/` e devono
fornire il loro ID stabile in `migrations/manifest.json`. Le entità interne dell'applicazione,
incluse quelle di `Moongate.Server.Ultima`, usano la directory delle migrazioni core.
Per lo sviluppo di plugin, collega o monta la cartella delle migrazioni sorgenti nel
bundle del plugin.

### Lock, errori e baseline

La generazione usa lock cooperativi della directory sorgente e l'advisory lock PostgreSQL
del runner. I file di lock denominati `.moongate-generation.lock` vengono mantenuti; ignorali
nel controllo versione. Un'esecuzione fallita lascia il file SQL in attesa per un nuovo tentativo.
Un avvio annullato attende che il processo figlio termini e non annuncia mai
la disponibilità; PostgreSQL potrebbe ancora completare il rollback, oppure un commit potrebbe
essere già avvenuto. Al nuovo tentativo, la cronologia delle migrazioni determina cosa resta da applicare.
I batch core e dei plugin sono transazionali solo all'interno di un target.

Se un database ha già tabelle delle entità da una sincronizzazione non versionata ma nessuna cronologia SQL
per il loro componente, crea e revisiona una baseline prima di abilitare la
generazione. Un primo file composto solo da ALTER non ricreerebbe quel database altrove.
Partire da un database vuoto evita questo passaggio. Verifica che i file inclusi nei commit
si rieseguano correttamente su un database vuoto prima della distribuzione.

Una directory sorgente personalizzata non viene popolata automaticamente con l'SQL distribuito. Per
usare il catalogo auth fornito invece di generare la tabella degli account, copiane tutti i
file prima del primo avvio. Non sovrascrivere mai file già applicati e non riutilizzarne i
numeri in un catalogo esistente.

La build di sviluppo dai sorgenti copia `mgctl` e le dipendenze in una cartella di output
`mgctl/` separata, e da lì il server esegue `mgctl migrate apply`; una
distribuzione ha `mgctl` accanto al server. Un `mgctl` mancante o una directory delle migrazioni non scrivibile
impedisce l'avvio prima che la generazione possa essere dichiarata riuscita.
