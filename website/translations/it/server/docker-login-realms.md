<!-- translation: {"sourceHash":"b1ac6ba171dbed36e375074c3d01ee003795b151ed1ea827570a773ed7dc164d","title":"Docker: login e realm"} -->

# Esempio Docker: un login e due istanze game

L'[esempio Compose](../examples/docker/login-realms/compose.yaml) compila il sorgente
corrente ed esegue un processo login, due processi game, PostgreSQL 16 e Redis 7.4
privato. PostgreSQL contiene database separati Accounts, Realm 1 e Realm 2. Redis
contiene lease dei realm di breve durata e ticket di handoff del login monouso.
Nulla nell'esempio pubblica Redis sull'host.

Un login `0x80` riuscito riceve un elenco `0xA8` filtrato. `0xA0` seleziona un realm;
login invia `0x8C` con indirizzo IPv4, porta e chiave monouso, poi chiude quella
connessione di login. Il client si riconnette alla porta game scelta, invia la
chiave grezza di quattro byte come seed, poi `0x91` con la stessa chiave e credenziali.
Game consuma il ticket da Redis, associa l'account alla sessione locale e risponde
con le funzionalità supportate (`0xB9`) e i personaggi salvati dell'account (`0xA9`)
con le città iniziali. La creazione salva un nuovo personaggio e i suoi oggetti
iniziali; selezionare un personaggio lo porta nel mondo di quel realm.

## Topologia

| Servizio | Database | Endpoint client sull'host | Accesso Redis |
| --- | --- | --- | --- |
| `login` | Solo runtime Accounts | `127.0.0.1:2593` | Lease e ticket di handoff |
| `game-1` | Solo runtime Realm 1 | `127.0.0.1:2595` | Il proprio lease e ticket di handoff |
| `game-2` | Solo runtime Realm 2 | `127.0.0.1:2596` | Il proprio lease e ticket di handoff |
| `redis` | Nessuno | Nessuna porta pubblicata | Solo bridge privato `moongate` |
| Job del profilo schema | Solo la destinazione schema selezionata | Nessuno | Nessuno |

Ogni server ha il proprio volume `/data`. PostgreSQL ha un volume persistente
separato. Redis non ha un volume persistente: ticket e lease sono intenzionalmente
effimeri. Un riavvio Redis invalida entrambi; i processi game in esecuzione
ripubblicano i lease. Un'interruzione Redis impedisce nuovi elenchi di realm e
handoff, mentre le sessioni game già ammesse continuano. Redis usa
`maxmemory-policy noeviction`, così la pressione di memoria rifiuta le scritture
anziché rimuovere silenziosamente ticket attivi.

`game-1` usa lo stage `sample-plugin` del Dockerfile. Il suo plugin registra
`sample_greeter.notes` in Realm 1; i job dello schema esercitano lo stesso plugin
e SQL revisionato. `game-2` usa l'immagine ordinaria. Login non ha credenziali del
database Realm, e i game non hanno credenziali Accounts. Tutti e tre condividono
una credenziale Redis e un segreto di handoff separato; questi server sono quindi
un unico dominio di fiducia privato. Non servono certificati dei peer o ACL per realm.

Il server ping UDP risponde sulla porta host 12000 (`PING_PORT`) solo da `game-1`:
i tre server condividono un indirizzo host, e un solo processo per indirizzo può
possedere la porta, quindi `login` e `game-2` impostano `enable_ping_server = false`.

Dentro i container, login ascolta sulla porta 2593 e ogni game sulla 2595. Compose
mappa game 2 sulla porta host 2596, dichiarata anche da
`realm_directory.advertised_port`. L'indirizzo IPv4 annunciato predefinito è
`127.0.0.1` per un client sull'host Docker. Per client remoti, imposta
`advertised_address` in entrambi i TOML game su un IPv4 dell'host raggiungibile e
pubblica le porte client su quell'indirizzo host. Imposta lo stesso indirizzo in
`game-1-admin.toml` e `game-2-admin.toml` se usi l'override di amministrazione.
L'elenco `0xA8` ha indirizzi IPv4 ma non porte; `0x8C` fornisce la porta del realm selezionato.

## Preparare configurazione e segreti

Usa Docker Engine o Docker Desktop con Compose v2 e la tua directory client Ultima
Online leggibile. Dalla radice del repository:

```sh
cd examples/docker/login-realms
cp .env.example .env
```

Imposta `UO_DATA_PATH` in `.env` su una directory host assoluta. Se necessario,
cambia lì le porte rivolte all'host o i nomi PostgreSQL non segreti. Se cambi
`GAME_1_PORT` o `GAME_2_PORT`, imposta il corrispondente
`realm_directory.advertised_port` in `config/game-1.toml` o `config/game-2.toml`
su quella porta host; aggiorna anche il corrispondente `*-admin.toml` se usi
l'override di amministrazione. Altrimenti il redirect game invia i client alla
vecchia porta. Tieni password e chiavi di handoff fuori da `.env`, TOML e repository.

Crea nove record segreti distinti in Bitwarden: sette password PostgreSQL, una
password Redis esadecimale di 64 caratteri e un segreto di handoff esadecimale
indipendente di almeno 64 caratteri. Esportali nella shell che esegue Compose,
sostituendo i nomi effettivi degli elementi Bitwarden:

```sh
export MOONGATE_POSTGRES_ADMIN_PASSWORD="$(bw get password moongate-postgres-admin)"
export MOONGATE_ACCOUNTS_SCHEMA_PASSWORD="$(bw get password moongate-accounts-schema)"
export MOONGATE_ACCOUNTS_RUNTIME_PASSWORD="$(bw get password moongate-accounts-runtime)"
export MOONGATE_REALM_1_SCHEMA_PASSWORD="$(bw get password moongate-realm-1-schema)"
export MOONGATE_REALM_1_RUNTIME_PASSWORD="$(bw get password moongate-realm-1-runtime)"
export MOONGATE_REALM_2_SCHEMA_PASSWORD="$(bw get password moongate-realm-2-schema)"
export MOONGATE_REALM_2_RUNTIME_PASSWORD="$(bw get password moongate-realm-2-runtime)"
export MOONGATE_REDIS_PASSWORD="$(bw get password moongate-redis)"
export MOONGATE_HANDOFF_SECRET="$(bw get password moongate-handoff)"
```

Compose monta questi valori come segreti. L'entrypoint del server costruisce
`MOONGATE_REDIS_CONNECTION_STRING=redis:6379,password=...` ed esporta
`MOONGATE_HANDOFF_SECRET` solo nell'ambiente del processo runtime. I TOML montati
contengono riferimenti ai nomi delle variabili, non ai valori. Redis legge la propria
password dal proprio mount segreto in un file di configurazione privato in memoria.
Anche gli URI di connessione PostgreSQL vengono assemblati in memoria da segreti
specifici per ruolo. Prima di avviare il server, l'entrypoint esegue `mgctl` sul
volume, aggiungendo ogni [file dei dati dello shard](data-files.md) distribuito
dall'immagine e mancante nel volume, conservando quelli già presenti.

Valida senza stampare il modello Compose espanso, poi compila:

```sh
docker compose config --quiet
docker compose build login game-1 game-2 auth-schema-apply schema-preview schema-apply schema-apply-realm-2 migration-status
```

Input segreti mancanti fanno fallire la validazione indicando il nome. Usa
`--quiet`: renderizzare il modello espanso può esporre valori dall'estensione di
validazione anticipata di Compose.

## Creare i database e revisionare l'SQL

Lo script di inizializzazione PostgreSQL crea sei ruoli e tre database sul primo
volume vuoto. I ruoli schema possiedono DDL; i ruoli runtime hanno solo DML e
l'accesso necessario alle sequenze. L'inizializzazione non viene ripetuta quando
si ricrea un container. Non eseguire mai `docker compose down --volumes` su dati da conservare.

Applica l'SQL Accounts incorporato prima dell'avvio di login:

```sh
docker compose up -d --wait postgres redis
docker compose --profile schema run --rm auth-schema-apply
```

Revisiona e applica l'SQL World per Realm 1, le tabelle core del mondo più quelle
del plugin di esempio, poi le tabelle core del mondo per Realm 2:

```sh
docker compose --profile schema run --rm migration-status
docker compose --profile schema run --rm schema-preview
docker compose --profile schema run --rm schema-apply
docker compose --profile schema run --rm migration-status
docker compose --profile schema run --rm schema-apply-realm-2
```

Ogni server game richiede l'SQL World applicato prima dell'avvio; una migrazione in
sospeso interrompe l'avvio. Lo script di inizializzazione crea lo schema `world`
in entrambi i database Realm e concede a ogni ruolo runtime DML sulle sue tabelle
e uso delle sequenze ID. Un volume inizializzato prima dell'esistenza delle tabelle
core del mondo non ha quei permessi: esegui manualmente le istruzioni
`provision_world_schema` di `postgres/init.sh` per ogni database Realm, o ricrea
il volume usa e getta.

I job dello schema non avviano un client Redis. Usano il segreto PostgreSQL del
ruolo schema selezionato ed eseguono solo la destinazione richiesta. I TOML runtime
impostano `auto_sync_schema = false` e usano l'SQL core incluso in `/app/migrations`.
Per dettagli su scrittura e applicazione dell'SQL dei plugin, vedi
[Generare, revisionare e applicare](persistence-migrations.md#generate-review-and-apply).

## Avviare, osservare e arrestare

```sh
docker compose up -d login game-1 game-2
docker compose ps
docker compose logs --tail 100 login game-1 game-2
```

I tre servizi runtime attendono la salute di PostgreSQL e Redis. L'avvio controlla
il database del ruolo e la connessione Redis. I processi game pubblicano
`moongate:realm:1` e `moongate:realm:2`, ciascuno con lease di 15 secondi rinnovato
ogni cinque secondi. Per esaminarli senza mostrare la password Redis:

```sh
docker compose exec -T redis sh -ec 'export REDISCLI_AUTH="$(cat /run/secrets/redis-password)"; redis-cli --raw HGETALL moongate:realm:1; redis-cli TTL moongate:realm:1'
```

Le chiavi dovrebbero avere TTL positivi. Non usare `FLUSHDB`: Redis può contenere
altro stato Moongate di breve durata. Lo [script smoke](../examples/docker/login-realms/smoke.sh)
dell'esempio controlla entrambi i lease oltre un intero intervallo di lease.

Per creare un account di test, collegati alla console login con
`docker compose attach login`, premi `*` per sbloccare i comandi, poi esegui
`account create <username> <password> [level]`. Salva la password in Bitwarden.
Scollegati con la sequenza Docker `Ctrl-P`, `Ctrl-Q` così login resta in esecuzione.
I container game non registrano questo comando.

Ogni volume `/data` contiene PID, log, script e plugin di quel processo; non
condividere una radice tra server. Arresta o ricrea un game indipendentemente:

```sh
docker compose stop game-1
docker compose up -d --no-build --force-recreate game-1
```

`docker compose down` rimuove container e bridge privato ma conserva i volumi
nominati. I salvataggi del mondo non sono backup PostgreSQL; imposta separatamente
la politica di backup del database.

## Smoke test usa e getta

Dalla radice del repository:

```sh
MOONGATE_SMOKE_UO_PATH=/path/to/ultima-client sh examples/docker/login-realms/smoke.sh
```

I server game caricano `tiledata.mul`, mappe e multi all'avvio, quindi lo script
richiede una vera directory client; si arresta con codice di uscita 2 quando
`MOONGATE_SMOKE_UO_PATH` non contiene `tiledata.mul`. Lo script crea un progetto
Compose univoco con volumi temporanei e credenziali sintetiche solo di processo.
Compila immagini locali, controlla ruoli PostgreSQL e comportamento dello schema,
applica l'SQL World a entrambi i realm e verifica che i ruoli runtime possano
scrivere oggetti del mondo, avvia tutti e tre gli host, verifica TTL positivi di
entrambi i lease Redis dopo un intero intervallo, e controlla l'arresto corretto.
Handoff UO `0x80`→`0x91` e riutilizzo dei ticket sono coperti dai test di integrazione
basati su Redis nella suite .NET.

## Risoluzione dei problemi

- **Compose indica una variabile mancante:** esporta il valore nominato da Bitwarden
  nella stessa shell, o imposta `UO_DATA_PATH` in `.env`.
- **Il controllo di salute Redis fallisce:** esamina `docker compose logs redis`,
  il mount segreto e l'indirizzo di rete privata del container. Redis non ha una
  porta host da verificare.
- **Login non ha realm:** esamina `docker compose logs game-1 game-2` e chiavi
  `moongate:realm:*` e TTL. Verifica che tutti e tre i container runtime condividano
  lo stesso servizio Redis e segreto di handoff.
- **L'avvio segnala SQL in sospeso:** esegui il job del profilo schema corrispondente
  mentre il runtime interessato è fermo.
- **Il client non entra nel game selezionato:** conferma che `advertised_address`
  sia raggiungibile dal client, la porta host corrisponda ad `advertised_port`,
  e la chiave di redirect non sia scaduta o già usata.
- **Il container esce:** esamina `docker compose ps -a` e i log. Correggi l'errore
  database, Redis o configurazione prima di ricrearlo.

## Endpoint di amministrazione privato

La porta 2590 è riservata all'amministrazione gRPC facoltativa e resta disabilitata
nella configurazione predefinita immagine/Compose. Usa TLS del server sulla rete
privata; non servono mTLS o mapping di porte pubbliche. Vedi la
[guida all'amministrazione](admin-api.md) e la
[configurazione Compose facoltativa](../examples/docker/login-realms/README.md#optional-administration-api).
