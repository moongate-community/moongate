<!-- translation: {"sourceHash":"446af13d955d68763e53b35eca529cdceea954e9477185ec4bf912da5472190f","title":"Scrivere un plugin"} -->

# Scrivere un plugin

Un plugin è un assembly con una classe pubblica che implementa `IMoongatePlugin`,
collocato sotto `<root>/plugins/<bundle>/`. Registra servizi nel container dell'host
prima dell'avvio del server; l'host li risolve e avvia allo stesso modo dei propri
servizi incorporati.

Questa pagina ti porta da una libreria di classi vuota a un bundle distribuito.
L'esempio completo, [samples/Moongate.Sample.Plugin/](../samples/Moongate.Sample.Plugin/),
registra un'entità di persistenza, un modulo e un enum Lua, un comando console e un
provider di metriche, e la suite di test lo carica tramite il vero loader dei plugin.

## Il contratto

Ogni plugin implementa `IMoongatePlugin` da `Moongate.Server.Core.Interfaces.Plugins`:

```csharp
public interface IMoongatePlugin
{
    MoongatePluginData Metadata { get; }

    void Register(Container container);
}
```

`Metadata` è un record `MoongatePluginData` costruito come
`new(id, name, version, author?, description?, dependencies?)`. `id` è il valore a
cui si riferiscono ogni dipendenza e messaggio di errore. Viene confrontato senza
distinguere maiuscole e minuscole e `CODE_CONVENTION.md` §10 ne fissa la forma:
dominio inverso, `com.github.author.Moongate.plugins.name`. L'esempio usa
`com.github.moongate-community.moongate.plugins.greeter`.

`dependencies` è una lista di `MoongatePluginDependencyData(id, minimumVersion?)`.
`minimumVersion` è inclusiva. Una lista con due voci per lo stesso ID è rifiutata
dal costruttore `MoongatePluginData` stesso, prima che il plugin raggiunga il registro.

`Register` registra soltanto. Nulla può avviarsi, aprire un file o un socket o
accedere al database al suo interno; l'host avvia i servizi dopo, in ordine di
priorità (vedi [Cosa può fare Register](#what-register-may-do)).

## Creare il progetto

```bash
dotnet new classlib -n MyShard.Plugin
```

Poi modifica il `.csproj` generato:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Moongate.Server.Core" Version="0.6.0" ExcludeAssets="runtime" />
    <PackageReference Include="Moongate.Scripting" Version="0.6.0" ExcludeAssets="runtime" />
  </ItemGroup>

</Project>
```

Fissa la versione distribuita dal tuo host; quella corrente è su
[nuget.org/packages/Moongate.Server.Core](https://www.nuget.org/packages/Moongate.Server.Core).

`ExcludeAssets="runtime"` esclude la `.dll` propria di ogni pacchetto dall'output
di compilazione. L'host carica già `Moongate.Server.Core.dll` e
`Moongate.Scripting.dll`, quindi una copia nel bundle sarebbe peso inutile.
Un pacchetto non distribuito dall'host **non** deve avere quell'attributo, così il
suo assembly finisce nel bundle. I plugin che registrano entità persistite referenziano
anche `Moongate.Persistence` allo stesso modo; l'host fornisce e controlla l'identità
dei propri contratti condivisi FreeSql e Npgsql.

`EnableDynamicLoading` fa copiare all'SDK le dipendenze NuGet private accanto
all'output di compilazione; senza, una dipendenza non distribuita dall'host manca
nel bundle. Nomina il progetto come il bundle da distribuire (`MyShard.Plugin/`
che produce `plugins/MyShard.Plugin/`): il loader si aspetta che l'assembly di ingresso
abbia il nome della directory del bundle.

## La classe del plugin

Il plugin utile più piccolo registra un comando console:

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace MyShard.Plugin;

public sealed class MyShardPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new(
        "com.github.myshard.moongate.plugins.hello",
        "Hello",
        new Version(1, 0)
    );

    public void Register(Container container)
    {
        container.RegisterCommand<HelloCommand>(
            "hello",
            "Prints a greeting: hello <name>.",
            CommandSourceType.Console,
            AccountType.Regular
        );
    }
}

public sealed class HelloCommand : ICommandExecutor
{
    public Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 1)
        {
            context.PrintError("Usage: hello <name>");

            return Task.CompletedTask;
        }

        context.Print($"Hello, {context.Arguments[0]}!");

        return Task.CompletedTask;
    }
}
```

La classe del plugin richiede un costruttore pubblico senza parametri; il loader
la istanzia prima che esista qualsiasi container. In un plugin reale, metti ogni
tipo nel proprio file.

Il `Register` dell'esempio in
[SamplePlugin.cs](../samples/Moongate.Sample.Plugin/SamplePlugin.cs) mostra gli altri
helper affiancati: `AddPersistenceWorld<GreetingNote>()` registra un'entità Realm e
la sua facade dati, `RegisterInstance(new GreetingCounter())` condivide un'istanza
tra un modulo Lua e un provider di metriche, `AddScriptModule<GreeterModule>()` e
`RegisterScriptEnum<Tone>()` pubblicano una tabella e un enum Lua, e
`AddMetricProvider<GreetingMetricProvider>()` aggiunge campioni allo snapshot diagnostico.
La registrazione non si connette mai al database né lo modifica; l'host valida tutte
le registrazioni dei plugin come insieme e controlla la disponibilità dello schema
prima di risolvere qualsiasi servizio di avvio.

I plugin vengono eseguiti nel ruolo configurato del server. Registra entità Auth e
servizi account solo in login/standalone, ed entità World, moduli Lua del mondo e
servizi game solo in game/standalone. La registrazione di persistenza per una
destinazione inattiva fallisce all'avvio; non si connette al database dell'altro ruolo.

### Distribuire SQL versionato con un plugin di persistenza

Un plugin che registra entità con `AddPersistenceAuth<T>()` o
`AddPersistenceWorld<T>()` distribuisce l'SQL revisionato accanto alla propria DLL,
sotto `migrations/` con un `manifest.json` contenente un ID stabile del componente,
e copia quella directory nell'output tramite un elemento
`<Content Include="migrations/**/*">`. Il runner delle migrazioni scopre l'SQL senza
caricare assembly; il core viene eseguito prima, poi i componenti dei plugin in
ordine ordinale. Vedi [File SQL versionati](persistence-migrations.md#versioned-sql-files)
per struttura e regole, e [Creare un'entità persistente](persistence-entity-tutorial.md)
per un percorso eseguibile dalla classe entità alla migrazione applicata.

## Cosa può fare Register

| Helper di registrazione | Cosa registra | Documentato in |
| --- | --- | --- |
| `AddMoongateService<TService, TImpl>(priority)` / `AddMoongateService<TService>(instance)` | Un servizio singleton; se l'implementazione implementa anche `IMoongateStartupService`, si avvia automaticamente alla `priority` indicata e si arresta in ordine inverso. Altri overload accettano factory e tipi runtime | questa pagina |
| `RegisterCommand<TExecutor>(name, description, source, minimumAccountType, descriptionMessage)` | Un esecutore di comandi console/in gioco, come singleton | [Comandi console](#console-commands) |
| `RegisterPacketHandler<TPacket, THandler>()` | Un gestore singleton associato a un tipo di pacchetto in ingresso | questa pagina |
| `RegisterIncomingPacket<TPacket>()` | Un tipo di pacchetto in ingresso aggiunto al registro che l'host costruisce all'avvio, così il server può riconoscere i confini e decodificare l'opcode | [Pacchetti e gestori](packets.md#host-integration) |
| `RegisterAsyncPacketHandler<TPacket, THandler>()` | Un gestore asincrono singleton per I/O; i risultati tornano al game loop tramite `PacketContext` | [Pacchetti e gestori](packets.md#register-a-game-handler) |
| `OnEvent<TEvent>(handler)` | Un'iscrizione `Func<TEvent, CancellationToken, Task>` a un esatto tipo `IMoongateEvent`, mantenuta per la durata del container | questa pagina |
| `AddScriptModule<T>()` / `RegisterScriptEnum<T>()` | Una classe `[ScriptModule]` come singleton, pubblicata in Lua; oppure un enum pubblicato come tabella globale in sola lettura | [Scrivere un modulo Lua](lua-modules.md) |
| `AddMetricProvider<T>()` | Un contributo `IMetricProvider`, singleton, aggiunto al raccoglitore diagnostico | [Registrare un provider di metriche](metric-providers.md) |
| `AddConfig<T>(section)` | La `[section]` propria del plugin in `config/moongate.toml`, associata a `T` e registrata come singleton | [Aggiungere una sezione di configurazione](#add-a-config-section) |
| `AddPersistenceAuth<T>()` / `AddPersistenceWorld<T>()` | Una facade di entità tipizzata per Accounts o Realm, con moduli gestiti internamente (richiede `Moongate.Persistence`) | [Persistenza PostgreSQL](persistence.md) |

`priority` conta solo per un servizio che implementa anche `IMoongateStartupService`:
il bootstrap avvia i servizi per priorità crescente e li arresta al contrario,
quindi un servizio prende una priorità più bassa dei servizi che ne dipendono.
Valori incorporati:

| Priorità | Servizio |
| --- | --- |
| -900 | `TimerWheelService` |
| -800 | `IGameLoopService` (`GameLoopService`) |
| -10 | `IUltimaDataService` (`UltimaDataService`) |
| -5 | `IDataLoaderService` (`DataLoaderService`; game e standalone) |
| -4 | `IMapService` (`MapService`), `IMultiService` (`MultiService`); game e standalone, vedi [File client e query sul mondo](world-queries.md) |
| 0 (predefinito) | `ISessionService`, `IEventBusService`, `IPluginLoaderService`, `ICommandSystemService` e ogni registrazione che omette `priority` |
| 40 | `IWorldSaveService` (`WorldSaveService`), `IConnectionService` |
| 50 | `IPacketSendService` |
| 60 | `IPacketDispatchService` |
| 70 | `IScriptEngine` (`LuaScriptEngineService`) |
| 100 | `IGameServerService` |
| 110 | `IApiServerService` (`ApiServerService`; listener disabilitato per impostazione predefinita) |
| 900 | `IDiagnosticService` (`DiagnosticService`) |
| 1000 | `IConsoleInputService` (`ConsoleInputService`) |

Un plugin che registra il proprio servizio di avvio sceglie una priorità relativa
a questa tabella: dopo `IGameLoopService` (-800) se deve accodare lavoro al loop,
dopo `IScriptEngine` (70) se richiede il motore già associato, e così via. I plugin
si caricano prima dei controlli di persistenza, e la preparazione dello schema di
persistenza termina prima di risolvere questo elenco di servizi di avvio,
indipendentemente dalla priorità di un servizio del plugin.

`RegisterPacketHandler<TPacket, THandler>()` associa un singleton
`IPacketHandler<TPacket>` a un tipo di pacchetto in ingresso, chiamato sincronicamente
sul thread del game loop; un secondo gestore per lo stesso tipo genera un'eccezione
prima dell'avvio. Un pacchetto definito dal plugin richiede anche
`RegisterIncomingPacket<TPacket>()`, perché la sola registrazione del gestore non
aggiunge il suo opcode al registro del protocollo; vedi [Integrazione con l'host](packets.md#host-integration).

`OnEvent<TEvent>(handler)` si iscrive da `Register` al bus degli eventi posseduto
dal container; il gestore viene atteso per ogni `TEvent` pubblicato finché vive il container.

### Aggiungere una sezione di configurazione

Un plugin conserva le proprie impostazioni in una tabella propria di
`config/moongate.toml`. Scrivi una classe con i valori predefiniti, implementa
`IConfigSection` quando alcuni valori non sono consentiti, e aggiungila all'inizio di `Register`:

```csharp
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Config;

public sealed class GreeterConfig : IConfigSection
{
    public string Greeting { get; set; } = "Welcome";

    public GreeterLimits Limits { get; set; } = new(); // [greeter.limits]

    public void Validate()
    {
        if (Limits.MaxPerMinute < 1)
        {
            throw new InvalidOperationException("greeter.limits.max_per_minute must be at least 1.");
        }
    }
}

public void Register(Container container)
{
    var config = container.AddConfig<GreeterConfig>("greeter");
    // Services can now take GreeterConfig in their constructor.
}
```

```toml
[greeter]
greeting = "Welcome"

[greeter.limits]
max_per_minute = 10
```

- I nomi delle proprietà diventano chiavi `snake_case`; una classe annidata è una sottotabella.
- Quando il file non ha la tabella `[greeter]`, il plugin riceve i valori predefiniti
  e il server li **aggiunge** in fondo al file, così l'operatore vede ogni impostazione.
  Il resto del file non viene riscritto: commenti e ordine restano invariati. Se
  il file non può essere scritto, viene registrato un avviso e si usano i valori predefiniti.
- `Validate()` viene eseguito dopo la lettura; un'eccezione interrompe l'avvio.
- Un nome di sezione appartiene a un proprietario: le sezioni proprie del server
  (`network`, `redis`, `persistence`, ...) e un nome già aggiunto da un altro plugin
  interrompono l'avvio con
  `The configuration section [name] is already owned by the server or by another plugin.`
- Una chiave con il nome della sezione che non è una tabella (`greeter = 5`,
  `[[greeter]]`) interrompe l'avvio: aggiungere `[greeter]` accanto definirebbe la chiave due volte.
- Le impostazioni vengono lette una volta, all'avvio; non c'è ricaricamento.

**Come funziona.** All'avvio il server legge `config/moongate.toml` una volta, prende
le proprie sezioni e registra il file analizzato come `ServerConfigDocument` prima
dell'esecuzione di qualsiasi `Register` di plugin. `AddConfig` rivendica il nome
in quel documento, così due proprietari non lo condividono, poi legge la tabella o
scrive i valori predefiniti. Tutto avviene durante la registrazione, prima dell'avvio
di qualsiasi servizio: un valore errato non raggiunge mai un server in esecuzione.

**Usarla in un servizio.** La sezione è un normale singleton, quindi un servizio
la richiede nel costruttore:

```csharp
public sealed class GreeterService
{
    private readonly GreeterConfig _config;

    public GreeterService(GreeterConfig config)
    {
        _config = config;
    }
}
```

Una sottotabella che il plugin vuole distribuire separatamente viene registrata con
`container.RegisterInstance(config.Limits)`; il plugin Ultima lo fa affinché i suoi
servizi ricevano `WorldConfig` o `CharactersConfig` anziché l'intero `UltimaConfig`.

**Testarla.** Un test che chiama direttamente `Register` del plugin registra prima
un `ServerConfigDocument`, come fa il server:

```csharp
var path = Path.Combine(directory, "moongate.toml");
File.WriteAllText(path, "[greeter]\ngreeting = \"Hi\"\n");
container.RegisterInstance(new ServerConfigDocument(path, TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!, []));

new GreeterPlugin().Register(container);

Assert.Equal("Hi", container.Resolve<GreeterConfig>().Greeting);
```

Il plugin Ultima possiede `[ultima]` e il plugin Admin possiede `[admin_api]` in questo modo.

### Eventi del ciclo di vita della persistenza

Iscriviti durante `Register` agli eventi di `Moongate.Server.Core.Data.Events`:

```csharp
container.OnEvent<PersistenceReadyEvent>(async (_, cancellationToken) =>
{
    var items = await container.Resolve<IDataAccess<Item>>()
        .GetAllAsync(cancellationToken);
    // Load plugin state before startup services begin.
});

container.OnEvent<PersistenceStoppedEvent>((_, _) =>
{
    // Persistence is disposed. Release plugin bookkeeping; do not query or save.
    return Task.CompletedTask;
});
```

`Item` rappresenta la tua entità registrata; `IDataAccess<T>` proviene da
`Moongate.Persistence.Interfaces` e `OnEvent<TEvent>` da `Moongate.Server.Core.Extensions`.

| Evento | Tempistica e garanzie |
| --- | --- |
| `PersistenceReadyEvent` | Registrazione dei plugin e inizializzazione della persistenza sono terminate con successo, compresi controlli di connessione, migrazioni e schema. La persistenza è utilizzabile; seguono servizi di avvio e `MoongateStartedEvent`. |
| `PersistenceStoppedEvent` | Un proprietario inizializzato della persistenza è stato rilasciato con successo, dopo l'arresto dei servizi e `MoongateStoppedEvent`, ma prima del rilascio del container. Usa un token non annullabile affinché gli osservatori dell'arresto possano completare. |

Entrambi sono senza payload, attesi ed emessi al massimo una volta per ciclo di
vita del bootstrap. Nulla viene pubblicato quando l'host non ha registrazioni di
persistenza o l'inizializzazione fallisce; un successivo errore di avvio pubblica
comunque `PersistenceStoppedEvent`, un segnale di chiusura, non conferma di un
salvataggio finale del mondo. I gestori vengono eseguiti nell'operazione del ciclo
di vita, non sul game loop; non attendere mai `StartAsync` o `StopAsync` del bootstrap
al loro interno. Il bus degli eventi registra e isola le eccezioni degli osservatori,
quindi la validazione critica di avvio appartiene a un servizio di avvio.

## Comandi console

Per i comandi incorporati `echo`, `help`, `script` e `account`, vedi
[Comandi del server](commands.md).

Un comando è una classe che implementa `ICommandExecutor`, registrata con
`RegisterCommand<T>(name, description, source, minimumAccountType, descriptionMessage)`
come nella [classe del plugin](#the-plugin-class) sopra. `descriptionMessage` è
facoltativo: l'id di un messaggio in `data/messages` mostrato da `help` nella lingua
del server al posto di `description`; un plugin può usare id superiori a quelli
usati da Moongate. Anche i testi del comando possono passare da `ILocalizationService`:
accettalo come parametro facoltativo del costruttore e usa
`localization.Text(id, english, values)`, che usa il testo inglese se manca un messaggio.
`CommandContext` fornisce `Arguments` (i token dopo il nome del comando), `Print` e
`PrintError` (una riga di output ciascuno) e il `CancellationToken` dell'invocazione.

Un comando può implementare anche `ICommandArgumentCompleter` per completare gli
argomenti con TAB nella console: `GetArgumentCompletions(previousArguments)` dà i
valori che può assumere l'argomento in digitazione, dopo gli argomenti già digitati
(`[]` per il primo), oppure nessuno. Gira sul thread della console, quindi restituisce
liste fisse o nomi letti dal disco, mai stato di gioco; un'eccezione viene registrata
e non completa nulla, e un valore vuoto o contenente uno spazio viene scartato,
poiché il parser dei comandi lo separerebbe.

```csharp
public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
{
    return previousArguments.Count == 0 ? ["on", "off"] : [];
}
```

`CommandSourceType` è un enum `[Flags]` (`InGame`, `Console`), quindi un comando può
servire entrambe le sorgenti combinandole con OR, come `echo` incorporato.
`AccountType` è `Regular`, `GameMaster`, `Administrator` in ordine crescente;
un'invocazione console è sempre trattata come `Administrator`, quindi il minimo
conta solo per lo stesso comando raggiunto da `InGame`, dove viene controllato
il tipo di account della sessione chiamante.

**I comandi vengono eseguiti sul thread che ha chiamato il sistema dei comandi,
mai sul thread del game loop.** Un comando che legge o modifica stato di gioco
deve accodare il proprio elemento di lavoro a `IGameLoopService` e attenderne
l'esito prima di scrivere il risultato tramite `CommandContext`. Il comando
incorporato `script reload` (`src/Moongate.Server/Commands/ScriptCommand.cs`) è il
modello di riferimento. Il [GreetCommand](../samples/Moongate.Sample.Plugin/Commands/GreetCommand.cs)
dell'esempio mostra la validazione degli argomenti, compreso il controllo
`Enum.IsDefined` che impedisce a una stringa numerica come `"7"` di diventare un
membro enum non definito.

Il plugin Ultima incorporato registra il comando `account`:
`account create <username> <password> [Regular|GameMaster|Administrator]`.
Il livello predefinito è `Regular`; il prompt interattivo maschera il token della
password e il comando attende `IAccountService.CreateAccountAsync` prima di riportare
un esito. La registrazione permette anche amministratori in gioco, anche se nessun
input in gioco è ancora collegato. Il percorso futuro di input deve proteggere
la password come fa la console.

## Deployment e caricamento

Compila il progetto del plugin in Release e copia il suo output, tutto sotto
`bin/Release/net10.0/` e non solo la DLL di ingresso, in `<root>/plugins/<BundleName>/`
sul server di destinazione. `<root>` viene risolto all'avvio da `--root-directory`,
poi `MOONGATE_ROOT`; senza entrambi il server rifiuta l'avvio.

Un bundle è una directory sotto `<root>/plugins/`; il suo nome è anche quello che
il loader si aspetta per l'assembly di ingresso. Un bundle in `plugins/mymod/` deve
contenere `mymod.dll`, il proprio `mymod.deps.json` e ogni dipendenza privata non già
distribuita dall'host. I bundle si caricano in ordine ordinale dei nomi delle directory;
l'ordine di registrazione dei plugin è deciso dall'ordinamento delle dipendenze,
non dall'ordine di caricamento.

Ogni bundle riceve un `AssemblyLoadContext` rilasciabile. La sua regola per ogni
assembly referenziato dal bundle:

1. `Moongate.Core`, `Moongate.Server.Core`, `Moongate.Persistence`,
   `Moongate.Persistence.Migrations`, `FreeSql`, `FreeSql.Provider.PostgreSQL` e
   `Npgsql` si risolvono sempre dall'host, e versione, cultura e token di chiave
   pubblica referenziati dal plugin devono corrispondere esattamente alla copia
   dell'host, più vecchia o nuova che sia. Compila con la versione esatta del
   pacchetto distribuita dall'host di destinazione.
2. Ogni altro assembly prova prima il contesto di caricamento dell'host. Tutto ciò
   che l'host distribuisce (altri assembly `Moongate.*`, Serilog, DryIoc, LuaCSharp
   e così via) si risolve lì purché la versione referenziata dal plugin non sia
   più nuova di quella dell'host. Una copia nel bundle di tale assembly è peso
   inutile, mai una sostituzione; un riferimento più nuovo è trattato come non
   trovato e, con `ExcludeAssets="runtime"`, il bundle non si carica.
3. Solo un assembly assente dall'host passa al bundle: prima il resolver
   `.deps.json`, poi una `.dll` con lo stesso nome accanto all'assembly di ingresso.
   Due bundle con le proprie copie di una libreria ricevono due istanze separate
   del suo stato statico.

## Errori che impediscono l'avvio

Ogni errore impedisce l'avvio con una `InvalidOperationException` identificata.
Il caricamento di un bundle racchiude qualsiasi errore come
`Failed to load plugin bundle '{path}'.` con il problema originale come `InnerException`:

| Messaggio | Causa |
| --- | --- |
| `The plugin entry assembly is missing.` | La directory del bundle non contiene una DLL con il proprio nome |
| `The assembly contains no public concrete IMoongatePlugin types.` | Nessun tipo plugin nell'assembly di ingresso |
| `Plugin '{type}' needs a public parameterless constructor.` | La classe del plugin non può essere istanziata |
| `Required host persistence contract '{assembly}' is unavailable; private copies are not supported.` | Un assembly con identità controllata manca nell'host |
| `Incompatible host persistence contract '{assembly}'; host provides '{identity}'. Private copies are not supported.` | Il plugin è stato compilato contro una versione diversa di un assembly con identità controllata |
| eccezione interna `FileNotFoundException` | Una dipendenza più nuova della copia dell'host, senza copia privata nel bundle |

Il registro valida l'intero insieme prima di eseguire qualsiasi `Register`; un insieme
rifiutato lascia intatti i plugin già registrati:

| Messaggio | Causa |
| --- | --- |
| `Plugin ID '{id}' is already registered or duplicated.` | Due plugin condividono un ID, compreso un plugin su disco che collide con uno registrato |
| `Plugin '{id}' requires missing plugin '{dependency}'.` | Una dipendenza nomina un ID non fornito da nulla |
| `Plugin '{id}' requires '{dependency}' >= {minimum}; found {version}.` | Il plugin disponibile è più vecchio di `minimumVersion` |
| `Plugin dependency cycle: first -> second -> first.` | Un ciclo nel grafo delle dipendenze, stampato come percorso che lo ha chiuso |
| `Plugin '{id}' failed during registration.` | Il `Register` del plugin ha generato un'eccezione, compresa una sezione di configurazione non valida o già posseduta; l'eccezione è l'`InnerException` |

## Testare un plugin

Testa tramite il vero loader: distribuisci il bundle compilato sotto una directory
temporanea `plugins/<name>/`, registra in un `Container` i servizi host necessari
al plugin, aggiungi `PluginLoaderService` e avvia un `MoongateServerBootstrap`.
Poi risolvi ciò che `Register` ha aggiunto ed esercitalo: chiama Lua tramite il game
loop, esegui comandi tramite `CommandSystemService`, raccogli uno snapshot diagnostico.
[tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs](../tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs)
fa esattamente questo per l'esempio ed è il modello da copiare. Per unit test che
saltano il disco, costruisci `new MoongatePluginRegistry(container)`, chiama
`Register(plugin)` con un `IMoongatePlugin` in memoria e risolvi ciò che ha aggiunto
dallo stesso container.

## Errori comuni

- **`EnableDynamicLoading` mancante.** Una dipendenza non distribuita dall'host non
  raggiunge mai il bundle; vedi [Creare il progetto](#creating-the-project).
- **Distribuire assembly dell'host nel bundle.** La copia dell'host prevale sempre,
  quindi un `Moongate.*.dll` incluso è peso inutile; vedi
  [Deployment e caricamento](#deployment-and-loading).
- **Eseguire lavoro, I/O o avviare thread in `Register`.** `Register` registra solo;
  nulla può ancora avviarsi; vedi [Il contratto](#the-contract).
- **Registrare un oggetto legato al loop e accedervi da un comando senza accodare
  il lavoro.** I comandi girano sul thread del chiamante, mai sul game loop; vedi
  [Comandi console](#console-commands).
- **Una classe `[ScriptModule]` con una dipendenza nel costruttore non registrata
  prima dell'avvio.** Il motore risolve l'istanza solo quando si avvia; vedi
  [Scrivere un modulo Lua](lua-modules.md#registering).

## Plugin di amministrazione incorporato

`MoongateAdminPlugin` è registrato in `Program.cs` dopo il plugin Ultima obbligatorio.
È distribuito con il server e non viene caricato da `plugins/`. Il listener HTTP/2
facoltativo usa la configurazione `[admin_api]` e i servizi esistenti posseduti
dall'host. Vedi [API di amministrazione](admin-api.md).
