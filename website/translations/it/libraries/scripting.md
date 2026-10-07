<!-- translation: {"sourceHash":"2030a44a1e63df0da53d92d6708df2e772ea8af6e609ee33fd7039c3e5809d46","title":"Moongate.Scripting"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Scripting

Lua 5.2 integrato per Moongate: moduli C# associati tramite attributi, script eseguiti sul ciclo di gioco, coroutine sospese sulla
ruota dei timer, un budget di istruzioni per ripresa e definizioni per l'editor generate dai binding.

## Installazione

Richiede .NET 10. Usa la versione del package disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Scripting
```

## Funzionalità

- `[ScriptModule]`, `[ScriptFunction]` e `[ScriptConstant]` pubblicano una classe C# in Lua tramite una tabella di sola lettura.
- `IScriptEngine` carica file, chiama funzioni globali (`Call`) e funzioni di una tabella globale (`CallMember(owner, table,
  function, args)`, la cui coroutine appartiene a `owner` e che restituisce `ScriptResultKind.Missing` senza segnalazioni
  quando la tabella o la funzione è assente), invalida file per il ricaricamento e riporta metriche.
- `wait(seconds)` sospende uno script e la ruota dei timer lo riprende sul ciclo di gioco.
- `AddScriptEvent<TEvent>(name, map)` pubblica un evento del bus in Lua; gli script si iscrivono con `events.on(name, fn)` e
  ogni handler viene eseguito come coroutine sul ciclo di gioco.
- Le riprese della VM hanno un budget di istruzioni; i binding C# bloccanti e la memoria totale non sono limitati da esso.
- `definitions.lua` e `.luarc.json` vengono scritti all'avvio per il completamento nell'editor.
- `LuaModuleDescriber` e `LuaFunctionDescriber` descrivono un modulo e le sue funzioni dal solo tipo, in termini
  Lua, per documentazione generata senza avviare il motore.

## Isolamento

Gli script vedono le librerie base, `string`, `table`, `math`, `coroutine` e `package`. `io`, `os` e `debug` non vengono mai
aperte, e il motore rimuove il resto di ciò che oltrepassa la directory degli script o il budget di istruzioni: `dofile`,
`loadfile` e `rawset`; `package.searchpath`, `package.path`, `package.cpath`, `package.loadlib` e la seconda voce
`package.searchers` del runtime, che risolve `package.path` sul filesystem host indipendentemente dal loader dei moduli; e
`coroutine.create`, `coroutine.wrap` e `coroutine.resume`, i cui thread non avrebbero né l'hook del budget né il suo
token di annullamento. `coroutine.yield` resta, così `wait(seconds)` continua a funzionare. `require` risolve quindi solo sotto la
directory degli script. `print` viene sostituito da una funzione che unisce gli argomenti con tabulazioni, come fa Lua, e li scrive nel
log del server a livello Information sotto lo script chiamante, così l'output degli script non aggira mai le destinazioni configurate.

La memoria è limitata solo in parte. `string.rep` rifiuta un risultato più lungo di `MaxStringLength` (16.777.216 caratteri per impostazione predefinita,
`max_string_length` nella sezione `[scripting]` del server; qui le stringhe Lua sono UTF-16, quindi sono 32 MiB di testo) con un
errore di script, conteggiato nelle metriche come raggiungimento del limite delle stringhe. Tutto il resto alloca liberamente sotto il budget di istruzioni: un
costruttore di tabella, o un ciclo che raddoppia una stringa con `..`, può costruire molto più di quanto il budget suggerisca prima di essere
arrestato, e non c'è un limite alla memoria totale conservabile da uno stato.

## Esempio

Associa un modulo a uno stato Lua e chiamalo. Funziona senza un server Moongate; all'interno del server il servizio del motore esegue il
binding e impone il thread del ciclo.

<!-- nuget-smoke:Program.cs -->

```csharp
using Lua;
using Lua.Standard;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Binding;

using var state = LuaState.Create();
state.OpenBasicLibrary();
new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new GreeterModule());

var result = await state.DoStringAsync("return greeter.hello('Moongate')", "readme", default);
Console.WriteLine(result[0].Read<string>());

[ScriptModule("greeter", "Says hello.")]
public sealed class GreeterModule
{
    [ScriptFunction]
    public string Hello(string name)
    {
        return "Hello, " + name + "!";
    }
}
```

Vedi [Scrivere script Lua](https://moongate.sh/server/scripting/)
per esempi di bootstrap/moduli, appartenenza dei timer, eventi, ricaricamento e supporto all'editor.

## Dipendenze e ambito

Questo package dipende da `Moongate.Core`, `Moongate.Server.Core`, `LuaCSharp` e `Serilog`. Non avvia un ciclo di gioco o
timer; l'host li fornisce e registra il motore con
`AddMoongateService<IScriptEngine, LuaScriptEngineService>`.

## Licenza e sorgenti

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
