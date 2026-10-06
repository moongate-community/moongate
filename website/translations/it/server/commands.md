<!-- translation: {"sourceHash":"3ca6d01ab695b3d2999eab6d2aa77f3b358dd456f6184061351f6eae3237612e","title":"Panoramica"} -->

# Comandi del server

Moongate accetta comandi tramite la console interattiva del server. Premi `*` per
sbloccare il prompt dopo l'avvio; [`console lock`](commands/console.md) lo blocca di nuovo. Nella
console, TAB completa il nome del comando e poi gli argomenti con valori fissi, come
`account create` e i suoi livelli, `script reload` e i file `.lua`, `help` e i nomi dei
comandi (un secondo TAB elenca ciò che corrisponde a quanto hai digitato); non propone mai un nome utente o una
password. Su e Giù scorrono le righe inviate dall'avvio (le ultime 100; una riga `account create`
con una password non viene mai conservata), ed Escape cancella la riga. In gioco, digita un comando con un punto iniziale,
come `.help`. I comandi vengono separati sugli spazi; argomenti tra virgolette e
password contenenti spazi non sono supportati.

Il registro dei comandi descrive quali comandi possono essere eseguiti da `InGame` e il loro
livello minimo di account. La console viene trattata come amministratore; i comandi in gioco
usano il livello dell'account della sessione che li invoca. L'input dei comandi e il loro normale
output rimangono privati per il chiamante. `broadcast` invia esplicitamente un messaggio di
sistema a tutti nel mondo locale e un `save` riuscito annuncia il completamento.
`shutdown` annuncia inoltre ai giocatori l'arresto richiesto del server.
Usa `..text` per pronunciare `.text` letteralmente.

## Tutti i comandi a colpo d'occhio

"Console" indica la console del server (sempre trattata come amministratore); "In gioco" significa
digitato con un punto iniziale da un personaggio il cui account ha almeno il livello minimo. Il
ruolo è la modalità del server che registra il comando: `Login`, `Game` o entrambi in `Standalone`.

| Comando | Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- | --- |
| [`help`](commands/help.md) | `help [command]` | Sì | Yes | Regular | Ogni ruolo |
| [`echo`, `e`](commands/echo.md) | `echo <text>` | Sì | Yes | Regular | Ogni ruolo |
| [`version`](commands/version.md) | `version` | Sì | Yes | Regular | Ogni ruolo |
| [`uptime`](commands/uptime.md) | `uptime` | Sì | Yes | Regular | Ogni ruolo |
| [`time`](commands/time.md) | `time` | No | Sì | Regular | Game |
| [`console`](commands/console.md) | `console lock` | Sì | No | — | Ogni ruolo |
| [`script`](commands/script.md) | `script reload <file>` / `script metrics` | Sì | No | — | Game |
| [`account`](commands/account.md) | `account create <username> <password> [level]` / `account api-access <username> <on\|off>` | Sì | Yes | Administrator | Login |
| [`character`](commands/character.md) | `character pending [account-serial]` / `character restore <character-serial>` | Sì | Yes | GameMaster | Game |
| [`save`](commands/save.md) | `save` | Sì | Yes | Administrator | Game |
| [`sql_backup`](commands/sql_backup.md) | `sql_backup` | Sì | Yes | Administrator | Ogni ruolo |
| [`broadcast`](commands/broadcast.md) | `broadcast <text>` | Sì | Yes | Administrator | Game |
| [`shutdown`](commands/shutdown.md) | `shutdown [seconds]` | Sì | Yes | Administrator | Game |
| [`decorate`](commands/decorate.md) | `decorate` | Sì | Yes | Administrator | Game |
| [`initial_spawn`](commands/initial_spawn.md) | `initial_spawn` | Sì | Yes | Administrator | Game |
| [`globallight`](commands/globallight.md) | `globallight [0-31]` | Sì | Yes | GameMaster | Game |
| [`spawn`](commands/spawn.md) | `spawn <template>`, poi seleziona un punto | No | Sì | GameMaster | Game |
| [`set`](commands/set.md) | `set <hits\|mana\|stamina\|hunger\|thirst\|criminal> <value>`, poi seleziona un mobile | No | Sì | GameMaster | Game |
| [`add`](commands/add.md) | `add <template>`, poi seleziona un punto | No | Sì | GameMaster | Game |
| [`remove`](commands/remove.md) | `remove`, poi seleziona un NPC o un oggetto a terra | No | Sì | GameMaster | Game |
| [`kill`](commands/kill.md) | `kill`, poi seleziona un NPC | No | Sì | GameMaster | Game |
| [`resurrect`](commands/resurrect.md) | `resurrect`, poi seleziona un cadavere | No | Sì | GameMaster | Game |
| [`animate`](commands/animate.md) | `animate <action>`, poi seleziona un mobile | No | Sì | GameMaster | Game |
| [`where`](commands/where.md) | `where`, poi seleziona qualsiasi cosa | No | Sì | GameMaster | Game |
| [`go`](commands/go.md) | `go [<x>,<y>,<z> [map] \| <place>]` | No | Sì | GameMaster | Game |
| [`gmtools`](commands/gmtools.md) | `gmtools` | No | Sì | GameMaster | Game |
| [`moongate`](commands/moongate.md) | `moongate <x>,<y>,<z> [map]` | No | Sì | GameMaster | Game |
| [`jail`](commands/jail.md) | `jail [name]` | No | Sì | GameMaster | Game |
| [`fame`](commands/fame.md) | `fame <0..32000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |
| [`karma`](commands/karma.md) | `karma <-32000..32000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |
| [`weather`](commands/weather.md) | `weather [none\|rain\|snow\|storm]` | No | Sì | GameMaster | Game |
| [`season`](commands/season.md) | `season [spring\|summer\|fall\|winter\|desolation\|auto]` | No | Sì | GameMaster | Game |
| [`spawns`](commands/spawns.md) | `spawns` | No | Sì | GameMaster | Game |
| [`gump`](commands/gump.md) | `gump <id> [name=value ...]` | No | Sì | GameMaster | Game |
| [`music`](commands/music.md) | `music [track]` | No | Sì | GameMaster | Game |
| [`lock`](commands/lock.md) | `lock`, poi seleziona una porta | No | Sì | GameMaster | Game |
| [`unlock`](commands/unlock.md) | `unlock`, poi seleziona una porta | No | Sì | GameMaster | Game |
| [`key`](commands/key.md) | `key`, poi seleziona una porta | No | Sì | GameMaster | Game |
| [`book`](commands/book.md) | `book <template> [name=value ...]` | No | Sì | GameMaster | Game |
| [`create_check`](commands/create_check.md) | `create_check <1..2000000000>` | No | Sì | GameMaster | Game |
| [`add_gold`](commands/add_gold.md) | `add_gold <1..60000>`, poi seleziona un mobile | No | Sì | GameMaster | Game |

### Da uno script

Uno script esegue qualsiasi di questi comandi con il modulo Lua `commands`:

```lua
commands.execute("season", "winter")          -- as the console: every power, no player
commands.execute_as(player, "go", "britain")  -- as that player wrote it in game
```

- `commands.execute(name, ...)` esegue il comando come fa la console del server. Un comando che richiede un
  giocatore, come uno che apre un cursore del bersaglio, risponde che funziona solo in gioco. Ciò che
  risponde il comando viene scritto nel log del server.
- `commands.execute_as(player, name, ...)` lo esegue come quel giocatore: con il livello del suo account,
  quindi un comando superiore viene rifiutato, e con la sua sessione, quindi il cursore si apre per lui. Il giocatore
  legge la risposta del comando.
- Gli argomenti seguono il nome, uno ciascuno: stringhe, numeri e booleani. Vengono uniti tramite
  spazi in una riga, quindi un argomento con uno spazio viene letto come due.
- Entrambi rispondono `true` quando il comando è stato avviato e `false` quando non c'è nulla da eseguire: un
  nome vuoto, un a capo nel nome o in un argomento, un giocatore che non è nel mondo. Il
  comando viene eseguito autonomamente, come uno digitato in gioco: lo script non lo attende e non
  riceve la sua risposta.

### In base a chi li usa

- **Tutti:** `help`, `echo`, `time`.
- **Game master:** `character`, `spawn`, `add`, `set`, `remove`, `kill`, `resurrect`, `animate`, `where`, `go`, `gmtools`, `moongate`, `fame`, `karma`, `globallight`,
  `weather`, `music`, `season`, `spawns`, `gump`, `lock`, `unlock`, `key`, `book`, `create_check`, `add_gold`.
- **Amministratori:** `account`, `save`, `sql_backup`, `broadcast`, `shutdown`, `decorate`,
  `initial_spawn`, più tutto ciò che
  usa un game master.
- **Solo console:** `console`, `script`.

I comandi che richiedono un bersaglio aprono il cursore del client dopo aver verificato gli
argomenti; premere Escape stampa `Target canceled.` e non cambia nulla. I testi che
stampano sono nella lingua del server (vedi [Localizzazione](localization.md)).
