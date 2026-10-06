<!-- translation: {"sourceHash":"62bbac0f1e23e867ea175b2075bda3e925a69ef3980ee3a663f93c47b826a602","title":"Panoramica"} -->

<p align="center">
  <img src="images/moongate_logo.png" alt="Logo di Moongate" width="220" />
</p>

<h1 align="center">Moongate</h1>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/actions/workflows/ci.yml?query=branch%3Adevelop"><img src="https://img.shields.io/github/actions/workflow/status/moongate-community/moongate/ci.yml?branch=develop&amp;label=CI%20%28develop%29" alt="CI su develop"></a>
  <a href="https://github.com/moongate-community/moongate/actions/workflows/security.yml?query=branch%3Amain"><img src="https://img.shields.io/github/actions/workflow/status/moongate-community/moongate/security.yml?branch=main&amp;label=security%20%28main%29" alt="Audit di sicurezza delle dipendenze su main"></a>
  <a href="https://github.com/moongate-community/moongate/releases/latest"><img src="https://img.shields.io/github/v/release/moongate-community/moongate?label=release" alt="Ultima release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue" alt="Licenza: AGPL-3.0-or-later"></a>
</p>

<p align="center">
  <a href="https://moongate.sh/"><img src="https://img.shields.io/badge/docs-moongate.sh-3867D6" alt="Documentazione su moongate.sh"></a>
  <a href="https://github.com/moongate-community/moongate/pkgs/container/moongate"><img src="https://img.shields.io/badge/ghcr.io-moongate-2496ED?logo=docker&amp;logoColor=white" alt="Immagine container"></a>
  <a href="https://dotnet.microsoft.com/en-us/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&amp;logoColor=white" alt=".NET 10"></a>
  <a href="docs/scripting.md"><img src="https://img.shields.io/badge/Lua-5.2-2C2D72?logo=lua&amp;logoColor=white" alt="Scripting Lua 5.2"></a>
  <a href="https://buymeacoffee.com/zk7bnrbk4i"><img src="https://img.shields.io/badge/Buy%20me%20a%20coffee-support-FFDD00?logo=buymeacoffee&amp;logoColor=black" alt="Offrimi un caffè"></a>
</p>

Moongate è un emulatore open source di server Ultima Online scritto in C# su .NET 10.
Combina scripting Lua, persistenza PostgreSQL e trasferimento dal login al gioco
basato su Redis, con librerie riutilizzabili per costruire strumenti e servizi server.

I salvataggi del mondo non fermano mai il gioco: il ciclo di gioco si sospende solo per copiare il mondo in memoria, circa un
decimo di secondo per 173.000 entità, e il salvataggio scrive su PostgreSQL in background solo
le entità cambiate dall'ultimo salvataggio. Vedi
[Un salvataggio non ferma il gioco](docs/persistence-operations.md#a-save-does-not-stop-the-game).

Moongate supporta più shard: un server di login elenca un numero qualsiasi di server di gioco (fino a
128), ciascuno con il proprio mondo e database, e i giocatori ne scelgono uno dall'
elenco server del client. La modalità `standalone` esegue login e un singolo shard in
un processo. Vedi [Login e realm con Docker](docs/docker-login-realms.md).

## Stato

**In sviluppo attivo; il mondo non è ancora un gioco.** I personaggi vengono
creati, entrano nel mondo, camminano e corrono, si vedono e parlano, e spostano oggetti nel
proprio zaino e a terra. Il mondo è decorato, con porte che si aprono
(quelle cittadine sono lette dalla mappa), porte chiuse a chiave e relative chiavi, insegne dei negozi,
luci e teletrasporti; passano giorni e notti, i dungeon sono bui,
e ogni regione ha meteo, musica e stagione propri. Gli script riproducono effetti grafici. Le regioni di spawn popolano il mondo di NPC, su terra
e acqua, e li rigenerano; i game master generano e rimuovono gli NPC anche manualmente. Gli NPC
vicini a un giocatore eseguono il proprio script Lua per mobile, che può farli camminare lungo un percorso trovato con A*
aggirando muri, porte chiuse e mobili, e gli oggetti reagiscono agli script Lua per oggetti.
Non ci sono ancora combattimento, IA integrata degli NPC, morte o incremento delle abilità.

Rete e pipeline dei pacchetti, runtime Lua, infrastruttura di persistenza,
caricamento dei dati dello shard e lettori dei file client con interrogazioni di movimento e linea di vista
sono pronti. Vedi lo [Stato dell'implementazione](docs/implementation-status.md)
per i comportamenti supportati e il lavoro rimanente, e la [Roadmap](docs/roadmap.md)
per l'ordine in cui vengono realizzati i sistemi mancanti.

## Primi passi

Per eseguire Moongate, servono i tuoi file di dati del client Ultima Online, PostgreSQL
e Redis 7+. I dati del client non sono inclusi. La [Guida al primo avvio](docs/getting-started.md)
spiega come preparare la root del server, configurare queste dipendenze, applicare
le migrazioni del database e avviare il server.

Scegli un metodo di installazione sotto. L'installer Linux e l'immagine container
includono il runtime .NET; la compilazione dai sorgenti richiede l'**SDK .NET 10**.

## Installare su Linux

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

Installa l'ultima release in `/opt/moongate` e rende `moongate` e `mgctl`
disponibili nel path. Supporta Linux x64 e ARM64 con glibc. Mantieni la root del server
fuori dalla directory di installazione per preservare configurazione e dati
durante gli aggiornamenti. Vedi [Installare su Linux](docs/installation.md) per opzioni, aggiornamenti e rimozione.

## Docker

Ogni release pubblica un'immagine `linux/amd64` su
[GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
Vedi [Eseguire con Docker](docs/docker.md) per configurazione del primo avvio, archiviazione
persistente, Docker Compose, log e aggiornamenti.

## Compilare dai sorgenti

```sh
git clone https://github.com/moongate-community/moongate.git
cd moongate
git switch develop
dotnet build Moongate.slnx -c Release
```

`develop` contiene lavoro non ancora rilasciato. Usa un [tag di release](https://github.com/moongate-community/moongate/releases)
per compilare una versione pubblicata. Continua con [Primo avvio](docs/getting-started.md)
per configurarla ed eseguirla, oppure con [Contribuire](CONTRIBUTING.md) per lavorare sul codice.

## Scripting

Gli script vengono eseguiti sul thread del ciclo di gioco in un runtime Lua 5.2 isolato basato su
[LuaCSharp](https://github.com/nuskey8/Lua-CSharp). Inserisci lo script di avvio in
`scripts/init.lua` sotto la root del server:

```lua
-- scripts/init.lua
log.info("booted {Engine} {Version}", engine.name, engine.version)

timer.every(30, function()
    log.info("tick")
    wait(2) -- suspends this coroutine without blocking the game loop
    log.info("two seconds later")
end)
```

Il runtime fornisce log, timer, eventi, script per NPC e oggetti, budget di
istruzioni e definizioni generate per l'editor. Vedi [Scrivere script Lua](docs/scripting.md) per API
disponibili, comandi di ricaricamento e configurazione, e il
[README del package](src/Moongate.Scripting/README.md) per binding C# e limiti dell'isolamento.

## Guide del server

| Area | Guide |
| --- | --- |
| Configurazione e gestione | [Configurazione del server](docs/server-configuration.md), [comandi](docs/commands.md), [diagnostica](docs/diagnostics.md) |
| Client | [Enhanced Client](docs/enhanced-client.md) |
| Archiviazione | [Persistenza PostgreSQL e salvataggi del mondo](docs/persistence.md) |
| Protocollo ed esecuzione | [Pacchetti e handler](docs/packets.md), [ciclo di gioco e timer](docs/game-loop-and-timers.md) |
| Dati del client | [File del client, interrogazioni di movimento e linea di vista](docs/world-queries.md) |
| Validazione | [Copertura dei test](docs/test-coverage.md), [audit di sicurezza delle dipendenze](docs/security-audit.md) |

## Estendere Moongate

| Obiettivo | Guide |
| --- | --- |
| Aggiungere comportamento al server | [Plugin](docs/plugins.md), [moduli Lua in C#](docs/lua-modules.md) |
| Aggiungere diagnostica | [Provider di metriche](docs/metric-providers.md) |
| Definire contenuti dello shard | [File di dati dello shard](docs/data-files.md), [template TOML](docs/templates.md), [spawn degli NPC](docs/spawns.md) |
| Programmare il gioco con script | [Script Lua](docs/scripting.md), [gump](docs/gumps.md), [banca](docs/bank.md) |
| Personalizzare i formati dei dati | [Tipi di valore TOML](docs/toml-types.md) |
| Tradurre i messaggi del server | [Localizzazione](docs/localization.md) |

Il [plugin di esempio](samples/Moongate.Sample.Plugin/) compilato mostra registrazione
del plugin, binding Lua e un provider di metriche. La suite di test lo carica tramite
il vero loader dei plugin.

## Librerie

I nove package di libreria hanno i propri README in inglese ed esempi eseguibili.
Vedi [Librerie NuGet e verifica dei package](docs/nuget-packaging.md) per
elenco dei package, dipendenze e comando di verifica locale.

## Documentazione

Sfoglia le guide complete e la documentazione delle librerie su **[moongate.sh](https://moongate.sh/)**.
Vedi il [changelog](CHANGELOG.md) per la cronologia delle release e
[Scrivere documentazione](docs/documentation.md) per anteprime locali e contributi alle pagine.

## Contribuire

Leggi [CONTRIBUTING.md](CONTRIBUTING.md) per configurazione dello sviluppo, convenzioni del codice,
comandi di validazione e flusso delle pull request. I contributi sono destinati a `develop`.

Moongate è anche un progetto personale costruito per il piacere di programmare.
[Come uso l'AI](docs/ai-usage.md) spiega l'approccio del manutentore a migrazione,
test e progettazione assistiti dall'AI, e il codice che sceglie di scrivere a mano.

## Licenza

Moongate è distribuito con licenza [AGPL-3.0-or-later](LICENSE).
Le attribuzioni di terze parti sono elencate in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
