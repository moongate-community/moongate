<!-- translation: {"sourceHash":"788f6325e92570c5a2acfb5f40d01db01b6476c59deeaf2f1ab8fa975609ad6e","title":"Lua in Moongate"} -->

# Lua in Moongate

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). Il server esegue
Lua 5.2 tramite LuaCSharp, un Lua scritto in C#, quindi non serve un'installazione
Lua separata. Il linguaggio è quello del
[manuale di riferimento Lua 5.2](https://www.lua.org/manual/5.2/); questa pagina
indica cosa ha uno script e dove incontra differenze.

## Il linguaggio

`_VERSION` è `"Lua 5.2"`, e `goto` funziona. Le aggiunte di Lua 5.3 non compilano:
la divisione intera `//`, gli operatori bitwise come `&` e l'escape `\u{...}` in
una stringa; `\x41` invece sì.

## Librerie

Le librerie disponibili sono base, `string`, `table`, `math`, `coroutine` limitata
e `package` limitata. Non ci sono `io`, `os`, `debug`, `dofile`, `loadfile` o
`rawset`. Percorsi di ricerca dei pacchetti filesystem/nativi e coroutine create
 o riprese dagli script sono disabilitati. Usa `require` per moduli locali e `wait`
per sospensioni pianificate. Vedi il [riferimento della sandbox delle librerie](../../src/Moongate.Scripting/README.md#sandbox)
per le esatte funzioni rimosse.

| Uno script ha | Non ha |
| --- | --- |
| `string`, `table`, `math` | `io`, `os`, `debug`, `bit32`, `utf8` |
| `load`, `pcall`, `xpcall`, `select`, `setmetatable` | `dofile`, `loadfile`, `loadstring`, `rawset` |
| `table.unpack`, `table.pack` | `unpack` |
| `coroutine.yield`, `coroutine.running`, `coroutine.status` | `coroutine.create`, `coroutine.wrap`, `coroutine.resume` |
| `require` | `package.path`, `package.cpath`, `package.loadlib`, `package.searchpath` |

`print` scrive i valori, separati da tab, nel log del server al livello Information
e non restituisce nulla. `string.rep` rifiuta un risultato più lungo del limite
indicato in [Budget e sandbox](runtime.md#budgets-and-sandbox).

## Differenze rispetto al manuale

### Chiavi esadecimali delle tabelle

LuaCSharp non legge un numero esadecimale tra parentesi quadre (`t[0x0A27]` o
`{ [0x0A27] = ... }` falliscono con "numero malformato"): passalo tramite una
funzione o variabile, come fa `light.lua` con `add(0x0A27, 0x0B1D, "circle225")`.
Compila anche il numero tra parentesi, `t[(0x0A27)]`, come uno spazio prima della
parentesi quadra: `t[0x0A27 ]`.

### Stringhe

Una stringa conta unità UTF-16, mentre il Lua del manuale conta byte: `#"è"` e
`#"€"` sono 1, e `string.len("añb")` è 3. Le stringhe si confrontano unità per
unità, indipendentemente dalla lingua del server: `"é" < "z"` è false.

### Numeri

Ogni numero è un double, come Lua 5.2. Un valore intero viene scritto senza parte
frazionaria: `10 / 2` dà `5`. Ogni altro valore viene scritto con tutte le cifre,
mentre il Lua del manuale arrotonda a 14: `0.1 + 0.2` dà `0.30000000000000004`.
Un valore molto grande viene scritto con un esponente: `2 ^ 63` dà
`9.223372036854776E+18`. Anche `string.format("%g", 0.1 + 0.2)` scrive tutte le
cifre. Infinito e risultato di `0 / 0` vengono scritti `Infinity`, `-Infinity` e
`NaN`, mentre il Lua del manuale scrive `inf` e `nan`. `string.format("%d", 3.5)`
genera un errore: `%d` richiede un numero intero.

## Chiamare funzioni dell'host

Il server pubblica le proprie funzioni in tabelle modulo, come `npc` e `item`;
il [riferimento API Lua](https://moongate.sh/lua/) le elenca con i parametri.

- Una tabella modulo è di sola lettura: assegnare a un suo campo genera un errore
  come `'log' is read-only`, e la sua metatabella è bloccata.
- Un enum è una tabella globale di sola lettura, e `DirectionType.North` è un
  numero. Un parametro tipizzato con un enum accetta il numero o il nome del
  membro con le maiuscole esatte: `"North"`, non `"north"`.
- Un parametro tipizzato `integer` richiede un numero intero: `1.5` genera un errore
  `bad argument`.
- Una funzione host non converte una stringa in numero o un numero in stringa,
  come fanno gli operatori Lua (`"10" + 1` è 11): passare `"2"` per un `integer`
  genera `Int32 expected, got string`, e passare `5` per una `string` genera
  `String expected, got number`. Usa `tonumber` e `tostring`.
- Un parametro contrassegnato con `?` nel riferimento può essere omesso e usa il
  valore predefinito. Ometterne un altro genera
  `bad argument #1 to 'module.function' (name is required)`, con posizione e nome del parametro.

Per aggiungere funzioni proprie in C#, leggi [Scrivere un modulo Lua](../lua-modules.md).
