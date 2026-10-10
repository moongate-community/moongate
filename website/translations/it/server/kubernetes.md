<!-- translation: {"sourceHash":"3cfd3422f0a711c75f16a513b88f3be01778112ba75e8e8a950d3a87b4423252","title":"Eseguire su Kubernetes"} -->

# Eseguire su Kubernetes

Il chart Helm in [`deploy/helm/moongate`](../deploy/helm/moongate) esegue un login e un numero qualsiasi di realm, la stessa topologia dell'[esempio Docker con login e realm](docker-login-realms.md). Ogni rilascio lo pubblica come artefatto OCI accanto all'immagine:

```sh
helm install moongate oci://ghcr.io/moongate-community/charts/moongate --version <release> -n moongate --create-namespace -f values.yaml
```

Per usare il chart da un checkout, metti `deploy/helm/moongate` al posto dell'indirizzo `oci://`.

## Prima di installare

- Kubernetes 1.26 o successivo e un modo per raggiungere i Service dai giocatori: un `LoadBalancer` (il predefinito del chart) oppure `NodePort`.
- PostgreSQL 16 e Redis 7.4 raggiungibili dal cluster, oppure le copie di prova incluse (vedi [Trial PostgreSQL and Redis](#trial-postgresql-and-redis)).
- Una PersistentVolumeClaim con i file del client di Ultima Online. Non sono distribuiti con Moongate. Il chart monta la claim in sola lettura su `/uo` in ogni pod di Moongate, quindi i pod su nodi diversi richiedono un volume `ReadWriteMany` (oppure un `nodeSelector` che li tenga insieme).

## Valori minimi

```yaml
uo:
  existingClaim: uo-files
secrets:
  existingSecret: moongate-secrets
realms:
  - id: realm-1
    name: "Moongate Realm 1"
    serverIndex: 1
    advertisedAddress: 192.168.255.30
```

`helm install` rifiuta valori senza `uo.existingClaim`, un realm senza `advertisedAddress` (un indirizzo IPv4), due realm con lo stesso `id` o `serverIndex`, un `id` che non è un'etichetta DNS minuscola e `ping` attivo su più di un realm.

## Cosa crea

| Risorsa | Note |
| --- | --- |
| Deployment e Service `<release>-moongate-login` | Una replica, TCP 2593. `mode = "login"`. |
| StatefulSet, ConfigMap e Service `<release>-moongate-<id>` per ogni realm | Una replica, TCP 2595, un proprio volume `/data` e un proprio database. `mode = "game"`. Un realm non si scala: un processo possiede una radice e un database Realm. |
| Secret | Solo con le dipendenze di prova; altrimenti il Secret è tuo. |
| PostgreSQL, Redis | Solo con `postgresql.enabled` e `redis.enabled`. |
| NetworkPolicy | Solo con `networkPolicy.enabled`: PostgreSQL e Redis di prova accettano solo i pod di questa release. |

Ogni pod gira con l'utente non root dell'immagine, senza capability e senza token del service account. Prima del server partono due init container: `mgctl init /data` aggiunge i file dati, i template e gli script che un rilascio distribuisce (il volume tiene quelli che ha già), poi `mgctl migrate apply` applica l'SQL del database del pod (`auth` per il login, `world` per un realm) con le credenziali di schema. Imposta `schema.enabled: false` per applicare l'SQL da solo con [`mgctl migrate`](persistence-migrations.md). Ogni pod possiede il proprio database, quindi due applicazioni non girano mai insieme.

## Secret

Il chart non scrive mai una password in una ConfigMap o nei file TOML: i file contengono riferimenti `$MOONGATE_*` e i pod li leggono da un solo Secret, `secrets.existingSecret`. Crealo con queste chiavi:

| Chiave | Contenuto |
| --- | --- |
| `accounts-runtime-url` | URI `postgres://` del database Accounts, ruolo runtime |
| `accounts-schema-url` | Lo stesso database, ruolo di schema (lo legge solo l'init container) |
| `<id>-runtime-url`, `<id>-schema-url` | Lo stesso per ogni realm, per esempio `realm-1-runtime-url` |
| `redis-connection-string` | Stringa StackExchange.Redis, per esempio `redis.example:6379,password=<secret>` |
| `handoff-secret` | Almeno 64 caratteri esadecimali, condivisi dal login e da ogni realm |

```sh
kubectl create secret generic moongate-secrets -n moongate \
  --from-literal=accounts-runtime-url="$ACCOUNTS_RUNTIME_URL" \
  --from-literal=accounts-schema-url="$ACCOUNTS_SCHEMA_URL" \
  --from-literal=realm-1-runtime-url="$REALM_1_RUNTIME_URL" \
  --from-literal=realm-1-schema-url="$REALM_1_SCHEMA_URL" \
  --from-literal=redis-connection-string="$REDIS_CONNECTION_STRING" \
  --from-literal=handoff-secret="$HANDOFF_SECRET"
```

Prendi i valori dal tuo gestore di segreti; non scriverli in un file di valori. Ruoli e database sono descritti in [Migrazioni della persistenza](persistence-migrations.md); l'[`init.sh`](../examples/docker/login-realms/postgres/init.sh) dell'esempio Compose li crea per PostgreSQL.

## Realm e indirizzo usato dai client

Il login manda il client a `advertisedAddress` e `advertisedPort` di un realm (pacchetti `0xA8` e `0x8C`). Devono essere l'indirizzo e la porta esterni del Service di quel realm, e il chart non può conoscere l'indirizzo di un `LoadBalancer` prima che esista. Imposta `service.loadBalancerIP` a un indirizzo che conosci, oppure installa una volta, leggi gli indirizzi e fai l'upgrade:

```sh
kubectl get svc -n moongate -l app.kubernetes.io/instance=moongate
helm upgrade moongate oci://ghcr.io/moongate-community/charts/moongate --version <release> -n moongate -f values.yaml
```

La porta del Service del login è `login.service.port` (2593). Il Service di un realm ascolta su `advertisedPort` e inoltra a 2595.

| Valore | Significato |
| --- | --- |
| `realms[].persistence.size`, `storageClass` | Il volume `/data` del realm, 5Gi per impostazione predefinita |
| `realms[].ping.enabled` | Apre la porta UDP di ping 12000 sul Service di quel realm. Un solo realm: due non possono condividere un indirizzo |
| `realms[].service.type`, `annotations`, `loadBalancerIP` | `LoadBalancer`, `NodePort` o `ClusterIP` |
| `realms[].extraToml`, `login.extraToml` | Testo aggiunto al `moongate.toml` del server, per qualsiasi altra [impostazione](server-configuration.md) |
| `network.encryptionMode` | `Disabled`, `Optional` o `Required` per entrambi i listener UO |
| `image.tag` | Per impostazione predefinita l'`appVersion` del chart, il rilascio con cui è stato pubblicato |

## Trial PostgreSQL and Redis

`postgresql.enabled` e `redis.enabled` (entrambi, senza `secrets.existingSecret`) installano un PostgreSQL 16 a un solo pod e un Redis 7.4 con `noeviction`, senza persistenza e con password generate. Servono a provare il chart, non a gestire uno shard: niente backup, niente replica, e ruoli e database vengono creati al primo avvio di un volume vuoto, quindi per un realm aggiunto dopo ruolo e database vanno creati a mano.

Le password sono nel Secret `<release>-moongate-generated`, che resta dopo `helm uninstall` perché una reinstallazione possa ancora aprire i database. Eliminalo con `kubectl delete secret` insieme al volume di PostgreSQL quando vuoi ripartire da zero. `helm template` e `--dry-run` non possono leggere i Secret esistenti, quindi mostrano password diverse da quelle installate.

## Aggiornare e fermare

Un aggiornamento è un nuovo `--version` (o `image.tag`) e `helm upgrade`. Il pod di ogni realm riparte, applica l'SQL in sospeso nel suo init container e si avvia; un pod per realm significa una breve interruzione di quel realm durante l'aggiornamento. Leggi prima il [changelog](../CHANGELOG.md) del rilascio di destinazione e segui la tua politica di backup dei database.

I pod hanno 120 secondi (`terminationGracePeriodSeconds`) per fermarsi, perché il salvataggio finale del mondo richiede da 8 a 15 secondi per il mondo distribuito. I salvataggi del mondo non sono backup di PostgreSQL. `helm uninstall` mantiene le PersistentVolumeClaim degli StatefulSet dei realm.

## Non coperto

Modalità standalone, il plugin di esempio, l'API di amministrazione (richiede un Secret con il certificato TLS), un Ingress, l'autoscaling e uno smoke test su un cluster usa e getta in CI. Il chart è controllato da `helm lint`, dalle asserzioni di rendering in `deploy/helm/moongate/tests` e da `kubeconform` a ogni modifica.

## Cosa è stato provato

Il chart supera lint, le sue asserzioni di rendering e gli schemi di Kubernetes. È stato installato su un cluster k3s a cinque nodi in un namespace usa e getta, con PostgreSQL e Redis di prova e una claim vuota per i file del client: ruoli e database sono stati creati, gli init container hanno applicato 4 migrazioni ad Auth e 26 a World, il login è partito e ascolta, e il realm si è fermato su `tiledata.mul not found in the Ultima path: /uo`, come deve senza i file del client. `helm upgrade` ha mantenuto le password generate e ha riavviato solo il pod la cui configurazione era cambiata.

Non ancora provati: un client reale, un volume `ReadWriteMany`, gli indirizzi `LoadBalancer`, la NetworkPolicy e l'installazione dall'indirizzo OCI pubblicato (la prima pubblicazione avviene con il prossimo rilascio). Le readiness probe sono semplici connessioni TCP, quindi ogni probe compare nel log del server come un client che si connette e se ne va.
