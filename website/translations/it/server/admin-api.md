<!-- translation: {"sourceHash":"2e4ad44d971e066bca9c6272e5d009fb798fb2c7c7e342944061c35abfeb7231","title":"API di amministrazione"} -->

# API di amministrazione

Moongate include un plugin gRPC facoltativo per l'amministrazione su rete privata. Espone login degli account, informazioni sul server, elenco paginato degli account, creazione degli account e revoca delle sessioni. Un futuro pannello React dovrà chiamare il proprio backend; quel backend chiama questi endpoint gRPC. Il browser non si connette direttamente alle porte dello shard.

`MoongateAdminPlugin` è registrato in `Program.cs` dopo `MoongateUltimaPlugin`. È distribuito con il server, mai tramite la directory `plugins/`. La porta predefinita è **2590**. È **disabilitato per impostazione predefinita**.

## Abilitare un endpoint

Per una configurazione offline con certificato autofirmato, esegui:

```sh
mgctl init /srv/moongate --generate-admin-certificate \
  --admin-certificate-hosts "login.example.test,192.0.2.10"
```

Usa i nomi effettivi degli endpoint al posto di questi esempi. Questo crea
`certificates/admin.pfx` senza password, esporta il certificato pubblico `admin.crt` e
imposta `[admin_api].enabled = true` con TLS abilitato. Indirizzo di ascolto e
porta esistenti restano invariati. Considera attendibile `admin.crt` nei client del backend e riavvia il
server. Vedi [configurazione e rinnovo dei certificati](mgctl.md#generate-an-administration-certificate).

Per un certificato fornito dall'operatore, configura `<MOONGATE_ROOT>/config/moongate.toml`:

```toml
[admin_api]
enabled = true
listen_address = "127.0.0.1"
port = 2590
session_lifetime_minutes = 30
max_receive_message_bytes = 65536
max_concurrent_calls = 64
allow_insecure_loopback = false
certificate_path = "certificates/admin.pfx"
certificate_password = "$MOONGATE_ADMIN_CERTIFICATE_PASSWORD"
```

Usa l'indirizzo dell'interfaccia raggiungibile dal backend del pannello privato. Sia `listen_address = "*"` sia `listen_address = "0.0.0.0"` ascoltano su tutte le interfacce IPv4; `"*"` è un alias di `"0.0.0.0"`, anche all'interno di una rete privata di container. Usa `"::"` per ascoltare sull'indirizzo wildcard IPv6. Gli indirizzi wildcard richiedono TLS. Limita l'accesso di rete agli host attendibili. Riavvia dopo le modifiche alla configurazione.

L'endpoint usa TLS server standard su HTTP/2. Non usa mTLS, certificati dei peer o il segreto Redis di trasferimento del gioco. Ottieni dalla CA privata un PFX server con chiave privata, uso per autenticazione del server e nomi DNS corrispondenti agli endpoint usati dai client. Posizionalo nel percorso configurato, leggibile solo dall'account di servizio Moongate. Distribuisci ai client del backend il **certificato pubblico della CA**, non la chiave privata del server. I client devono verificare sia la catena di fiducia sia il nome host.

Un percorso relativo del certificato viene risolto rispetto a `MOONGATE_ROOT`; sono supportati variabili d'ambiente e `~`. Un PFX senza password usa `certificate_password = ""`. Altrimenti fornisci la password tramite un riferimento d'ambiente popolato dal gestore dei segreti. L'avvio normale del server non genera certificati; usa esplicitamente `mgctl init <root> --generate-admin-certificate`. Certificati mancanti/illeggibili/scaduti, variabili d'ambiente mancanti o una porta occupata fanno fallire l'avvio abilitato con un errore dai dati sensibili oscurati. Gli endpoint disabilitati non caricano certificati né risolvono le variabili delle password.

Solo per lo sviluppo locale:

```toml
[admin_api]
enabled = true
listen_address = "127.0.0.1"
port = 2590
allow_insecure_loopback = true
```

Questa deroga esplicita consente HTTP/2 in chiaro solo su indirizzi letterali di loopback (`127.0.0.1` o `::1`). Nomi host, indirizzi wildcard e interfacce bridge Docker non possono usare la deroga. Viene registrato un avviso. I client di esempio forniti richiedono TLS; usa un client HTTP/2 configurato in chiaro quando provi questa deroga.

Le impostazioni accettano porte 1–65535, durate delle sessioni 1–1440 minuti, limiti di ricezione 1024–1048576 byte e limiti di chiamate concorrenti 1–1024. I valori predefiniti sono mostrati sopra. L'API accetta chiamate solo dopo che gli iscritti all'avvio del server hanno terminato; lo spegnimento rifiuta nuove chiamate e completa il lavoro esistente prima dell'arresto delle dipendenze.

## Predisporre il primo amministratore

Esegui questi comandi nella **console locale Login o Standalone** (premi `*` per sbloccarla):

```text
account create <username> <password> Administrator
account api-access <username> on
```

Sostituisci i segnaposto con credenziali del gestore dei segreti. Gli argomenti della console non possono contenere spazi. `account api-access` rifiuta l'invocazione in gioco. Disabilita l'accesso con `account api-access <username> off`; questo revoca anche le sessioni amministrative precedenti.

Gli account creati da `account create` hanno l'accesso API disabilitato. Anche `CreateAccount` lo disabilita quando `can_access_api` è omesso; un chiamante autorizzato può richiedere esplicitamente l'accesso. Questi percorsi di creazione impostano esplicitamente il permesso invece di affidarsi all'inizializzatore dell'entità. La migrazione `auth/0004_account_admin_api_access.sql` aggiunge la colonna con valore predefinito false nel database; se uno schema di sviluppo esistente ha già la colonna, i valori true espliciti vengono conservati. Applica l'SQL in attesa con il [flusso delle migrazioni](persistence-migrations.md). Il normale login di gioco è indipendente dall'accesso API.

## Ruoli e permessi

| RPC | Login / Standalone | Game | Permesso |
| --- | --- | --- | --- |
| `AdminLogin.Login` | Sì | No | Password valida, account sbloccato, `CanAccessApi = true`, tipo di account definito |
| `AdminSession.Logout` | Sì | Sì | Token presentato ben formato; già assente equivale a successo |
| `AdminServer.GetServerInfo` | Sì | Sì | Qualsiasi sessione amministrativa valida |
| `AdminAccounts.ListAccounts` | Sì | No | Administrator |
| `AdminAccounts.CreateAccount` | Sì | No | Administrator |
| `AdminAccountSessions.RevokeAccountSessions` | Sì | No | Administrator |

Gli host Game non risolvono mai i servizi Accounts né si connettono al database Accounts. I servizi specifici del ruolo non disponibili restituiscono `UNIMPLEMENTED`. Configura gli indirizzi degli endpoint nel backend del pannello; gli endpoint di amministrazione non vengono annunciati nell'individuazione dei realm.

Gli account Regular e GameMaster possono leggere le informazioni del server quando abilitati all'API, ma non possono elencare/creare account o revocare sessioni altrui. I tipi di account sconosciuti vengono rifiutati. `CreateAccount` può assegnare qualsiasi tipo definito e abilitare esplicitamente l'accesso API perché solo gli Administrator possono invocarlo.

## Chiamare l'API

Il package sul protocollo è `moongate.admin.v1`. Ottieni i client C# generati da `Moongate.Admin.Contracts`, oppure genera qualsiasi linguaggio supportato dai file grezzi sotto `proto/moongate/admin/v1/`. Ogni release include `moongate-admin-protos-<version>.zip`. Gli import standard `google/protobuf` provengono dalla distribuzione del compilatore.

Una sequenza di richieste:

1. Chiama `AdminLogin.Login` su un endpoint Login con `username` e `password`.
2. Conserva l'`access_token` restituito nella memoria del backend. Invia i metadati `authorization: Bearer <token>` sulle chiamate protette a Login o Game.
3. Usa `AdminAccounts.CreateAccount` e `ListAccounts` su Login, oppure `AdminServer.GetServerInfo` su entrambi gli endpoint.
4. Chiama `AdminSession.Logout` su uno dei due endpoint per rimuovere quel token globalmente.

Esempi eseguibili: [client C#](../samples/Moongate.Admin.Client/README.md), [client Python](../samples/admin-python/README.md). Entrambi usano la verifica TLS standard, credenziali fornite dall'ambiente, scadenze limitate e nessun nuovo tentativo automatico delle mutazioni. Creano un account di prova che rimane in Accounts; usa ambienti eliminabili per la verifica.

`ListAccounts` usa la paginazione keyset del database: inizia con `after_account_id = 0`, invia il `next_after_account_id` restituito nella chiamata successiva, fermati quando è zero. La dimensione predefinita della pagina è 50; il massimo è 200. Gli ID sono `uint32` diversi da zero per gli account esistenti. I riepiloghi contengono nome utente, ID, ruolo, flag di accesso/blocco e data di creazione UTC, mai password, hash o email.

Omettere `CreateAccount.account_type` significa Regular. `UNSPECIFIED` esplicito e valori enum sconosciuti falliscono. `can_access_api` è false per impostazione predefinita. I nomi utente devono essere non vuoti e lunghi al massimo 255 caratteri; le password sono non vuote e di massimo 1024 byte UTF-8. I caratteri NUL sono rifiutati. Il confronto dei nomi utente mantiene la semantica esistente degli account, sensibile alle maiuscole.

## Sessioni, revoca ed errori

I token hanno 256 bit casuali e una durata assoluta predefinita di 30 minuti. Non esistono rinnovo scorrevole o refresh token: effettua nuovamente il login dopo la scadenza. Redis memorizza solo digest dei token e campi di identità sicuri sotto `moongate:admin:`, separatamente da lease dei realm/ticket di trasferimento. Sono consentite fino a 64 sessioni attive per account. Le sessioni amministrative **non hanno un proprio livello di cifratura**; proteggi la distribuzione Redis privata e le credenziali come descritto nella [configurazione](server-configuration.md).

Ogni chiamata protetta controlla Redis. Logout e revoca dell'intero account si applicano immediatamente tra host per le richieste appena ammesse. Un riavvio di Redis effimero invalida le sessioni; un riavvio del server non ne rinnova la scadenza. Un'interruzione di Redis nega l'accesso con `UNAVAILABLE`. Il login è limitato tra host a 10 tentativi/minuto per nome utente esatto e 30/minuto per indirizzo diretto del peer; gli header degli indirizzi inoltrati non sono considerati attendibili.

Le modifiche di sicurezza degli account supportate passano da `IAccountAdminAccessService.SetApiAccessAsync`, `UpdateAccessAsync`, `ChangePasswordAsync` o `RevokeSessionsAsync`. Questi coordinano i lock di riga PostgreSQL con le generazioni di autorizzazione Redis. Un login in competizione con una modifica di sicurezza non può conservare una sessione privilegiata obsoleta. Una mutazione parzialmente fallita lascia l'accesso bloccato; un login valido successivo recupera rispetto allo stato autorevole dell'account confermato con commit. **Gli aggiornamenti SQL diretti e generici tramite `IDataAccess` aggirano la revoca immediata.** Usa i metodi supportati del servizio per le modifiche di sicurezza.

| Stato | Significato |
| --- | --- |
| `INVALID_ARGUMENT` | Input, enum, ID del cursore o dimensione della pagina non validi |
| `UNAUTHENTICATED` | Credenziali non valide o token mancante, scaduto o revocato |
| `PERMISSION_DENIED` | La sessione valida manca del ruolo richiesto |
| `ALREADY_EXISTS` | Il nome utente esiste già |
| `NOT_FOUND` | Il destinatario della revoca non esiste |
| `RESOURCE_EXHAUSTED` | Limite di login/sessioni/chiamate concorrenti raggiunto, oppure richiesta troppo grande |
| `UNAVAILABLE` | Interruzione di una dipendenza o endpoint non pronto/in arresto |
| `UNIMPLEMENTED` | Questo ruolo server non espone la RPC |
| `CANCELLED` / `DEADLINE_EXCEEDED` | Annullamento del chiamante o esecuzione limitata terminata |
| `INTERNAL` | Errore inatteso; controlla i log sicuri del server |

Le chiamate hanno una finestra massima di esecuzione sul server di 15 secondi. L'annullamento non annulla una creazione di account già confermata. Se una risposta di creazione va persa, un nuovo tentativo può restituire `ALREADY_EXISTS`; usa l'elenco per riconciliare. Non esiste garanzia di esecuzione una sola volta né politica di nuovi tentativi automatici. La revoca non annulla le scritture delle richieste già ammesse.

Gli audit registrano operazione, ID degli account, esito e correlazione della richiesta; nessuna password, token o corpo delle richieste. Le future mutazioni del mondo attivo devono essere pianificate tramite GameLoop. Questa versione non espone modifiche dei personaggi o esecuzione di comandi arbitrari.

## Docker e verifica

L'immagine documenta la porta 2590, ma `EXPOSE` non la abilita né la pubblica. L'[override Compose privato](../examples/docker/login-realms/compose.admin.yaml) abilita TLS con file PFX montati forniti dall'operatore e non pubblica alcuna porta di amministrazione. Vedi la [guida Compose](docker-login-realms.md).

Controlli del repository:

```sh
bash scripts/verify-admin-protos.sh
bash examples/docker/login-realms/admin-smoke.sh
```

Il primo richiede PostgreSQL e Redis: Docker, oppure `MOONGATE_TEST_POSTGRES_CONNECTION_STRING` e `MOONGATE_TEST_REDIS_CONNECTION_STRING` (vedi [Verificare le modifiche](../CONTRIBUTING.md#verify-your-changes)); usa un ambiente Python temporaneo e fixture TLS reali. Il secondo costruisce le immagini server, crea un proprio progetto Compose eliminabile e una CA di prova, esegue il client Python dalla rete Docker privata e rimuove solo i propri container/volumi/certificati.
