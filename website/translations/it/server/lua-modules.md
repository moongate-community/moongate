<!-- translation: {"sourceHash":"9f628b87200f8605698ed0dd53178cb9b76ba40cfa7bfcbc0b627535df07145c","title":"Scrivere un modulo Lua"} -->

# Scrivere un modulo Lua

Per creare contenuti Lua con i moduli esistenti, parti da
[Scrivere script Lua](scripting.md). Questa guida estende i binding dell'host in C#.

Un modulo Lua è una normale classe C# marcata con `[ScriptModule("name", "help text")]`. Il motore associa ogni modulo registrato una volta, all'avvio, sul thread del ciclo di gioco, e lo pubblica nel `LuaState` attivo come tabella globale di sola lettura con il nome dell'attributo. In quella tabella:

- ogni metodo di istanza pubblico e non generico marcato `[ScriptFunction]` diventa un campo chiamabile;
- ogni membro statico marcato `[ScriptConstant]` diventa un campo valore di sola lettura;
- ogni enum citato da una firma associata o da una costante — o registrato esplicitamente — viene pubblicato come propria tabella globale di sola lettura che mappa i nomi dei membri ai numeri.

Un modulo viene registrato dal metodo `Register(Container container)` di un plugin oppure, per un modulo appartenente all'host stesso, direttamente nell'host (`src/Moongate.Server/Bootstrap/Internal/ServerRoleRegistration.cs` registra allo stesso modo il modulo integrato `log`, con `.AddScriptModule<LogModule>()`). La registrazione annota solo il tipo; non viene eseguita reflection e non esiste alcuna tabella Lua finché il motore degli script non si avvia e la associa.

Gli script vengono eseguiti in un ambiente isolato: `io`, `os` e `debug` non vengono mai aperte, `dofile`, `loadfile` e `rawset` vengono rimossi e `print` è reindirizzato al log del server invece della console. Anche `string.rep` rifiuta di costruire un risultato oltre un limite configurato. Vedi la [sezione "Isolamento" del README del package](../src/Moongate.Scripting/README.md#sandbox) per l'elenco completo.

## Il modulo di esempio

L'enum `Tone` del plugin di esempio, da `samples/Moongate.Sample.Plugin/Types/Tone.cs`:

```csharp
namespace Moongate.Sample.Plugin.Types;

/// <summary>How warmly the greeter speaks. Published to Lua as the <c>Tone</c> table.</summary>
public enum Tone
{
    /// <summary>"Hello, name!"</summary>
    Plain = 0,

    /// <summary>"Hello there, name!"</summary>
    Warm = 1,

    /// <summary>"Good day, name."</summary>
    Formal = 2
}
```

Il modulo che lo accetta, da `samples/Moongate.Sample.Plugin/Modules/GreeterModule.cs`:

```csharp
using Moongate.Sample.Plugin.Internal;
using Moongate.Sample.Plugin.Types;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Sample.Plugin.Modules;

/// <summary>The <c>greeter</c> Lua module: one function, one constant, and the <see cref="Tone"/> enum it takes.</summary>
[ScriptModule("greeter", "Greets whoever asks.")]
public sealed class GreeterModule
{
    private readonly GreetingCounter _counter;

    /// <summary>The greeting used when no tone is given; visible to scripts as <c>greeter.DEFAULT_GREETING</c>.</summary>
    [ScriptConstant("DEFAULT_GREETING", "The plain greeting word.")]
    public static readonly string DefaultGreeting = "Hello";

    /// <summary>Initializes a new instance of the <see cref="GreeterModule"/> class.</summary>
    /// <param name="counter">Shared with the metric provider, which reports how often <see cref="Hello"/> ran.</param>
    public GreeterModule(GreetingCounter counter)
    {
        _counter = counter;
    }

    /// <summary>Builds a greeting for <paramref name="name"/> in the given <paramref name="tone"/>; scripts call it as <c>greeter.hello(name, tone)</c>.</summary>
    [ScriptFunction(helpText: "Returns a greeting for name, in the given tone.")]
    public string Hello(string name, Tone tone = Tone.Plain)
    {
        _counter.Increment();

        return tone switch
        {
            Tone.Warm => $"Hello there, {name}!",
            Tone.Formal => $"Good day, {name}.",
            _ => $"{DefaultGreeting}, {name}!"
        };
    }
}
```

Uno script che lo usa:

```lua
greeting = greeter.hello('Moongate', Tone.Warm)
function report() return greeting, greeter.DEFAULT_GREETING end
```

All'avvio il motore scrive `definitions.lua` per il completamento nell'editor. Il file completo inizia con un'intestazione `---@meta` e la funzione integrata `wait`, e da un server attivo dichiara anche i moduli integrati `engine`, `timer`, `events` e `log`, quelli del plugin Ultima `dice`, `localization`, `npc`, `item`, `mobile`, `world`, `target`, `hue_picker`, `prompt`, `skill`, `combat`, `gump`, `bank`, `stable`, `mount`, `pet`, `vendor`, `trainer`, `effect`, `moongates` e `locations`, e qualsiasi altro elemento registrato. L'estratto sotto è ciò che producono il modulo di esempio e il suo enum:

```lua
---@enum Tone
Tone = {
    Plain = 0,
    Warm = 1,
    Formal = 2,
}

---Greets whoever asks.
---@class greeter
---@field DEFAULT_GREETING string
greeter = {}
greeter.DEFAULT_GREETING = "Hello"

---Returns a greeting for name, in the given tone.
---@param name string
---@param tone? Tone|string
---@return string
function greeter.hello(name, tone) end
```

Nota l'annotazione del parametro: `tone? Tone|string` — `?` perché `tone` ha un valore predefinito C#, `Tone|string` perché il convertitore accetta in input il numero dell'enum oppure il nome di un membro. Una funzione che *restituisce* un enum sarebbe annotata con il solo nome dell'enum — `@return Tone`, non `Tone|string` — perché il motore restituisce sempre al chiamante il numero del membro, mai il suo nome.

## Funzioni

`[ScriptFunction]` è valido solo su un **metodo di istanza pubblico e non generico**. Un metodo statico, non pubblico o generico che lo contiene è un errore di binding, non una funzione silenziosamente mancante: `"{Module}.{Method}: a [ScriptFunction] must be a public, non-generic instance method."`

Il nome Lua è l'argomento `name` dell'attributo quando fornito (deve essere un identificatore Lua minuscolo; l'attributo rifiuta altro), oppure il nome del metodo convertito in snake_case: ogni sequenza di lettere maiuscole inizia una nuova parola minuscola separata da underscore, quindi `NextColour` diventa `next_colour`.

Parametri e risultati vengono convertiti rigorosamente, senza coercizione tra tipi, e per `int`/`long` vengono verificati intervallo e valore intero:

| Tipo C# | Accetta da Lua |
| --- | --- |
| `bool` | booleano |
| `int`, `long` | numero intero nell'intervallo |
| `double`, `float` | qualsiasi numero |
| `string` | stringa |
| un enum | numero sottostante (deve essere un membro definito) o nome del membro (sensibile alle maiuscole) |
| un tipo valore nullable (`int?`, `Tone?`, …) | quanto sopra, oppure `nil` Lua (diventa `null` C#) |
| `LuaValue` | qualsiasi cosa, non convertita, incluso `nil` |
| `LuaTable` | tabella |
| `object` | numero (come `double`), stringa, booleano, tabella o `nil` (diventa `null` C#), estratti; tutto il resto resta un `LuaValue` |
| array `params` | ogni argomento rimanente, convertito uno per uno nel tipo degli elementi dell'array |

Un argomento errato genera un errore Lua della forma `bad argument #{n} to '{module}.{function}' ({reason})`, dove il motivo indica il tipo atteso e ciò che è stato ricevuto, per esempio `Int32 expected, got string`, `nil cannot be converted to Int32`, `{number} is out of range for Int32` oppure `'{name}' is not a member of Tone`. `LuaValue`, `object` e gli elementi `params` correttamente tipizzati non falliscono mai.

Lua accetta il nome di un membro enum distinguendo le maiuscole; il parsing del tono di `GreetCommand` sul lato console è deliberatamente più permissivo (vedi [Scrivere un plugin: comandi della console](plugins.md#console-commands)). Sono due scelte indipendenti per chiamanti diversi.

Un parametro è facoltativo quando il metodo C# gli assegna un valore predefinito, esattamente come `Hello(string name, Tone tone = Tone.Plain)` sopra: se lo script omette l'argomento viene usato il valore predefinito; se il parametro è obbligatorio e lo script lo omette, l'errore è `"bad argument #{n} to '{module}.{function}' ({parameter} is required)"`.

I tipi restituiti vengono convertiti con la stessa tabella. `void` non restituisce nulla a Lua. Le tuple non sono supportate: un metodo che restituisce `System.ValueTuple` non viene associato, con lo stesso errore `"return type {Type} cannot be bound"` descritto in [Registrazione](#registering), generato prima dell'esecuzione di qualsiasi script. Una funzione associata restituisce sempre al massimo un valore; uno script che vuole più risultati definisce una propria funzione Lua intorno alla chiamata, come `report()` nell'esempio sopra.

Un'eccezione generata dal metodo stesso diventa un errore Lua intercettabile con `pcall`: il binder estrae `TargetInvocationException` e genera `"'{module}.{function}' failed: {innerException.Message}"`.

Ogni funzione associata viene eseguita sul thread del ciclo di gioco — il binder verifica una protezione del thread prima di ogni chiamata — quindi non deve bloccare, dormire o attendere nulla; vedi [Errori comuni](#common-mistakes).

## Costanti ed enum

`[ScriptConstant]` ha effetto solo su un **campo public static readonly** o una **proprietà public static con solo getter**. Qualsiasi altra cosa, come un membro di istanza, non pubblico o una proprietà con setter, è un errore di binding: `"{Module}.{Member}: a [ScriptConstant] must be a public static readonly field or a public static get-only property."`

I tipi di costanti supportati sono `int`, `long`, `double`, `float`, `bool`, `string` ed enum — meno di quanto accettino parametri o risultati delle funzioni: `LuaTable`, `LuaValue` e `object` sono esplicitamente esclusi anche se altrimenti il convertitore li supporta. L'errore di incompatibilità indica membro e tipo: `"{Module}.{Member}: constants of type {Type} are not supported; use int, long, double, bool, string or an enum."` Anche un getter che genera un'eccezione è un errore di binding, indicando il membro e conservando l'eccezione originale come causa: `"{Module}.{Member}: the constant's getter threw {ExceptionType}: {Message}"`.

Ogni tabella di modulo è un proxy di sola lettura: le letture passano alla tabella reale, ma qualsiasi assegnazione — a una chiave esistente o nuova — genera `"'{name}' is read-only"`, e anche `setmetatable` su di essa genera un errore, perché `__metatable` è bloccato. `rawset`, l'unica chiamata che potrebbe altrimenti aggirarlo, viene completamente rimosso dall'ambiente isolato.

Un enum viene pubblicato come tabella globale di sola lettura con il nome del tipo, indicizzata per nome del membro con il valore numerico del membro (`Tone.Plain == 0` e così via). Succede automaticamente la prima volta che il tipo di un parametro o risultato di una funzione associata, o di una costante, è quell'enum — oppure esplicitamente tramite `container.RegisterScriptEnum<TEnum>()`, che lo pubblica anche se nient'altro lo cita (`SamplePlugin.cs` lo fa per `Tone`, anche se `GreeterModule.Hello` causa già l'individuazione automatica). Uno script può passare il numero sottostante del membro o il nome con maiuscole esatte a un parametro di quel tipo enum; il motore restituisce sempre il numero. `definitions.lua` rende ciascuno come `---@enum Name` seguito da una tabella letterale, come mostrato sopra per `Tone`.

Il nome della tabella pubblicata è esattamente il nome del tipo enum C#, senza qualificatori: uno script scrive `Tone.Warm`.

## Registrazione

Il plugin di esempio registra il modulo e il suo enum con queste due righe da `samples/Moongate.Sample.Plugin/SamplePlugin.cs`:

```csharp
container.AddScriptModule<GreeterModule>();
container.RegisterScriptEnum<Tone>();
```

`AddScriptModule<TModule>()` fa due cose: registra `TModule` nel container come `Reuse.Singleton` e annota il tipo nell'`IScriptModuleRegistry` letto dal motore all'avvio. Nessuna delle due azioni tocca Lua — il motore risolve ogni tipo registrato dal container e lo associa solo quando si avvia. Poiché il modulo è un singleton del container, qualsiasi altra classe che lo riceve come dipendenza del costruttore risolve la stessa istanza: al `GreetCommand(GreeterModule greeter)` dell'esempio viene consegnato lo stesso `GreeterModule` associato dal motore degli script, quindi comando console e script Lua condividono lo stato. Le dipendenze del costruttore del modulo — qui `GreetingCounter` — devono essere già registrate nel container quando il motore si avvia, perché la risoluzione avviene allora, non al momento di `AddScriptModule`; l'esempio la registra prima con `container.RegisterInstance(new GreetingCounter())`.

`RegisterScriptEnum<TEnum>()` aggiunge solo la voce del registro; non c'è un singleton DI da creare, perché un enum viene esaminato tramite reflection, non risolto.

Il binding viene eseguito una volta all'avvio, e ogni errore viene segnalato tramite le eccezioni già elencate sopra:

- un nome Lua duplicato tra due funzioni, o tra una funzione e una costante, nello stesso modulo: `"{Module}.{Member}: Lua name '{name}' is already used in module '{module}'."`
- un `[ScriptFunction]` su un metodo statico, non pubblico o generico: `"{Module}.{Method}: a [ScriptFunction] must be a public, non-generic instance method."`
- un parametro o tipo restituito da una funzione che il convertitore non può associare: `"{Module}.{Method}: parameter '{parameter}' of type {Type} cannot be bound."` / `"{Module}.{Method}: return type {Type} cannot be bound."`
- un `[ScriptConstant]` che non è un campo public static readonly o una proprietà public static con solo getter, oppure ha un tipo non supportato (vedi [Costanti ed enum](#constants-and-enums) per i messaggi esatti).

Vedi [Scrivere un plugin: registrare moduli Lua](plugins.md#what-register-may-do) per dove `Register` si colloca nel ciclo di vita del plugin.

## Nella guida di riferimento API

La [guida di riferimento API Lua](https://moongate.sh/lua/) del sito viene generata dagli stessi attributi
di `definitions.lua`. Un modulo registrato dal server distribuito, in `AddUltimaScriptModules` o in
`Moongate.Scripting`, ottiene una pagina senza nulla da scrivere a mano: la pagina mostra il
testo di aiuto di `[ScriptModule]`, e ogni funzione mostra firma Lua, `helpText` e
parametri con i rispettivi valori predefiniti. Quindi entrambi i testi sono obbligatori per quei moduli: la build del sito
fallisce per un modulo senza testo di aiuto o una funzione senza `helpText`. Un modulo registrato da un
plugin tuo non viene elencato sul sito; le voci in `definitions.lua` vengono comunque scritte.

Per mostrare un esempio sotto una funzione di un modulo fornito, aggiungi una sezione `## <function>` a
`website/lua/examples/<module>.md`; vedi [Scrivere documentazione](documentation.md).

Una callback viene accettata come `LuaValue`, che editor e sito mostrerebbero come `any`. Marcala
`[ScriptParameterType("function")] LuaValue callback`, come fa `timer.after`, così entrambi mostrano
`function`.

`LuaModuleDescriber.Describe(typeof(GreeterModule))` fornisce la stessa descrizione senza
costruire il modulo, e `LuaFunctionDescriber.Describe(function)` fornisce parametri
e risultato di una funzione in termini Lua, se vuoi generare documentazione per i tuoi moduli.

## Test senza un server

Il percorso minimo non richiede server: apri le librerie necessarie a un modulo, associalo con
`LuaModuleBinder` e `NoThreadGuard.Instance` (una protezione che non rifiuta mai un chiamante,
per host senza ciclo di gioco), poi esegui direttamente un blocco. L'
[esempio del README del package](../src/Moongate.Scripting/README.md#example) mostra esattamente
questo con un modulo di saluto a una funzione.

Per l'intera gamma di conversioni, errori e casi limite (`params`,
enum per numero o nome, interi fuori intervallo, proxy di sola lettura, nomi duplicati),
vedi i test del binder sotto `tests/Moongate.Tests/Scripting/Binding/`, che associano allo stesso modo i
moduli fixture in `tests/Moongate.Tests/TestSupport/Scripting/`.

Per provare un modulo attraverso l'intero stack (caricamento dei plugin, DI, ciclo di gioco e
`definitions.lua` generato), `tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs`
avvia un vero `MoongateServerBootstrap` con directory temporanee per plugin e
script, poi chiama Lua sul thread del ciclo, esegue il comando console che
condivide il modulo e legge la metrica registrata.

## Errori comuni

- **Restituire `Task` o `ValueTask`.** I metodi dei moduli vengono eseguiti sincronicamente nella chiamata Lua associata; un tipo di risultato asincrono non è supportato, quindi il binding fallisce all'avvio con `"{Module}.{Method}: return type {Type} cannot be bound."`
- **Accettare un parametro classe o interfaccia.** Solo i tipi della tabella [Funzioni](#functions) vengono convertiti; altro — una classe personalizzata, un'interfaccia, una collezione — fallisce nel binding con `"{Module}.{Method}: parameter '{parameter}' of type {Type} cannot be bound."`
- **Aspettarsi `__tostring` o metatable sulla tabella del modulo.** Il binder blocca la metatable della tabella (`__metatable = "locked"`); `setmetatable(mymodule, {})` genera un errore Lua e non viene installato alcun hook `__tostring`.
- **Aspettarsi che la tabella sia scrivibile.** Ogni assegnazione — anche a un nome esistente — genera `"'{name}' is read-only"`, e `rawset`, il modo usuale per aggirare una metatable, viene rimosso dall'ambiente isolato.
- **Chiamare il motore da un altro thread.** Ogni funzione associata verifica prima una protezione del thread; chiamarne una fuori dal thread del ciclo di gioco genera `"{member} must be called on the game loop thread. Post a work item to the loop instead."`
