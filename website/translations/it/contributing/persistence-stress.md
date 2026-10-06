<!-- translation: {"sourceHash":"461965e194ca242c8dcf1206d37b79226093c05b762faffc7b7e15d4f5531ccd","title":"Test di stress della persistenza"} -->

# Test di stress della persistenza PostgreSQL

Esegui da un checkout del sorgente su Linux o WSL2 con .NET 10, Python 3 e Docker:

```sh
python3 scripts/stress-persistence.py
```

Il comando compila il progetto di test della persistenza esistente, crea un container
PostgreSQL dedicato ed esegue tre fasi: **100, 500 e 1.000 sessioni virtuali**, per
30 secondi ciascuna. Prevedi tempo aggiuntivo per scaricare l'immagine, preparare lo
schema, popolare i dati, drenare le operazioni in sospeso e controllare la correttezza.

Questo esercita l'implementazione reale di `DataAccess<T>`, FreeSql e PostgreSQL.
Il test non include protocollo Ultima Online, connessione TCP, `SessionService`,
GameLoop, autenticazione degli account, pianificazione del salvataggio del mondo o
bootstrap del server. Una sessione virtuale è un worker asincrono con una propria
riga sintetica persistita. Non è un giocatore connesso o una connessione PostgreSQL dedicata.

## Isolamento e riproducibilità

Lo script avvia sempre un proprio container PostgreSQL 16 con versione fissata.
Non accetta un indirizzo database esterno, non usa il TOML del server e non tocca
i database di sviluppo locali. PostgreSQL non ha accesso alla rete né porte pubblicate;
l'host di test usa un socket Unix temporaneo. L'autenticazione trust è confinata a
quell'istanza isolata.

Il container è limitato a **2 CPU e 1 GiB di RAM**, con un volume Docker anonimo su
disco. Le impostazioni di durabilità PostgreSQL restano quelle predefinite: non è
un database in memoria e `fsync` non è disabilitato. Le prestazioni effettive dello
storage dipendono comunque dall'host Docker. Lo script rimuove container, volume
anonimo e socket temporaneo al completamento, Ctrl-C o SIGTERM, conservando i report.
Un crash della macchina o `kill -9` può saltare la pulizia; qualsiasi container residuo
ha il prefisso `moongate-stress-` e un suffisso generato univoco.

Ogni fase usa un database di test appena creato e le tabelle `stress.sessions` e
`stress.writes`. La sincronizzazione dello schema è abilitata solo durante la
preparazione iniziale di quel database usa e getta. La riapertura per la verifica
la disabilita. Questo scenario misura la persistenza runtime; non è un test di
validazione delle migrazioni.

## Carico di lavoro

Ogni sessione parte con una riga contenente un `Serial` automatico, un numero di
sessione, una revisione e un payload di circa 300 caratteri. Esegue ripetutamente:

| Operazione logica | Quota approssimativa | Comportamento |
| --- | --- | --- |
| Lettura | 50% | Legge la propria riga per ID e valida revisione e payload |
| Query | 20% | Filtra per numero di sessione indicizzato con paginazione limitata |
| Aggiornamento | 20% | Incrementa revisione e payload della propria riga |
| Commit | 5% | Inserisce una riga di registro e aggiorna la sessione in una transazione |
| Rollback | 5% | Tenta entrambe le scritture, poi annulla deliberatamente la transazione |

La miscela è deterministica, e le esecuzioni brevi o fallite possono differire da
queste quote. Le transazioni contano come un'operazione logica anche se emettono più
comandi SQL. I rollback previsti contano come operazioni riuscite quando l'annullamento
richiesto si completa; eccezioni inattese e timeout sono errori.

Le sessioni non modificano contemporaneamente la stessa riga. Questo evita di
affermare incrementi atomici per un'API i cui upsert con ID esplicito seguono la
semantica dell'ultima scrittura che prevale. Il benchmark copre sessioni indipendenti
concorrenti e pressione sull'allocazione condivisa di database/identità, non contesa
su righe molto usate o controllo della concorrenza ottimistica.

Preparazione e popolamento sono fuori dall'intervallo misurato. Tutti i worker delle
sessioni vengono avviati insieme. Al massimo 32 operazioni logiche entrano contemporaneamente
nel livello di persistenza, e ogni sessione attende 10 ms dopo un'operazione prima
di inviarne un'altra. Ogni operazione ha una scadenza di annullamento di 30 secondi,
compresa l'attesa di ammissione. Alla fine di una fase, il lavoro accettato viene
drenato prima di chiudere la persistenza.

È un **carico a ciclo chiuso**: risposte lente riducono il ritmo delle richieste
offerte. Non modella un ritmo fisso di arrivi esterni né stabilisce la capacità di
giocatori. Per una saturazione sostenuta, usa `--think-ms 0` e controlla l'attesa di
ammissione oltre al throughput completato.

## Personalizzare un'esecuzione

```sh
# A quick smoke run
python3 scripts/stress-persistence.py --sessions 4 --seconds 2 --concurrency 2 --think-ms 0

# Longer saturated run with more in-flight work
python3 scripts/stress-persistence.py --sessions 100 500 1000 --seconds 120 --concurrency 64 --think-ms 0
```

`--sessions` accetta 1–10.000 worker per fase, `--seconds` 1–600, `--concurrency`
1–256 e `--think-ms` 0–60.000. Tutte le fasi devono esercitare almeno un commit e
un rollback; un'esecuzione molto breve con attesa lunga fallisce questo controllo.
Il limite configurato del pool di connessioni segue `--concurrency`, ma il valore
di concorrenza del report è un limite delle operazioni logiche, non un conteggio
osservato delle connessioni.

Scegli una directory di output nuova con `--output-dir /path/to/new-run`. Le
directory esistenti vengono rifiutate per non sovrascrivere le misurazioni precedenti.

## Risultati e correttezza

I risultati vengono scritti sotto `TestResults/persistence-stress/<UTC timestamp>-<id>/`:

| File | Contenuto |
| --- | --- |
| `report.json` | Throughput per fase, conteggi delle operazioni, percentili di latenza, errori e risultato della verifica |
| `postgres-stats.jsonl` | Campioni Docker di CPU, memoria e I/O a blocchi con timestamp |
| `environment.json` | Digest dell'immagine, limiti delle risorse, parametri del carico e revisione Git/stato delle modifiche locali |
| `build.log`, `test.log` | Diagnostica della compilazione e dell'host di test |

La latenza comprende l'attesa di un permesso di concorrenza e l'intera operazione
logica, incluso commit o rollback della transazione. I percentili usano istogrammi
nearest-rank arrotondati per eccesso a 1 ms; i campioni oltre 60 secondi condividono
un bucket di overflow riportato come massimo osservato. `AdmissionWait` misura
separatamente l'attesa. I percentili delle operazioni comprendono solo quelle
riuscite. Gli errori vengono contati per tipo, con la scadenza riportata come
`timeout`; le operazioni fallite non vengono ritentate.

`ElapsedSeconds` comprende il drenaggio del lavoro accettato e l'ultima pausa.
`SuccessfulOperationsPerSecond` divide le operazioni completate con successo per
quell'intervallo. CPU del processo, allocazioni e working set di fine fase coprono
l'intero host di test .NET durante quell'intervallo, non PostgreSQL. Ogni fase include
timestamp UTC di inizio/fine per correlazione. I campioni Docker comprendono preparazione
e verifica oltre al carico; sono approssimativi e non vanno confrontati con la latenza
per fase senza considerarne i timestamp.

Dopo ogni fase, il test rilascia il servizio di persistenza e inizializza una nuova
istanza sullo stesso database. Verifica ID finale, revisione e payload di ogni
sessione; tutte le revisioni e i conteggi del registro confermati; e l'assenza delle
righe di registro annullate deliberatamente. È una riapertura dell'istanza di
persistenza, non un test di durabilità dopo riavvio PostgreSQL, crash del processo
o perdita di alimentazione.

Il comando termina con errore in caso di errori del carico, discrepanze di verifica
o copertura insufficiente delle transazioni. Il JSON viene scritto dopo ogni fase
completata, inclusa una fase di carico fallita. Errori di preparazione o dell'host di
test possono verificarsi prima che esista un report; usa i log. Non c'è una soglia
universale di successo per throughput/latenza: confronta esecuzioni ripetute sullo
stesso host e carico prima di fissare un budget di prestazioni. Il generatore conserva
le revisioni attese del registro per verificarle, quindi esecuzioni lunghe con molte
scritture consumano anche memoria dell'host di test.

## Prestazioni: un'esecuzione reale di riferimento

Non esiste una soglia di successo/fallimento (vedi sopra); quanto segue è un'esecuzione
reale, come orientamento, non come obiettivo. Riproducila con
`python3 scripts/stress-persistence.py` (impostazioni predefinite: 100/500/1.000
sessioni, 30 s/fase, concorrenza 32, attesa 10 ms).

**Macchina di sviluppo:**

| | |
| --- | --- |
| CPU | AMD Ryzen AI 9 HX 370 (12 core / 24 thread) |
| RAM | 30 GiB |
| Disco | SSD NVMe (Samsung 970 EVO Plus) |
| Sistema operativo | Debian GNU/Linux 13 (trixie) |
| .NET | 10.0.12 |

Il container di stress rimane limitato a 2 CPU / 1 GiB indipendentemente dalle
specifiche dell'host (vedi Isolamento sopra), quindi la potenza dell'host influisce
soprattutto sul lato dell'host di test .NET di questo carico, non sul database.

**Risultati** (commit `1489e1f7`, `report.json`, mostrate lettura/aggiornamento/commit;
vedi [Carico di lavoro](#workload) sopra per il significato delle operazioni):

| Sessioni | Op/s | Lettura P50/P95/P99 (ms) | Aggiornamento P50/P95/P99 (ms) | Commit P50/P95/P99 (ms) | Verificato | Errori |
| --- | --- | --- | --- | --- | --- | --- |
| 100 | 2,656 | 12 / 36 / 52 | 56 / 91 / 127 | 66 / 105 / 193 | true | 0 |
| 500 | 2,678 | 19 / 430 / 546 | 431 / 581 / 669 | 538 / 733 / 800 | true | 0 |
| 1,000 | 1,659 | 76 / 1,227 / 5,811 | 1,121 / 2,388 / 3,893 | 1,556 / 7,661 / 7,939 | true | 0 |

Ogni fase ha verificato correttamente e prodotto zero errori a ogni numero di sessioni;
a peggiorare è la latenza, non la correttezza. Il salto netto tra 500 e 1.000 sessioni
è la saturazione del container di stress volutamente piccolo da 2 CPU/1 GiB, non
un'affermazione su `DataAccess<T>` stesso: ripeti con più sessioni su un'istanza
Postgres dimensionata per esse per vedere il limite di un deployment *reale*.

## Test ordinari e CI

Il test di stress completo è contrassegnato `Category=Stress` e saltato salvo
`MOONGATE_RUN_PERSISTENCE_STRESS=1`. Lo script imposta quel flag e filtra esplicitamente
il test di stress. Uno smoke test di correttezza con quattro sessioni (20 operazioni
per sessione) e i test degli istogrammi vengono eseguiti con la normale suite di
persistenza. Nessuna esecuzione di stress lunga viene aggiunta alla CI o attivata da un commit.

Vedi [Persistenza PostgreSQL](persistence.md) per le API applicative e
[Contribuire](../CONTRIBUTING.md) per il normale ambiente di test.
