<!-- translation: {"sourceHash":"e9eed87c8304db0fb5dacd24aa147b792367bae2a2f688e2cfd4532f8fdcbd40","title":"Contribuire a Moongate"} -->

# Contribuire a Moongate

Sono benvenuti contributi come segnalazioni di bug, correzioni, funzionalità, test, documentazione
ed esempi. Questa guida spiega come preparare un contributo per la revisione.

## Prima di iniziare

Cerca nelle [issue esistenti](https://github.com/moongate-community/moongate/issues)
e nelle [pull request](https://github.com/moongate-community/moongate/pulls) prima di
iniziare. Ogni funzionalità parte da una issue che la descrive in dettaglio, come
richiede [CODE_CONVENTION.md §13.1](CODE_CONVENTION.md); lo stesso vale per una modifica
ad API pubbliche, comportamento dei pacchetti o formati di persistenza. Correzioni di bug e documentazione
possono andare direttamente in una pull request.

Per una segnalazione di bug, includi versione o commit Moongate, sistema operativo,
passaggi di riproduzione, comportamento atteso ed effettivo e log rilevanti. Includi la
versione del client per problemi di rete. Rimuovi credenziali e dati personali da
qualsiasi cosa condivisa. Le richieste di funzionalità devono spiegare caso d'uso e comportamento
previsto, non solo l'implementazione proposta.

## Preparare il checkout

Servono Git e l'**SDK .NET 10** per compilare la soluzione. La verifica dei package
richiede anche Bash e accesso a nuget.org. Per il sito, usa la versione Node
in [website/.nvmrc](website/.nvmrc); Node non serve per la build C#.

Crea un fork del repository su GitHub, poi un branch dall'attuale `develop`:

```sh
git clone https://github.com/YOUR-USERNAME/moongate.git
cd moongate
git remote add upstream https://github.com/moongate-community/moongate.git
git fetch upstream
git switch -c feature/short-description upstream/develop
```

Usa un nome descrittivo del branch, come `fix/packet-length` o `docs/plugin-guide`.
Per eseguire un server, vedi la [guida Docker](docs/docker.md), inclusi i
file richiesti del client Ultima Online e la configurazione del primo avvio.

## Seguire le convenzioni del progetto

Leggi [CODE_CONVENTION.md](CODE_CONVENTION.md) e usa l'
[.editorconfig](.editorconfig) del repository. Definiscono stile C#, namespace, collocazione dei tipi,
organizzazione dei test e documentazione delle interfacce pubbliche.

Mantieni le modifiche concentrate su un problema. Segui il codice circostante ed evita
formattazioni o refactoring non correlati. Scrivi documentazione, commenti XML pubblici,
messaggi di commit e descrizioni delle pull request in inglese. Discuti esplicitamente le modifiche
incompatibili e spiega il loro effetto su utenti delle librerie e plugin.

Usa Conventional Commits, per esempio:

```text
fix(network): reject invalid packet lengths
feat(persistence): add a collection query option
docs(plugins): clarify bundle deployment
```

## Verificare le modifiche

Per un controllo rapido mentre lavori, esegui la suite veloce. Non richiede Docker né
variabili, perché esclude i test nei namespace `Integration`, `Performance` e
`Stress` o con un tratto `Category` corrispondente:

```sh
scripts/test.sh
scripts/test.sh fast --filter 'FullyQualifiedName~Serial'
```

`scripts/test.sh all` esegue la suite completa. I test del database e Redis richiedono
server PostgreSQL e Redis isolati. Con Docker attivo, i
test li avviano autonomamente con [Testcontainers](https://dotnet.testcontainers.org/):
un `postgres:17-alpine` e un `redis:7-alpine` (con `maxmemory-policy noeviction`)
per processo di test, rimossi alla fine dell'esecuzione. La prima esecuzione scarica le immagini.

Per usare invece server tuoi, come fa la CI, imposta queste variabili d'ambiente
tramite il fornitore di segreti; una variabile impostata prevale sempre sul container:

| Variabile | Dipendenza dei test |
| --- | --- |
| `MOONGATE_TEST_POSTGRES_CONNECTION_STRING` | Una connessione Npgsql amministrativa a un server PostgreSQL eliminabile; il ruolo deve poter creare ed eliminare database di test |
| `MOONGATE_TEST_REDIS_CONNECTION_STRING` | Una stringa di connessione StackExchange.Redis per un server Redis 7+ eliminabile configurato con `maxmemory-policy noeviction` |

Non puntare queste variabili ai database o all'istanza Redis di uno shard attivo.
Senza variabili e senza Docker, i test di integrazione falliscono; non vengono
saltati silenziosamente. Esegui i progetti di test in serie con `-m:1`, come fa la CI, perché i loro
host condividono i servizi database.

La CI mantiene la directory dati PostgreSQL eliminabile su un mount `tmpfs` limitato a
1 GiB. Creazione e rimozione ripetute dei database forzerebbero altrimenti centinaia di
checkpoint su disco sul runner condiviso. Le impostazioni `fsync`, WAL e commit
sincrono di PostgreSQL restano abilitate, e la CI esegue comunque la suite completa con copertura.
L'artefatto CI `test-results` contiene rapporti TRX con tempi individuali dei test,
inclusi rapporti di esecuzioni fallite, e viene conservato per 14 giorni.

Esegui questi comandi dalla root del repository per replicare i controlli della soluzione in CI:

```sh
dotnet restore Moongate.slnx
dotnet format style Moongate.slnx --diagnostics IDE0022 --severity warn --no-restore --verify-no-changes
dotnet build Moongate.slnx -c Release --no-restore
bash scripts/test.sh all -c Release --no-build
```

`scripts/coverage.sh` accetta gli stessi argomenti, esegue i test con copertura del codice
e scrive un rapporto unificato in `artifacts/coverage/index.html`. La CI lo esegue al posto
di `scripts/test.sh`, mostra il riepilogo per assembly sulla pagina dell'esecuzione e conserva il
rapporto HTML come artefatto `coverage-report`. La documentazione pubblicata mostra il
rapporto del proprio commit nella pagina [Copertura dei test](docs/test-coverage.md).

Per i test facoltativi di carico concorrente sul database, vedi [Stress test della persistenza PostgreSQL](docs/persistence-stress.md).

Aggiungi o aggiorna test per il comportamento modificato. Per le correzioni di bug, includi un test di regressione
che dimostri l'errore. Segui la struttura dei test in `CODE_CONVENTION.md` e
mantieni le asserzioni concentrate sul comportamento osservabile. Le correzioni alla sola prosa non
richiedono nuovi test C#.

La CI viene eseguita sulle pull request e sui push a `develop` e `main`; modifiche che toccano solo
`docs/`, `website/` o file Markdown la saltano. Pull request verso `develop` e push a
`develop` eseguono solo ripristino, verifica dello stile dei corpi dei metodi e build: esegui i test localmente
(`bash scripts/test.sh all`) prima di aprire la pull request. Pull request verso `main`, che
creano una release, e push a `main` eseguono la pipeline completa: test con copertura, client di amministrazione
portabile, package NuGet, esempi README eseguibili e avvisi di terze parti. L'immagine Docker di sviluppo
(`ghcr.io/moongate-community/moongate:develop`) viene ricostruita ogni notte, o su richiesta dalla
scheda Actions. Il controllo di amministrazione usa le stesse connessioni di
 test e richiede Python 3 con supporto `venv` e accesso per installare le
dipendenze in `samples/admin-python/requirements.txt`:

```sh
bash scripts/verify-admin-protos.sh
bash scripts/verify-packages.sh
./scripts/third-party-notices.sh
git diff --exit-code -- THIRD-PARTY-NOTICES.md
```

Vedi [Verifica dei package NuGet](docs/nuget-packaging.md) per prerequisiti e
risoluzione dei problemi. Se una modifica alle dipendenze aggiorna gli avvisi, revisiona e includi
l'aggiornamento nel commit affinché il confronto CI sia pulito.

Per modifiche alla documentazione o al sito, esegui:

```sh
npm --prefix website ci
npm --prefix website test
npm --prefix website run build
```

La build verifica link locali, immagini e frammenti delle intestazioni. Modifica i sorgenti
Markdown originali invece dei file generati del sito. Vedi
[Scrivere documentazione](docs/documentation.md) per comandi di anteprima e registrazione
 delle pagine. Gli esempi README delle librerie conservano i marcatori `nuget-smoke` affinché
la verifica dei package possa compilarli ed eseguirli.

## Inviare una pull request

1. Esegui il commit della modifica mirata e il push del branch sul tuo fork.
2. Apri una pull request verso **`moongate-community/moongate:develop`**.
3. Spiega problema, comportamento risultante, issue rilevante e controlli eseguiti.
   Evidenzia modifiche di compatibilità ed eventuali controlli non eseguibili.
4. Rispondi alla revisione e risolvi gli errori CI prima del merge. Aggiorna la
   documentazione quando cambiano comportamento o configurazione.

Mantieni le discussioni rispettose e concentrate sulla modifica. I manutentori revisionano e
integrano i contributi; aprire una pull request non richiede la pubblicazione di package
 o la creazione di una release.

La licenza del progetto è disponibile in [LICENSE](LICENSE). Mantieni intatti gli avvisi esistenti di copyright
 e licenza; l'attribuzione delle dipendenze è registrata in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
