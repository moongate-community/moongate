<!-- translation: {"sourceHash":"34aaa672e77d1746e93a01305474763a32329013ab5f42ba5f4a71e2b3df759d","title":"Scrivere script Lua"} -->

# Scrivere script Lua

Inserisci gli script sotto `<root>/scripts`. Il bootstrap predefinito è `init.lua`, selezionato
da `[scripting].bootstrap_file` nella [configurazione del server](server-configuration.md).
L'host usa Lua 5.2 tramite LuaCSharp; esecuzione e riprese avvengono sul ciclo
di gioco. Non serve un'installazione Lua separata.

## Primo script e moduli

Crea `scripts/common/greeting.lua`:

```lua
local greeting = {}

function greeting.for_name(name)
    return "Welcome, " .. name
end

return greeting
```

Crea `scripts/init.lua`:

```lua
local greeting = require("common.greeting")
log.info("{Message}", greeting.for_name("Moongate"))
log.info("{Engine} {Version} ({Codename}) on {Platform}",
    engine.name, engine.version, engine.codename, engine.platform)

local pulse = timer.every(30, function()
    log.info("Pulse")
    wait(2)
    log.info("Pulse resumed")
end)

timer.after(95, function()
    log.info("Pulse cancelled: {Cancelled}", timer.cancel(pulse))
end)
```

Avvia il server o inserisci `script reload init.lua` nella console. `require` mappa
i nomi dei moduli con punti a percorsi `.lua` relativi: `common.greeting` carica
`common/greeting.lua`. Conserva in cache il risultato del modulo. La risoluzione resta nella
directory degli script, inclusi controlli contro link simbolici che escono da quella root.
Un bootstrap mancante registra un avviso e avvia un motore vuoto. Un bootstrap
esistente che fallisce compilazione/esecuzione interrompe l'avvio del server.

## Cosa possono chiamare gli script

La [guida di riferimento API Lua](https://moongate.sh/lua/) ha una pagina per ogni modulo, con firma,
parametri e tipo restituito di ogni funzione; viene generata dal codice del server.

L'host predefinito registra `log`; il motore fornisce `engine`, `timer`, `events` e `wait`.
Il plugin Ultima registra `dice`, `localization`, `npc`, `item`, `mobile`, `world`, `target`, `prompt`, `gump`, `bank`, `effect`, `moongates`, `locations`, `jail`, `board` e `commands` nelle modalità game e standalone.
I livelli di log seguono comunque la politica di logging dell'host, quindi una chiamata `log.debug` può non
apparire nell'output predefinito della console. Usa template invece di concatenare
valori variabili nei messaggi.

I moduli `npc`, `item`, `mobile`, `effect`, `world`, `target`, `prompt`, `bank` e `gump` servono agli [script dei mobile](scripting/mobile-scripts.md) e
[degli oggetti](scripting/item-scripts.md). Uno script legge e scrive i numeri e le abilità di un mobile e
verifica un mobile in un'abilità con `skill.check`, che può incrementarla (vedi [Abilità](skills.md)). Per esporre comportamento
applicativo, associa un modulo C# usando [Scrivere un modulo Lua](lua-modules.md).

Alcuni moduli hanno una pagina di approfondimento:

| Modulo | Lettura |
| --- | --- |
| `events`, `timer` | [Eventi e timer](scripting/events.md) |
| `npc` | [Script dei mobile](scripting/mobile-scripts.md), con [camminare lungo un percorso](scripting/mobile-scripts.md#walking-a-path) |
| `item` | [Script degli oggetti](scripting/item-scripts.md) |
| `effect` | [Effetti](scripting/effects.md) |
| `gump` | [Gump](gumps.md), con [gump costruiti in Lua](gumps.md#gumps-built-in-lua), e [Il tuo primo gump](gump-tutorial.md) |
| `bank` | [Banca](bank.md) |
| `dice` | Le forme di [DiceSpec](toml-types.md#dicespec) |
| `localization` | [Leggere un messaggio da Lua](localization.md#read-a-message-from-lua) |
| `locations` | [Luoghi](data-files/locations.md) e il [comando `go`](commands/go.md) |
| `moongates` | [Moongate](data-files/moongates.md) |
| `jail` | [Prigione](jail.md) e il [comando `jail`](commands/jail.md) |
| `board` | [Bacheche](bulletin-boards.md) |
| `commands` | [Comandi](commands.md): `commands.execute` ne esegue uno come console, `commands.execute_as` come giocatore |

## Cosa leggere dopo

- [Lua in Moongate](scripting/lua-in-moongate.md): versione Lua, librerie disponibili a uno script e differenze
  rispetto al manuale.
- [Eventi e timer](scripting/events.md): eventi del server, `wait` e timer ripetuti.
- [Script dei mobile](scripting/mobile-scripts.md): funzioni definibili nello script di un NPC e camminata lungo un percorso.
- [Script degli oggetti](scripting/item-scripts.md): funzioni definibili nello script di un oggetto e come rifiutare uno spostamento.
- [Script forniti](scripting/shipped-scripts.md): cosa fa ogni script della distribuzione: mostri, porte,
  luci, cibo, teletrasporti, moongate, orologi e contenitori che si riempiono.
- [Effetti](scripting/effects.md): opzioni del modulo `effect`.
- [Ricaricamento, budget ed editor](scripting/runtime.md): ricaricare uno script, budget di istruzioni e
  completamento nell'editor.
- [Guida di riferimento API Lua](https://moongate.sh/lua/): ogni funzione, con la firma e, per molte, un
  esempio.
