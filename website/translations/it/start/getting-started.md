<!-- translation: {"sourceHash":"f6cafed8fa6a94534245b493e552e15dc8f9cee1b1ce714c9902fbadcfef3720","title":"Primo avvio"} -->

# Avviare un server Moongate

Questa è la sequenza unica di primo avvio per Moongate. Si applica sia installando
il rilascio con [l'installer Linux](installation.md), sia usando
[l'immagine container](docker.md), sia compilando dal sorgente. Moongate è in sviluppo
attivo: i personaggi entrano nel mondo, camminano e si vedono tra loro, e NPC e oggetti
eseguono script Lua, ma combattimento, AI NPC incorporata e gran parte del gameplay
non sono ancora implementati. Vedi [Stato dell'implementazione](implementation-status.md).

L'avvio del server richiede una radice, file client leggibili, il database PostgreSQL
del ruolo attivo e SQL revisionato, e un'istanza Redis privata per lease dei realm
e ticket di handoff monouso. I passaggi sotto preparano queste dipendenze.

## Prima di iniziare

- I tuoi dati client Ultima Online. Moongate non li distribuisce. Il protocollo
  iniziale dei pacchetti è rivolto a ClassicUO 7.x.
- Un server PostgreSQL raggiungibile su cui puoi creare database. Gli esempi di
  questo repository usano PostgreSQL 16.
- Un server Redis 7+ raggiungibile con autenticazione e `maxmemory-policy noeviction`.
  Mantienilo su una rete privata.
- Due porte TCP libere in modalità standalone: login usa normalmente 2593 e game
  2595. Il server ping UDP usa la porta 12000 quando è libera.
- Per compilare dal sorgente: Git e l'SDK .NET 10 selezionato da `global.json`.
  Node.js serve solo per lavorare sul sito della documentazione.

## Dove si trovano i comandi

La sequenza usa tre eseguibili. Ogni metodo di installazione li distribuisce:

| Comando | Rilascio installato | Checkout del sorgente |
| --- | --- | --- |
| Preparare una radice | `mgctl init` (`mgboot` nei rilasci da 0.7 a 0.11) | `dotnet run --project src/Moongate.Server -- --initialize-root` |
| Server | `moongate` | `dotnet run --project src/Moongate.Server -c Release --` |
| Migrazioni | `mgctl migrate` | `dotnet run --project src/Moongate.Ctl -- migrate ... --migrations-directory ./migrations` |

I passaggi sotto usano i nomi installati. Sostituiscili con la forma per il checkout
del sorgente, mantenendo tutto dopo `--`. Per l'immagine container, gli stessi
passaggi vengono eseguiti tramite `--entrypoint`; vedi [Eseguire con Docker](docker.md).

Per un checkout del sorgente, compila prima una volta:

```sh
git clone https://github.com/moongate-community/moongate.git
cd moongate
git switch develop
dotnet build Moongate.slnx -c Release
```

`develop` include lavoro non rilasciato. Per riprodurre un rilascio, esegui il checkout
del suo tag e usa la documentazione pubblicata per quella versione.

Quando la radice è configurata, `scripts/run_server.sh` esegue compilazione e avvio
in un passaggio: pubblica una build Release del server e `mgctl` in `dist/moongate`,
esegue `mgctl init` sulla radice per aggiungere i file distribuiti mancanti, e vi avvia il server:

```sh
scripts/run_server.sh --root-directory "$HOME/moongate"
```

`--skip-build` avvia la build già presente in `dist/moongate`, `--build-only` pubblica
senza avviare, e ogni altra opzione va al server invariata, per esempio
`--pid-file-name game.pid`.

Senza `--root-directory` lo script usa `MOONGATE_ROOT`. Ogni build elimina prima
`dist/moongate`, quindi non conservarvi nulla di tuo. Lo script esporta `MOONGATE_ROOT`
per una configurazione che nomina i percorsi tramite `${MOONGATE_ROOT}`; un
`MOONGATE_ROOT` già impostato resta invariato anche quando `--root-directory` indica
un'altra radice, quindi rimuovilo dall'ambiente o passa lo stesso percorso.

## Primo avvio

1. **Preparare la radice.** Assegna al server una directory propria:

   ```sh
   sudo mkdir -p /srv/moongate && sudo chown "$USER" /srv/moongate
   mgctl init /srv/moongate
   ```

   Questo scrive `config/moongate.toml` con i valori predefiniti, crea `logs/` e
   `plugins/`, copia l'SQL core del rilascio in `migrations/`, i file dei dati dello
   shard in `data/`, i template in `templates/` e gli script di esempio in `scripts/`.
   Non richiede database o file client. [Preparare una radice con mgctl](mgctl.md)
   descrive cosa accade su una radice già esistente.

   Per abilitare anche l'amministrazione facoltativa con un certificato TLS autofirmato, usa:

   ```sh
   mgctl init /srv/moongate --generate-admin-certificate \
     --admin-certificate-hosts "login.example.test"
   ```

   Sostituisci l'host di esempio con il nome DNS o l'indirizzo IP usato dal client
   amministrativo; ometti `--admin-certificate-hosts` per il solo localhost. Questo
   crea `certificates/admin.pfx` e il pubblico `certificates/admin.crt`, e aggiorna
   esplicitamente quattro impostazioni `[admin_api]`. Il binding predefinito resta
   `127.0.0.1:2590`. Vedi [Configurazione del certificato](mgctl.md#generate-an-administration-certificate)
   per fiducia del client, accesso dalla rete privata e riuso di un'identità esistente.

   `mgctl` è distribuito nei rilasci dopo 0.11.0; i rilasci da 0.7 a 0.11 hanno
   invece `mgboot <root>`. Su 0.6.0, avvia invece il server una volta: scrive la
   configurazione ed esce. Non serve altro, perché su 0.6.0 sia il server sia il
   runner delle migrazioni leggono l'SQL core da `/opt/moongate/migrations`, accanto all'eseguibile:

   ```sh
   moongate --root-directory /srv/moongate
   ```

2. **Modificare la configurazione.** Apri `/srv/moongate/config/moongate.toml` e
   imposta percorso client ed entrambe le connessioni database. Mantieni le altre sezioni generate:

   ```toml
   [ultima]
   ultima_path = "/absolute/path/to/your/ultima-client"

   [persistence.accounts]
   connection_string = "$MOONGATE_ACCOUNTS_DATABASE"

   [persistence.realm]
   connection_string = "$MOONGATE_REALM_DATABASE"

   [redis]
   connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
   handoff_secret = "$MOONGATE_HANDOFF_SECRET"
   ```

   Usa un percorso client assoluto; i percorsi relativi si risolvono dalla directory
   di lavoro del processo, non dalla radice. Fornisci i due URI PostgreSQL, la
   stringa di connessione Redis e un segreto di handoff separato comune al cluster
   tramite le variabili d'ambiente referenziate. Il segreto deve avere almeno 32
   byte UTF-8. Leggi le credenziali da Bitwarden; non scriverne i valori in questo
   file. Ogni processo login e game di un deployment richiede lo stesso endpoint
   Redis, password Redis e segreto di handoff. Il [riferimento della configurazione](server-configuration.md)
   elenca ogni impostazione.

   La cifratura del client UO è disabilitata per impostazione predefinita. Per un
   client che invia traffico cifrato, configura `[network.encryption]` usando la
   [guida alla cifratura del client](server-configuration.md#uo-client-encryption)
   prima di avviare il server. Applica lo stesso profilo a processi login e game separati.

3. **Preparare PostgreSQL e Redis.** Crea i database Accounts e World e credenziali
   specifiche per ruolo prima di avviare Moongate. Il server non crea mai database
   o ruoli. Usa un ruolo runtime solo DML e un ruolo schema separato; vedi
   [Separare ruoli DDL e runtime](persistence-operations.md#separate-ddl-and-runtime-roles).

   Prepara Redis con password forte, accesso dalla rete privata e
   `maxmemory-policy noeviction`. Tieni la password separata dal segreto di handoff.
   Per una topologia PostgreSQL e Redis eseguibile, usa l'
   [esempio Docker di login e realm](docker-login-realms.md). Standalone controlla
   entrambi i database; login controlla solo Accounts, e game solo il proprio Realm.
   Tutte e tre le modalità controllano anche Redis all'avvio.

4. **Applicare le migrazioni core.** L'avvio valida la cronologia SQL versionata e
   rifiuta di partire mentre ci sono file in sospeso, quindi applicali prima (o
   imposta [`persistence.auto_apply_migrations`](persistence-migrations.md#apply-at-startup)
   e lascia che l'avvio li applichi):

   ```sh
   mgctl migrate apply --root-directory /srv/moongate --target auth
   mgctl migrate apply --root-directory /srv/moongate --target world
   ```

   `--target auth` usa `[persistence.accounts]`, `--target world` usa
   `[persistence.realm]`. Il runner legge la configurazione della radice e la sua
   directory `migrations/`; `status` al posto di `apply` elenca i file in sospeso
   senza applicarli. Entrambe le destinazioni hanno migrazioni core; `apply`
   riporta ogni file eseguito.

5. **Avviare il server.**

   ```sh
   moongate --root-directory /srv/moongate
   ```

   Passa sempre `--root-directory`. Senza, o con una radice uguale alla directory
   del binario, il server rifiuta l'avvio con codice di uscita 2, perché un
   aggiornamento sostituisce quella directory. Un avvio riuscito registra
   `Postgres connection successful` una volta per database, poi servizi caricati
   ed endpoint associati. Un `scripts/init.lua` mancante è un avviso e avvia un
   ambiente di scripting vuoto; uno script bootstrap presente ma fallito impedisce
   l'avvio. Aggiungi script con [Scrivere script Lua](scripting.md). I comandi
   della console interattiva sono elencati in [Comandi del server](commands.md).

6. **Arrestarlo.** Premi Ctrl+C e lascia terminare l'arresto. Dopo un avvio riuscito,
   l'host esegue un salvataggio finale del mondo prima di chiudere la persistenza
   PostgreSQL. Un avvio fallito o un game loop in errore non possono garantirlo.
   Non terminare il processo mentre lo attende. Il primo salvataggio dopo un avvio
   scrive l'intero mondo e richiede alcuni secondi (da 8 a 15 per quello distribuito);
   il server lo attende fino a 2 minuti dopo Ctrl+C o SIGTERM. Un salvataggio
   interrotto a metà non conferma nulla: ciò che è accaduto dall'ultimo salvataggio
   completato viene perso.

Per eseguire un'altra istanza standalone, assegnale una radice propria, porte
listener login e game distinte, `realm_directory.realm_id` e `server_index` distinti,
e un proprio database realm. Riutilizzare ID e indice predefiniti del realm
sostituisce il lease Redis della prima istanza. Non puntare mai due server alla
stessa radice o allo stesso database realm.

## File e proprietà del processo

Tutti i percorsi gestiti dal server sotto sono relativi a `--root-directory`:

| Percorso | Scopo |
| --- | --- |
| `config/moongate.toml` | Creato se mancante; il normale avvio lo conserva, mentre la configurazione esplicita del certificato aggiorna quattro impostazioni `[admin_api]` |
| `certificates/admin.pfx`, `certificates/admin.crt` | Identità TLS di amministrazione facoltativa di `mgctl`: PFX privato del server e PEM pubblico per la fiducia del client |
| `migrations/auth/`, `migrations/world/` | SQL core copiato da `mgctl init`; i plugin distribuiscono il proprio sotto `plugins/` |
| `data/` | File dei dati dello shard copiati da `mgctl`, letti all'avvio game e standalone; vedi [File dei dati dello shard](data-files.md) |
| `templates/items/`, `loots/`, `mobiles/`, `npc_lists/`, `spawns/`, `decorations/`, `gumps/` | [Template](templates.md) copiati da `mgctl`, caricati all'avvio game e standalone |
| `logs/moongate-*.clef` | Eventi di log JSON strutturati, uno per riga |
| `logs/errors/<id>.md` | Report di ogni eccezione registrata dal server, pronto da incollare in una issue GitHub |
| `plugins/` | Un bundle assembly per directory plugin |
| `scripts/` | Sorgente Lua: `init.lua`, gli [script mobile](scripting/mobile-scripts.md) `mobiles/<script_id>.lua`, gli [script oggetto](scripting/item-scripts.md) `items/<script_id>.lua`, i moduli Lua condivisi in `common/`, gli script gump `gumps/<id>.lua`, e i generati `definitions.lua` e `.luarc.json` |
| `moongate.pid` | Identificatore del processo corrente |
| `moongate.pid.lock` | File di lock usato per escludere un'altra istanza |

La protezione PID viene acquisita prima di caricare la configurazione. Un PID attivo
o un lock già detenuto rifiuta un altro avvio. Un PID obsoleto o malformato viene
sostituito. La protezione controlla se il processo è vivo, non l'identità dell'eseguibile,
quindi anche un PID riutilizzato può rifiutare l'avvio. Esamina quel processo prima
di modificare il file PID. La normale pulizia rimuove il file PID se appartiene
ancora a questo processo; il file `.lock` può restare dopo il rilascio del suo handle.
La sola presenza non significa che il server sia in esecuzione.

I log console mostrano ora, livello, sorgente e messaggio. I log su file ruotano
ogni giorno e a 10 MiB, conservando fino a 30 file. Per le metriche vedi
[Diagnostica](diagnostics.md); per operazioni sullo schema e salvataggi del mondo
vedi [Persistenza PostgreSQL](persistence.md).

## Quando qualcosa fallisce

La console mostra un'eccezione su una riga, messaggio e report, mai lo stack:

```text
12:04:31.552 ERR BankService                  | Opening the bank failed: the bank box is missing - details: /srv/moongate/logs/errors/7f3a9c21aa.md (paste it into a GitHub issue)
```

Il report contiene versione e nome in codice Moongate, sistema, runtime .NET, ora,
sorgente, livello, messaggio e l'intera eccezione, con eccezioni interne e stack,
in Markdown. Aprilo e incollalo in una
[issue GitHub](https://github.com/moongate-community/moongate/issues/new). Il nome
è l'hash dell'eccezione, così la stessa registrata di nuovo, per esempio da un timer,
riusa il report, che descrive la prima volta in cui è stata vista. Dopo 500 report
non ne vengono scritti di nuovi, così eccezioni con messaggi variabili non possono
riempire il disco; elimina i vecchi per fare spazio. In Docker il percorso è quello
interno al container, sotto la radice montata. Un wrapper come `AggregateException`
mostra il messaggio di ciò che contiene. I log `.clef` conservano l'eccezione completa
di ogni evento.

## Problemi comuni all'avvio

| Sintomo | Controllo |
| --- | --- |
| Esce subito dopo aver scritto `config/moongate.toml` | Previsto su una radice nuova: `ultima_path` è ancora `ChangeMe`. Continua con il passaggio 2 |
| Errore del percorso client | Imposta `ultima.ultima_path` su dati client reali e leggibili |
| `Map ... needs map{n}.mul or map{n}LegacyMUL.uop, staidx{n}.mul and statics{n}.mul` | Al client manca quella mappa. Usa un client completo, o rimuovi la mappa da `data/maps.toml` |
| `The Ultima path has neither MultiCollection.uop nor multi.idx and multi.mul` | La directory client è incompleta. Punta `ultima.ultima_path` a un'installazione client completa |
| `tiledata.mul not found in the Ultima path` | La directory client è incompleta o non è una directory client. Punta `ultima.ultima_path` a un'installazione client completa |
| Errore di analisi o validazione TOML | Correggi il campo indicato; i file esistenti non vengono sostituiti silenziosamente |
| Errore `Postgres connection` | Il database non esiste, l'host è errato o il ruolo non può accedere. Dentro un container, `localhost` è il container stesso |
| Variabile di connessione mancante | Esporta le variabili PostgreSQL e Redis referenziate dalle sezioni TOML attive |
| Errore di connessione Redis | Controlla endpoint privato, credenziale, salute Redis e politica `noeviction` |
| Migrazioni in sospeso o modificate | Esegui `status` e `apply` del runner per la destinazione indicata (passaggio 4), o abilita `persistence.auto_apply_migrations`. Non modificare mai un file applicato |
| Necessarie modifiche allo schema PostgreSQL | Un'entità richiede DDL non fornito da alcuna migrazione. Genera e revisiona un file SQL versionato con `--persistence-schema generate`, poi applicalo con il runner a server fermo |
| Errore di binding della porta | Controlla `network.listen_address`, disponibilità delle porte e indirizzi delle interfacce |
| Rilevata un'altra istanza | Controlla PID e processo in esecuzione; usa una radice separata per un altro server |
| `... file ... not found` per un file dati, come `maps.toml` | La radice non ha `data/`. Riesegui `mgctl` sulla radice: aggiunge i file mancanti e mantiene gli altri |
| `InvalidDataException` che indica un file dati | Correggi la voce indicata dal messaggio; vedi le regole di validazione in [File dei dati dello shard](data-files.md) |
| Errore di avvio degli script | Correggi `scripts/init.lua`; controlla nome file e riga dello script nel log |
