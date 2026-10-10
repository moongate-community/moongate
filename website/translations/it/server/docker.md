<!-- translation: {"sourceHash":"d63b8fb127e770b8ace24e5ecb709403d9c59d2433b03927019424e6b0ebc2b0","title":"Eseguire con Docker"} -->

# Eseguire con Docker

Moongate pubblica immagini Linux su [GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
Usa la documentazione della versione in esecuzione; il [changelog](../CHANGELOG.md)
identifica i rilasci pubblicati. Per eseguire il sorgente corrente, compila l'immagine localmente:

```sh
docker build -f src/Moongate.Server/Dockerfile -t moongate:local .
```

L'immagine viene eseguita come utente non root con `MOONGATE_ROOT=/data`. Monta lì
un volume persistente scrivibile e monta in sola lettura i tuoi file client Ultima
Online; i file client non sono distribuiti con Moongate. Include [`mgctl`](mgctl.md),
che prepara la radice e applica le migrazioni,
l'SQL core, i [file dei dati dello shard](data-files.md), i [template](templates.md)
e gli [script](scripting/shipped-scripts.md) di esempio. La destinazione di build
`sample-plugin` aggiunge il bundle del plugin di esempio.

## Cache della build

Il Dockerfile ripristina le dipendenze prima di copiare i sorgenti, poi pubblica ogni
eseguibile incluso con `--no-restore`. Ripristino e pubblicazione usano la stessa
configurazione, architettura di destinazione e impostazione self-contained.
Non c'è una compilazione separata del server prima della pubblicazione.

Il contesto di build include sorgenti, migrazioni core, file dei dati dello shard,
template e script di esempio (`moongate_root/data`, `templates`, `scripts`, senza
`definitions.lua` e `.luarc.json` generati), impostazioni di build, licenze e plugin
di esempio. Documentazione, test, sito, directory locali `bin`/`obj` e file di ambiente
sono esclusi. Il sorgente del plugin di esempio viene copiato solo nella propria
destinazione di build. Quando aggiungi un nuovo input all'immagine, aggiorna insieme
`.dockerignore` e le istruzioni `COPY` pertinenti.

I flussi di sviluppo e rilascio esportano i layer intermedi su GHCR sotto
`buildcache-develop` e `buildcache-release`. Ogni flusso legge entrambe le cache ma
scrive solo il proprio tag. Questi tag contengono cache di build, non immagini
server eseguibili. I pacchetti NuGet restano nel layer di ripristino affinché nuovi
builder CI possano recuperarli dalla cache esterna. Una prima build senza cache
funziona comunque normalmente.

Per una build locale che legga anche la cache di sviluppo:

```sh
docker buildx build --load \
  --cache-from type=registry,ref=ghcr.io/moongate-community/moongate:buildcache-develop \
  -f src/Moongate.Server/Dockerfile -t moongate:local .
```

Ripeti la build con `--progress=plain` per esaminare i passaggi in cache. Modifiche
alla sola documentazione dovrebbero mantenere in cache i layer di ripristino e
pubblicazione. Usa `--pull` per verificare immagini base .NET aggiornate, o
`--no-cache` per ricompilare tutti i passaggi. Le immagini pubblicate restano Linux amd64.

## Esempio Compose consigliato

L'[esempio con login e due istanze game](docker-login-realms.md) è un deployment
completo compilato dal sorgente con:

- Un processo login sulla porta TCP UO 2593 e due processi game sulle porte host 2595 e 2596.
- Database PostgreSQL Accounts, Realm 1 e Realm 2 separati e ruoli schema/runtime.
- Un servizio Redis privato per lease attivi dei realm e ticket di handoff del login monouso.
- Segreti Compose da Bitwarden, job schema, volumi persistenti server/PostgreSQL e uno smoke test usa e getta.

Segui quella guida per `.env`, esportazione dei segreti, build, applicazione SQL e
comandi di avvio esatti. Redis non viene pubblicato sull'host. Non ci sono listener
API interni o scambio di certificati in questa topologia.

## Container standalone

`mode = "standalone"` esegue entrambi i ruoli UO in un processo, con listener login
e game separati. Pubblica entrambe le porte configurate. Standalone richiede anche
database PostgreSQL Accounts e Realm raggiungibili e il servizio Redis condiviso;
annuncia il proprio realm locale tramite Redis. Il TOML della radice include:

```toml
mode = "standalone"

[network]
login_port = 2593
game_port = 2595

[ultima]
ultima_path = "/uo"

[persistence.accounts]
connection_string = "$MOONGATE_ACCOUNTS_DATABASE"

[persistence.realm]
connection_string = "$MOONGATE_REALM_DATABASE"

[redis]
connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
handoff_secret = "$MOONGATE_HANDOFF_SECRET"
```

Fornisci le quattro variabili referenziate da un gestore dei segreti nell'ambiente
del container. Le variabili PostgreSQL sono URI `postgres://` per database specifici
per ruolo. La variabile Redis è una stringa di connessione StackExchange.Redis come
`redis:6379,password=<secret>` su una rete Docker privata; il segreto di handoff è
un valore diverso. Non committare nessuno dei valori in TOML, Compose o `.env`.
Il [riferimento della configurazione](server-configuration.md) copre le altre impostazioni.

Prepara una radice prima dell'avvio (questo copia anche file dei dati dello shard,
template e script di esempio in `/data/data`, `/data/templates` e `/data/scripts`),
poi applica l'SQL Auth e World ai rispettivi database:

```sh
docker volume create moongate-data
docker run --rm --entrypoint /app/mgctl -v moongate-data:/data moongate:local init /data
```

Per TLS amministrativo facoltativo in questa radice, aggiungi
`--generate-admin-certificate` dopo `/data`, con `--admin-certificate-hosts` che
indica DNS/IP usato dai client. Vedi [Configurazione certificati di mgctl](mgctl.md#generate-an-administration-certificate)
per file generati e fiducia del client. Il binding predefinito resta loopback;
configura un'interfaccia privata prima di connetterti da un altro container.
L'override Compose multiprocesso usa invece TOML montati e certificati forniti
 dall'operatore, come descritto nel [README dell'esempio](../examples/docker/login-realms/README.md#optional-administration-api).

Monta lo stesso volume per server e runner delle migrazioni. Arresta il runtime
interessato prima di applicare nuovo SQL revisionato. Per destinazioni e output del
runner, vedi [Generare, revisionare e applicare](persistence-migrations.md#generate-review-and-apply).
Database, connessione Redis o migrazione obbligatoria mancanti fanno fallire l'avvio;
il server non crea database né applica automaticamente SQL non revisionato.

## Porte, storage e aggiornamenti

L'immagine corrente dichiara porte client UO 2593 e 2595 e porta UDP 12000 per il
server ping. Pubblica `12000/udp` solo per il processo il cui indirizzo viene
contattato dai ping dei client; due processi dietro un indirizzo host non possono
usarla entrambi. `EXPOSE` non pubblica una porta host; configura `ports` per login
e ogni listener game che i client devono raggiungere. Mantieni Redis e PostgreSQL
su una rete privata. Ogni processo Moongate in esecuzione richiede un volume `/data`
proprio; ogni realm un database Realm proprio. Non condividere una radice o un
database Realm tra processi game in esecuzione.

Dopo aver scaricato un'immagine più recente, esegui di nuovo `mgctl` sul volume come
sopra: aggiunge file dati e SQL core introdotti dal nuovo rilascio e conserva i
file già nella radice. L'esempio Compose lo fa nel proprio entrypoint a ogni avvio.

`docker compose down` conserva i volumi nominati. Aggiungere `--volumes` elimina
radici dei server e dati PostgreSQL; usalo solo per un ambiente usa e getta.
I salvataggi del mondo non sono backup PostgreSQL. Arresta normalmente i servizi
così il salvataggio finale può completarsi: Docker termina forzatamente un container
10 secondi dopo avergli chiesto di fermarsi, e il primo salvataggio dopo un avvio
richiede 8–15 secondi per il mondo distribuito, quindi concedi più tempo con
`stop_grace_period: 2m` nel file Compose o `docker stop -t 120`.

Per aggiornare un'istanza, leggi il changelog del rilascio di destinazione, arrestala,
segui la politica di backup del database, cambia il tag dell'immagine e riavvia con
gli stessi volumi. Se l'avvio segnala migrazioni in sospeso, arresta il server
interessato e applica l'SQL revisionato prima di riavviarlo.

Con `persistence.auto_apply_migrations = true` in `config/moongate.toml` il server
esegue entrambi i passaggi all'avvio: aggiunge l'SQL core della nuova immagine a
`/data/migrations` e applica ciò che è in sospeso, quindi un aggiornamento consiste
in nuovo tag immagine e riavvio. Vedi [Applicare all'avvio](persistence-migrations.md#apply-at-startup)
per requisiti e ciò che ancora lo interrompe; file dati, template e script aggiunti
da un rilascio provengono comunque da `mgctl init`.

## Conversione dei contenuti UOX3

L'immagine non converte contenuti: i convertitori sono uno strumento Python, `moongate-convert`, che si lancia da un checkout del repository (servono [uv](https://docs.astral.sh/uv/) e Python 3.13 o successivo, non serve .NET). Eseguilo sull'host, con i dati UOX3 in una cartella leggibile, e scrivi l'output dove il volume `/data` del container è montato, oppure copialo dopo:

```sh
cd tools/convert
uv run moongate-convert uox \
  --source /path/to/uox3/data/dfndata/items \
  --destination /path/to/templates/items --loot-destination /path/to/templates/loots
```

Vedi [Migrare da UOX3](uox3-migration.md) per comportamento e limiti attuali del convertitore.

## Risoluzione dei problemi

- **La connessione database fallisce:** `localhost` dentro un container significa
  quel container. Usa il nome del servizio raggiungibile o l'indirizzo di rete
  privata e conferma che il ruolo abbia accesso al database di destinazione.
- **La connessione Redis fallisce:** verifica servizio privato, salute, credenziale
  condivisa e politica `noeviction`. Un riavvio Redis cancella ticket e lease;
  i game ripubblicano i lease dopo la riconnessione.
- **Realm assente dal login:** controlla log game e TTL dei lease Redis come nella
  [guida Compose](docker-login-realms.md#start-observe-and-stop).
- **Il redirect client non può connettersi:** IPv4 e porta annunciati dal game devono
  essere raggiungibili dal client e corrispondere al mapping host pubblicato.
- **Dati client mancanti o radice non scrivibile:** controlla mount `/uo` in sola
  lettura e proprietà di `/data` per l'utente non root dell'immagine.

Vedi [Primo avvio](getting-started.md), [Configurazione](server-configuration.md) e
[Diagnostica](diagnostics.md) per le operazioni a livello server.

## Kubernetes

Il [chart Helm](kubernetes.md) esegue la stessa topologia con login e realm su Kubernetes ed è pubblicato su GitHub Container Registry a ogni rilascio.

## Endpoint di amministrazione privato

La porta 2590 è riservata all'amministrazione gRPC facoltativa e resta disabilitata
nella configurazione predefinita immagine/Compose. Usa TLS del server sulla rete
privata; non servono mTLS o mapping di porte pubbliche. Vedi la
[guida all'amministrazione](admin-api.md) e la
[configurazione Compose facoltativa](../examples/docker/login-realms/README.md#optional-administration-api).
