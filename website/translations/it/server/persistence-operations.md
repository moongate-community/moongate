<!-- translation: {"sourceHash":"73a0d796ee650716e72f991ad35f223509a0e3e402639cb3c4d31bfd0209bcea","title":"Gestire PostgreSQL"} -->

# Gestire PostgreSQL: connessioni, ruoli e salvataggi

Questa pagina è per l'operatore: come il server si connette ai due database, quali
ruoli richiede, cosa è un salvataggio del mondo e cosa non è. La mappatura delle entità e l'accesso
ai dati sono in [Entità e accesso ai dati](persistence.md); le modifiche allo schema sono in
[Migrazioni](persistence-migrations.md). I due database e i loro nomi di destinazione
sono elencati in [Due database, quattro nomi ciascuno](persistence.md#two-databases-four-names-each).

## Connessioni

Ogni database ha una `connection_string`. Contiene un URI PostgreSQL o un riferimento
a una variabile d'ambiente. Una configurazione appena generata usa:

```toml
[persistence]
auto_sync_schema = false

[persistence.accounts]
connection_string = "postgres://moongate:moongate@localhost:5432/auth"

[persistence.realm]
connection_string = "postgres://moongate:moongate@localhost:5432/world"
```

Questi valori predefiniti sono per lo sviluppo locale. I file TOML esistenti non vengono mai riscritti.

Per una distribuzione, sostituisci il valore Accounts con `"$MOONGATE_ACCOUNTS_DATABASE"`
e il valore Realm con `"${MOONGATE_REALM_DATABASE}"`, e imposta ogni variabile a un
URI come `postgres://runtime:password@db:5432/moongate_realm?sslmode=require`
tramite il gestore dei servizi o il provider di segreti. Esportare semplicemente quelle variabili
non sostituisce un URI letterale nel file. I riferimenti `$NAME` e `${NAME}`
si espandono una volta, senza trattare il risultato come percorso del filesystem né espandere nuovamente il
valore sostituito. Una variabile non definita fa fallire l'inizializzazione di ogni connessione
configurata, compreso un database senza entità o file SQL.

Regole degli URI:

- Sono accettati sia `postgres://` sia `postgresql://`. La porta predefinita è 5432.
  Racchiudi tra parentesi quadre gli host IPv6: `postgres://runtime@[::1]/moongate_realm`.
- Codifica in forma percentuale i caratteri riservati nei nomi utente, nelle password e nei nomi di database:
  `@` diventa `%40`, `#` diventa `%23`, un `$` letterale diventa `%24`. I valori vengono
  decodificati una volta; un `+` rimane un più letterale.
- Le opzioni di query includono `sslmode`, `connect_timeout`, `application_name`,
  `search_path` e i nomi delle opzioni Npgsql. Opzioni o modalità SSL non supportate fanno fallire
  la validazione.
- Sono accettate anche stringhe native Npgsql `key=value;`, per l'uso diretto della libreria.

## Controlli all'avvio

Il normale avvio apre ogni database ed esegue `SELECT 1`, indipendentemente dalla modalità del server
o dalle entità registrate. Un successo registra `Postgres connection successful` con la
destinazione e l'endpoint, senza credenziali. Un errore di connessione o ping interrompe
l'avvio prima che partano gli altri servizi. Moongate non crea database.

Dopo i ping, l'avvio valida la cronologia dell'SQL versionato di ogni database
attivo, comprese le migrazioni dei soli dati, e poi confronta le mappature delle entità registrate
con lo schema. File applicati in sospeso, modificati o mancanti, oppure un'entità
che richiede DDL non fornito da alcuna migrazione, impediscono l'avvio dei servizi. Applica
l'SQL revisionato con il runner delle migrazioni mentre il server è fermo; vedi
[Migrazioni](persistence-migrations.md#generate-review-and-apply).

Un ping di connettività non attiva le mappature delle entità né i controlli delle migrazioni per un
database che non ha nulla di registrato. Esegui `status --target ...` del runner
durante la distribuzione per controllare la cronologia di un database diventato vuoto. Un'entità
registrata attiva sempre il proprio database e i relativi controlli delle migrazioni.

`auto_sync_schema` è false per impostazione predefinita e dovrebbe rimanere false. Quando è true, l'avvio
applica direttamente modifiche allo schema non versionate e non registra la cronologia delle migrazioni. Usalo
solo per un database di sviluppo eliminabile.

## Separare i ruoli DDL e runtime

Assegna ai processi ordinari una connessione di runtime. Un'attività di schema eseguita una volta usa la stessa
impostazione `connection_string` con un URI del ruolo di schema per lo stesso database. Il suo
TOML separato può riferirsi a una variabile d'ambiente riservata allo schema:

```toml
[persistence.realm]
connection_string = "$MOONGATE_REALM_SCHEMA_DATABASE"
```

Solo quel processo amministrativo riceve la credenziale di schema; gli host ordinari
ricevono la credenziale di runtime. Non esiste un'impostazione separata di connessione allo schema nella
configurazione del server.

Il ruolo di schema possiede gli schemi delle entità ed esegue DDL. Il ruolo di runtime richiede
`CONNECT` sul database, `USAGE` sullo schema delle entità, `SELECT`, `INSERT`, `UPDATE` e
`DELETE` sulle tabelle delle entità, e `USAGE` e `SELECT` sulle sequenze delle entità (`SELECT` permette a un
[backup](#database-backups) di leggere i loro valori). Configura privilegi
predefiniti per le future tabelle dei plugin. Per `moongate_migrations`, il ruolo di runtime
richiede solo `USAGE` sullo schema e `SELECT` su `moongate_migrations.history`; non concedergli mai
scritture della cronologia. Imposta queste concessioni come proprietario dopo la prima applicazione, oppure
prepara in anticipo lo schema e i privilegi `SELECT` predefiniti. Moongate
non concede privilegi silenziosamente. L'
[esempio con un accesso e due istanze di gioco](docker-login-realms.md) mostra
ruoli, concessioni e attività di schema separati.

## Salvataggi del mondo

Un salvataggio del mondo acquisisce lo stato di proprietà del game loop e ne esegue l'upsert, una transazione
per database attivo. È il modo in cui il server salva ciò che è accaduto dall'ultimo
salvataggio; non elimina le righe uscite dallo snapshot.

I salvataggi periodici avvengono ogni 300 secondi per impostazione predefinita:

```toml
[world_save]
enabled = true
interval_seconds = 300
```

### Un salvataggio non ferma il gioco

I giocatori continuano a camminare, parlare e combattere mentre il mondo viene salvato: nessun "il mondo viene salvato,
attendi", nessuno schermo bloccato. Un salvataggio ha tre passaggi e solo il primo occupa il game
loop:

1. **Acquisizione, sul loop.** Ogni entità attiva viene copiata in uno snapshot scollegato, in memoria. Su un
   mondo di sviluppo di 173.000 entità (144.000 oggetti, 29.000 NPC) richiede circa 0,1 secondi,
   una volta ogni cinque minuti.
2. **Impronta, in background.** Ogni snapshot riceve un'impronta, confrontata con quella
   dell'ultimo salvataggio confermato; circa un secondo per lo stesso mondo, mentre il gioco continua.
3. **Scrittura, in background.** Solo le entità cambiate vengono scritte in PostgreSQL, in una
   transazione per database. Con il mondo tranquillo sono poche centinaia di righe invece di 173.000;
   ogni dodicesimo salvataggio (una volta all'ora) scrive di nuovo tutto, circa 14 secondi su quel mondo, sempre
   in background.

Un salvataggio che fallisce viene annullato integralmente e ritentato per intero dal successivo; vedi
[Snapshot del mondo attivo](persistence.md#live-world-snapshots) per le regole.

`enabled = false` disabilita la richiesta periodica lasciando disponibili i salvataggi espliciti e
finali. Le richieste contemporanee si uniscono al salvataggio attivo; l'annullamento interrompe solo
l'attesa di quel chiamante. Un arresto idoneo esegue un'acquisizione finale dopo lo svuotamento del lavoro
accettato, motivo per cui Ctrl+C deve poter terminare. Un avvio fallito o un
game loop in errore non possono garantire quel salvataggio finale, e un errore critico di
persistenza blocca i salvataggi successivi fino al riavvio dell'host; vedi
[Snapshot del mondo attivo](persistence.md#live-world-snapshots) per quella regola.

## Backup dei database

Un salvataggio del mondo è uno snapshot dell'applicazione, non un backup del database. Per i backup Moongate scrive esportazioni
SQL dei database di proprietà di un processo:

| Modalità del server | File |
| --- | --- |
| `standalone` | `auth_<date>.sql` e `world_<date>.sql` |
| `login` | `auth_<date>.sql` |
| `game` | `world_<date>.sql` |

`<date>` è l'ora UTC del backup, `yyyyMMdd_HHmmss`; un secondo backup nello stesso secondo riceve
`_2` dopo la data. I file vanno in `backups` sotto la
directory radice del server e vengono conservati solo i cinque più recenti di ogni database. Tutto questo è impostato in
[`[sql_backup]`](server-configuration.md).

Un backup viene eseguito secondo la pianificazione quando `sql_backup.enabled` è true e subito con il
comando [`sql_backup`](commands/sql_backup.md).

### Cosa fa un backup

1. Con il ruolo game, salva il mondo e attende il completamento del salvataggio. Se il salvataggio fallisce, non viene
   scritto alcun file.
2. Esporta ogni database in una transazione di sola lettura, quindi un file è un'immagine coerente anche
   quando un altro salvataggio viene confermato nel frattempo.
3. Scrive il file come `.tmp` e lo rinomina quando completo: un file `.sql` è sempre intero.
4. Elimina i file più vecchi di quel database oltre `keep`.

L'esportazione viene eseguita fuori dal game loop. Solo il salvataggio del mondo precedente coinvolge il loop.

Un database che fallisce viene registrato e non ferma l'altro; i suoi vecchi file vengono lasciati intatti.
I file nella directory che non sono stati scritti da Moongate non vengono mai eliminati.

### Cosa contiene un file

Solo dati: un `TRUNCATE` delle tabelle, un blocco `COPY ... FROM stdin` per ogni tabella e il
valore attuale di ogni sequenza, tutto in un'unica transazione. Il file non contiene schema. Ogni tabella che il
ruolo di runtime può leggere viene inclusa, anche le tabelle dei plugin. Una tabella che non può leggere viene saltata
e indicata in un commento in cima al file e in un avviso del log. Questo comprende una tabella in uno schema
che il ruolo non può usare. Quando una tabella saltata fa riferimento a una esportata, il file e il log
avvisano anche che la tabella deve essere svuotata prima del ripristino, altrimenti PostgreSQL rifiuta il `TRUNCATE`.
Una sequenza che il ruolo non può leggere (`USAGE` senza `SELECT`) viene esclusa allo stesso modo, indicata nel
commento e in un avviso del log; dopo un ripristino conserva il valore assegnato dalle migrazioni, quindi
concedi `SELECT` sulle sequenze.

La cronologia delle migrazioni (`moongate_migrations`) non è mai nel file: descrive lo schema del
database in cui si trova e la procedura di ripristino la ricostruisce.

Un file di `auth` contiene gli hash delle password degli account. Mantieni la directory dei backup leggibile solo
dall'utente che esegue il server.

### Ripristino

Ripristina con la versione di Moongate che ha scritto il backup.

1. Arresta il server.
2. Crea un database vuoto e applica le migrazioni di quella versione con [`mgctl`](mgctl.md).
3. Esegui il file con un ruolo che possiede le tabelle:

   ```bash
   psql "postgres://moongate:<password>@localhost:5432/world" -v ON_ERROR_STOP=1 -f world_20261002_113000.sql
   ```

4. Avvia il server. Per passare a una versione più recente, aggiorna dopo il ripristino: le nuove migrazioni
   vengono poi eseguite sui dati ripristinati come in qualsiasi aggiornamento.

Il file viene eseguito in un'unica transazione: se fallisce, il database rimane com'era. Ripristina `auth` e
`world` dalla stessa esecuzione di backup, così personaggi e account corrispondono.

### Cosa non fa

I file non sono compressi, non vengono copiati altrove e non vengono ripristinati dal server. Moongate non ha
migrazioni inverse automatiche, quindi un file non si carica su uno schema più vecchio e uno schema più recente
può avere colonne che il file non può riempire. Per il recupero a un istante preciso o copie fuori sede, usa gli strumenti propri di PostgreSQL
insieme a questo.

## Ritiro degli allegati delle lettere

Le ricompense delle lettere sono fissate nella proprietà stringa `book.attachments` dell'oggetto. La
tabella `world.book_attachment_claims` contiene una ricevuta di solo inserimento con chiave il
seriale della lettera; non ha una sorgente attiva per il salvataggio del mondo. La ricevuta e le righe delle ricompense
appena materializzate vengono confermate con la lettera salvata e i contenitori genitori richiesti in una
transazione del mondo. L'inserimento della ricevuta blocca e verifica la lettera referenziata.
La pulizia delle eliminazioni viene eseguita al completamento della transazione: distruggere la lettera rimuove
la sua ricevuta, mentre un salvataggio che elimina temporaneamente e ripristina una lettera spostata
la conserva. Le ricevute non diventano mai snapshot attivi del salvataggio del mondo.

I ritiri condividono la barriera delle operazioni di persistenza con i salvataggi del mondo. Riservano brevemente
l'inventario di chi ritira, eseguono il lavoro sul database fuori dal game loop e applicano
gli oggetti consegnati sul loop prima di rilasciare la prenotazione. La disconnessione attende
la conclusione prima di acquisire l'inventario e l'arresto completa i ritiri prima del
salvataggio finale. I ritiri non ritentano la transazione dopo un'eccezione: una
nuova lettura riconcilia la ricevuta esatta e gli snapshot attesi degli oggetti.

Un rollback confermato lascia disponibile il diritto al ritiro. Un gruppo confermato corrispondente
viene applicato una sola volta. Un risultato incerto o incoerente, una riconciliazione fallita
o un'applicazione fallita sul loop dopo il commit mantengono escluso l'inventario e mettono in errore la
barriera condivisa; l'acquisizione finale deve fallire invece di salvare un inventario obsoleto sopra
ricompense confermate. Ispeziona il log del server e le righe persistenti di ricevute/oggetti prima del
recupero. Non rigenerare mai un contenuto già emesso da una sorgente TOML modificata.

Applica `0023_book_attachment_claims.sql` tramite il normale flusso di migrazione a server fermo
prima di eseguire il server aggiornato.
