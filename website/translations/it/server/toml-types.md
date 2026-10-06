<!-- translation: {"sourceHash":"f52758456d36a99fbb50ebfbcec23956e83de7f19352bb9647f0736f84679e89","title":"Tipi di valore TOML"} -->

# Tipi di valore TOML

Moongate legge configurazione, file di dati e template da TOML. La maggior parte dei
campi sono valori TOML semplici: stringhe, interi, booleani, array e tabelle. Alcuni
campi hanno un tipo C# che TOML non conosce, come un punto, un intervallo di tinte o un
tipo di account. Un convertitore traduce tra quel tipo e un valore TOML: legge
il valore quando viene caricato un file e lo scrive quando il codice salva un file.

Questa pagina elenca ogni convertitore, le forme che accetta, la forma che scrive e gli
errori che produce. I nomi delle chiavi sono sempre snake_case: la proprietà `GoLocation` è la
chiave `go_location`.

## Come viene applicato un convertitore

Un convertitore raggiunge un campo in uno di tre modi.

**Integrato per gli enum.** `TomlUtils` aggiunge sempre `EnumTomlConverterFactory` alle proprie
opzioni predefinite, quindi ogni enum viene scritto e letto per nome senza registrazione:
vedi [Enum](#enums). Un convertitore registrato per un tipo enum prevale su di esso.

**Registrato per ogni chiamata.** `TomlUtils` in `Moongate.Core` mantiene un elenco di
convertitori usati da ogni chiamata senza opzioni proprie:

```csharp
public static void AddTomlConverter(TomlConverter converter);
public static bool RemoveTomlConverter<T>() where T : TomlConverter;
public static IReadOnlyList<TomlConverter> GetTomlConverters();
```

`AddTomlConverter` ignora un secondo convertitore dello stesso tipo.
`RemoveTomlConverter<T>` rimuove ogni convertitore di tipo `T` e restituisce se ne
ha rimosso uno. `GetTomlConverters` restituisce l'elenco attuale. Entrambe le modifiche sono
thread-safe: ciascuna costruisce un nuovo elenco e nuove opzioni predefinite sotto un lock e
le pubblica insieme, quindi una chiamata in esecuzione contemporanea vede il vecchio insieme o
quello nuovo, mai una combinazione. L'elenco è globale al processo, quindi registra una volta sola,
all'avvio.

Queste classi registrano convertitori:

| Chi | Convertitori |
| --- | --- |
| `MoongateUltimaPlugin.Register` | `Serial`, `Point2D`, `Point3D`, `HueSpec`, `Rectangle2D`, e le factory `EnumValueSpec` e `RangeValueSpec` |
| `mgctl convert` (`Moongate.UoxItemConverter`) | gli stessi, tranne `Rectangle2D` |
| Test | nessuno globalmente; ogni test passa le proprie opzioni con il convertitore che verifica |

Un convertitore registrato copre anche la forma nullable del proprio tipo: `go_location` in
un file di regione è un `Point3D?` e usa `Point3DTomlConverter`.

**Dichiarato con un attributo.** `[TomlConverter(typeof(...))]` su una proprietà o su un
tipo applica il convertitore a quella proprietà o a ogni uso di quel tipo, senza
registrazione. Le aree delle regioni lo usano sul proprio tipo:

```csharp
[TomlConverter(typeof(RegionAreaContentTomlConverter))]
public sealed class RegionAreaContent
```

Un convertitore indicato da un attributo deve convertire esattamente il tipo del membro: una proprietà `int?`
richiede un convertitore per `int?`, non per `int`. I convertitori registrati e quello
integrato per gli enum non hanno tale limite: coprono anche la forma nullable, e
un valore nullable non impostato viene omesso dal file.

**Le opzioni esplicite non ricevono nulla.** `Deserialize`, `Serialize` e gli overload per file
accettano un `TomlSerializerOptions` opzionale. Senza di esso, la chiamata usa le opzioni
predefinite: nomi snake_case, i convertitori registrati e il convertitore enum. Con esso la chiamata usa
le opzioni esattamente come fornite: i convertitori registrati non vengono mai aggiunti.
I convertitori dichiarati con un attributo si applicano comunque, perché appartengono al tipo.

Quando un convertitore rifiuta un valore, il caricamento fallisce con una `TomlException`. Il messaggio
indica riga e colonna e termina con il motivo del convertitore, per esempio:

```text
(1,10) : error : Exception while trying to convert TOML value to type '...EnumValueSpec`1[...]' using converter '...'.
Reason: (1,10) : error : 'not-a-member' is not a valid ItemRarityType value or random_of spec.
```

I motivi citati in questa pagina sono quell'ultima parte.

## Riepilogo

| Tipo | Forma TOML | Esempio | Convertitore | Applicato tramite |
| --- | --- | --- | --- | --- |
| `Serial` | intero senza virgolette o numero tra virgolette | `item_id = 0x0FEF` | `SerialTomlConverter` | registrazione |
| `Point2D` | `"(x, y)"` tra virgolette | `size = "(7168, 4096)"` | `Point2DTomlConverter` | registrazione |
| `Point3D` | `"(x, y, z)"` tra virgolette | `location = "(3503, 2574, 14)"` | `Point3DTomlConverter` | registrazione |
| `Rectangle2D` | `"(x1, y1)..(x2, y2)"` tra virgolette | `bounds = "(44, 65)..(186, 159)"` | `Rectangle2DTomlConverter` | registrazione |
| `RegionAreaContent` | stringa di rettangolo oppure tabella con `bounds`, `z1`, `z2` | `{ bounds = "(1, 2)..(3, 4)", z1 = 0 }` | `RegionAreaContentTomlConverter` | attributo sul tipo |
| `HueSpec` | intero senza virgolette oppure tinta o intervallo tra virgolette | `hue = "0x047E-0x04B0"` | `HueSpecTomlConverter` | registrazione |
| `EnumValueSpec<TEnum>` | nome del membro o specifica `random_of` tra virgolette | `rarity = "random_of:rare,epic"` | `EnumValueSpecTomlConverterFactory` | registrazione |
| `RangeValueSpec<T>` | numero senza virgolette oppure `"min-max"` tra virgolette | `amount = "5-10"` | `RangeValueSpecTomlConverterFactory` | registrazione |
| `decimal` | intero o float senza virgolette | `weight = 0.02`, `weight = 7` | integrato | built in |
| `DiceSpec` | intero senza virgolette oppure espressione di dadi tra virgolette | `strength = "1d25+95"` | `DiceSpecTomlConverter` | registrazione |
| Qualsiasi enum | il suo nome snake_case; flag uniti da `\|` | `visibility = "game_master"`, `mode = "standalone"` | `EnumTomlConverterFactory` | integrato |

I convertitori del primo gruppo e quello enum sono in
`Moongate.Core/Serialization/Toml`; `RegionAreaContentTomlConverter` è in
`Moongate.Server.Ultima/Serialization/Toml`.

## Serial

Un `Serial` è un'identità UO: un seriale di entità, un id grafico o un numero cliloc.

Forme accettate:

```toml
item_id = 0x0FEF    # any TOML integer: decimal, hex, octal or binary
item_id = 4079      # the same value
item_id = "0x0FEF"  # quoted hex
item_id = "4079"    # quoted decimal
```

Nel testo tra virgolette, il prefisso `0x` sceglie la base: `"40000001"` è quaranta milioni, non
il primo seriale di oggetto `"0x40000001"`. Il testo tra virgolette non accetta segni; gli spazi attorno
vengono ignorati.

Forma scritta: sempre un intero decimale senza virgolette. `0x0FEF` viene riscritto come `4079`.

Errori:

| Valore | Risultato |
| --- | --- |
| `item_id = "not-a-serial"` | `'not-a-serial' is not a valid serial.` |
| `item_id = 1.5` | `Expected token Integer but was Float.` |
| `item_id = -1` | nessun errore: un intero senza virgolette non viene verificato nell’intervallo e viene ridotto a 32 bit, quindi `-1` diventa `0xFFFFFFFF` |

Usato da: `cliloc` in `starting_cities.toml` e `item_id` nei template di oggetto.

## Point2D e Point3D

Un punto viene scritto come il testo tra virgolette prodotto dal suo `ToString()`, la stessa forma che il
server stampa nei log e nei comandi, quindi un valore copiato da lì può essere incollato in un file.

Forme accettate:

```toml
size = "(7168, 4096)"             # Point2D: (x, y)
location = "(1495, 1629, 10)"     # Point3D: (x, y, z)
location = "( -5 , 7 , -20 )"     # spaces are ignored; negative numbers are allowed
```

Le parentesi sono obbligatorie e ogni coordinata è un intero. Entrambi i convertitori
usano la cultura invariabile, quindi un file viene letto allo stesso modo su ogni macchina e un numero
negativo viene sempre scritto con un meno ASCII.

Forma scritta: `"(x, y)"` o `"(x, y, z)"`, con uno spazio dopo ogni virgola.

Errori (mostrati per `Point2D`; `Point3D` produce gli stessi messaggi con `(x, y, z)`):

| Valore | Risultato |
| --- | --- |
| `position = 5` | `Expected a "(x, y)" string for a Point2D.` |
| `position = "not-a-point"` | `'not-a-point' is not a valid Point2D, expected "(x, y)".` |
| `position = "(1, 2, 3)"` | `'(1, 2, 3)' is not a valid Point2D, expected "(x, y)".` |
| `location = "(1, 2)"` | `'(1, 2)' is not a valid Point3D, expected "(x, y, z)".` |

Usato da: `size` in `maps.toml` (`Point2D`); `location` in `starting_cities.toml`,
`go_location` ed `entrance` nei file di regione (`Point3D`).

## Rectangle2D

Un rettangolo consiste di due angoli uniti da `..`. Il primo è incluso e il
secondo è escluso: il secondo punto è un estremo, non larghezza e altezza.

Forme accettate:

```toml
bounds = "(44, 65)..(186, 159)"       # X 44 to 185, Y 65 to 158
bounds = " (44, 65) .. (186, 159) "   # spaces are ignored
bounds = "(44, 65)+(142, 94)"         # legacy corner-plus-size form, the same rectangle
```

Forma scritta: sempre la forma con gli angoli, `"(44, 65)..(186, 159)"`. Un file che usa la
forma legacy viene riscritto nella forma con gli angoli la prossima volta che il codice lo salva.

Errori:

| Valore | Risultato |
| --- | --- |
| `bounds = 5` | `Expected a "(x1, y1)..(x2, y2)" string for a Rectangle2D.` |
| `bounds = "(44, 65)"` | `'(44, 65)' is not a valid Rectangle2D, expected "(x1, y1)..(x2, y2)".` |
| `bounds = "(44, 65)-(142, 94)"` | lo stesso messaggio, indicando `(44, 65)-(142, 94)` |
| `bounds = "44 65 142 94"` | lo stesso messaggio, indicando `44 65 142 94` |

Il testo che contiene `..` ma i cui angoli non possono essere analizzati è sempre un errore; non viene
mai tentato come forma legacy.

Usato da: `bounds` in `containers.toml` e dal convertitore delle aree di regione qui sotto.

## Aree delle regioni

Ogni voce di `areas` in un file di regione è un `RegionAreaContent`. Il tipo contiene
`[TomlConverter(typeof(RegionAreaContentTomlConverter))]`, quindi non richiede
registrazione e legge il proprio rettangolo con un suo `Rectangle2DTomlConverter`.
L'inizio è incluso e la fine è esclusa su ogni asse, anche l'altezza.

Forme accettate, che possono essere mescolate in un array:

```toml
areas = [
    "(1330, 1991)..(1343, 2004)",                              # every height
    { bounds = "(1416, 1498)..(1740, 1777)", z1 = -10, z2 = 128 },
    { bounds = "(1, 2)..(3, 4)", z1 = 0 },                     # either limit may be left out
    { x1 = 10, y1 = 20, x2 = 30, y2 = 40, z2 = -80 },          # legacy corners, read only
]
```

Una stringa accetta ogni forma di `Rectangle2D`, compresa la forma legacy `+`.
Dentro una tabella, le chiavi sono `bounds`, `x1`, `y1`, `x2`, `y2`, `z1` e `z2`; `z1`
è incluso e `z2` è escluso.

Forma scritta: una stringa quando l'area non ha limite di altezza, altrimenti una tabella inline
con `bounds` e i limiti presenti. Le chiavi legacy `x1`/`y1`/`x2`/`y2` non vengono mai
scritte.

Errori:

| Valore | Risultato |
| --- | --- |
| `42` | `Expected a region area string or a table with bounds and optional z1/z2.` |
| `"(1, 2)..bad"` | il messaggio di `Rectangle2D` |
| `{ bounds = "(1, 2)..(3, 4)", x1 = 1 }` | `Use bounds or x1/y1/x2/y2 for a region area, not both.` |
| `{ z1 = 0 }` oppure `{ x1 = 1, y1 = 2, x2 = 3 }` | `A region area table requires bounds or all of x1/y1/x2/y2.` |
| `{ bounds = "(1, 2)..(3, 4)", z1 = 1.5 }` | `Region area coordinates must be integers.` |
| `{ bounds = "(1, 2)..(3, 4)", z2 = 2147483648 }` | `Region area coordinates must fit in a 32-bit integer.` |
| `{ bounds = "(1, 2)..(3, 4)", top = 5 }` | `Unknown region area field 'top'.` |

Usato da: `areas` in `data/regions/<map>.toml`. Vedi
[Aree delle regioni](data-files/regions.md#areas).

## HueSpec

Un `HueSpec` è una singola tinta oppure un intervallo da cui scegliere una nuova tinta ogni volta che viene
risolto, per esempio una volta per oggetto generato. Ogni tinta è tra `0` e `0xFFFF`.

Forme accettate:

```toml
hue = 1150                  # bare integer, decimal
hue = 0x047E                # bare integer, hex: the same hue
hue = "0x047E"              # a quoted hue, decimal or hex
hue = "1150-1200"           # a range, both bounds included
hue = "0x047E-0x04B0"       # the same range in hex
hue = "hue(1150:1200)"      # the same range, alternative form
```

In un intervallo, il primo estremo non deve essere maggiore del secondo. Un intervallo con estremi
uguali viene accettato ed è comunque un intervallo.

Forma scritta: una tinta singola come intero decimale senza virgolette (`hue = 1150`), un intervallo come
intervallo esadecimale tra virgolette (`hue = "0x047E-0x04B0"`).

Errori:

| Valore | Risultato |
| --- | --- |
| `hue = 70000` | `70000 is not a hue; a hue must be between 0 and 0xFFFF.` |
| `hue = -1` | `-1 is not a hue; a hue must be between 0 and 0xFFFF.` |
| `hue = "red"` | `'red' is not a hue or a hue range.` |
| `hue = "1200-1150"` | `'1200-1150' is not a hue or a hue range.` |
| `hue = 1.5` | `Expected a hue number or a hue range string.` |

Usato da: `skin_hues` e `hair_hues` in `races.toml` (array di `HueSpec`, come
`["0x03EA-0x0422"]`) e `hue` nei template di oggetto.

## EnumValueSpec

Un `EnumValueSpec<TEnum>` è un campo enum fisso oppure scelto casualmente
ogni volta che viene risolto. Una registrazione di `EnumValueSpecTomlConverterFactory`
copre ogni enum: la factory costruisce un `EnumValueSpecTomlConverter<TEnum>` per
ogni `EnumValueSpec<TEnum>` che incontra.

Forme accettate, sempre una stringa tra virgolette:

```toml
rarity = "common"                          # always Common
rarity = "random_of"                       # any member, picked on each resolve
rarity = "random_of:rare,epic,legendary"   # one of these members, picked on each resolve
```

I nomi dei membri ignorano maiuscole, minuscole e trattini bassi: `"Epic"` e `"epic"` sono equivalenti, così
come `"north_east"`, `"NorthEast"` e `"northeast"`. Gli spazi attorno ai nomi nell'elenco
vengono ignorati. Sono accettati solo nomi, mai numeri.

Forma scritta: snake_case minuscolo, `"rare"`, `"north_east"` oppure
`"random_of:rare,epic"`; viene sempre riletto correttamente.

Errori:

| Valore | Risultato |
| --- | --- |
| `rarity = "not-a-member"` | `'not-a-member' is not a valid ItemRarityType value or random_of spec.` |
| `rarity = "random_of:"` | lo stesso messaggio, indicando `random_of:` |
| `rarity = "3"` | `'3' is not a valid ItemRarityType value or random_of spec.` |
| `rarity = 1` | `Expected token String but was Integer.` |

Usato da: `rarity` nei template di oggetto. Vedi
[Campi risolti casualmente](templates.md#fields-that-resolve-randomly).

## RangeValueSpec

Un `RangeValueSpec<T>` è un campo numerico fisso oppure scelto da un intervallo
ogni volta che viene risolto. `T` è qualsiasi tipo numerico .NET. Una registrazione di
`RangeValueSpecTomlConverterFactory` copre ogni tipo numerico.

Forme accettate:

```toml
amount = 5         # bare integer: fixed
amount = 2.5       # bare float: fixed
amount = "5"       # quoted number: fixed
amount = "5-10"    # quoted range: a fresh value from 5 to 10 on each resolve
amount = "-10--5"  # negative bounds: a leading minus is a sign, not the separator
```

Entrambi gli estremi di un intervallo sono inclusi e il primo non deve essere maggiore del
secondo. Un intervallo sceglie il minimo più un numero intero, quindi un intervallo `double`
`"0.5-2.5"` produce `0.5`, `1.5` o `2.5`. I numeri tra virgolette usano la cultura invariabile.

Forma scritta: un valore intero fisso come intero senza virgolette (`amount = 5`), una frazione fissa
come float senza virgolette (`amount = 2.5`), un intervallo come testo tra virgolette (`amount = "5-10"`).

Errori:

| Valore | Risultato |
| --- | --- |
| `amount = "not-a-range"` | `'not-a-range' is not a valid Int32 value or range.` |
| `amount = "10-5"` | `'10-5' is not a valid Int32 value or range.` |
| `amount = true` | `Expected a number or a range for Int32.` |
| `small = 300` su un `RangeValueSpec<byte>` | `Arithmetic operation resulted in an overflow.` |
| `amount = 1.7` su un `RangeValueSpec<int>` | nessun errore: un float senza virgolette viene troncato a `1` |

Usato da: `amount` nei template di bottino. Vedi
[Campi risolti in un nuovo numero](templates.md#fields-that-resolve-to-a-fresh-number).

## DiceSpec

Un `DiceSpec` è un campo numerico tirato con la notazione dei dadi oppure una costante. I template dei
mobile lo usano per statistiche, abilità, danni, resistenze, karma, fama e oro.

Forme accettate:

```toml
armor = 20             # bare integer: constant
karma = -2500          # negative constants are allowed
karma = "-2500"        # the same, quoted
strength = "1d25+95"   # one 25-sided die plus 95: 96 to 120
damage = "3d4+2"       # three 4-sided dice plus 2: 5 to 14
hits = "4d6k3"         # four 6-sided dice, keep the highest three
mana = "(2d6+1)*10"    # parentheses, *, /
```

`NdM` tira N dadi a M facce; `+`, `-`, `*`, `/`, parentesi e `k` (conserva i
più alti) li combinano. Un intervallo uniforme da a a b è un dado,
`1d(b-a+1)+(a-1)`. **`"96-120"` non è un intervallo**: è 96 meno 120, una costante -24.

Forma scritta: una costante come intero senza virgolette, un'espressione come il testo tra virgolette da cui è stata
letta.

Errori:

| Valore | Risultato |
| --- | --- |
| `strength = "2d"` | `'2d' is not a number or a dice expression.` |
| `strength = true` | `Expected a number or a dice expression.` |

Usato da: `MobileTemplate`. Vedi [Le strutture dei template](templates.md#the-template-shapes).

## Enum

Ogni enum viene scritto come il proprio nome snake_case e riletto da esso, tramite
`EnumTomlConverterFactory`, che `TomlUtils` include sempre nelle opzioni
predefinite. Le regole risiedono in `EnumNameUtils`, condiviso da `EnumValueSpec`.

```toml
minimum_account_type = "game_master"   # AccountType.GameMaster
map = "ter_mur"                        # MapType.TerMur
music = "mountn_a"                     # MusicType.Mountn_a
```

- **La lettura** ignora maiuscole, minuscole e trattini bassi: `"game_master"`, `"GameMaster"` e
  `"gamemaster"` sono equivalenti e `"termur"` legge `MapType.TerMur`.
- **La scrittura** usa il nome snake_case minuscolo, sempre rileggibile.
- **I numeri** non sono mai accettati, senza virgolette o in una stringa: scrivi il nome. I dati indicizzati dai
  numeri del client, come `skills.toml`, indicano invece il membro
  (`id = "alchemy"` è `SkillType.Alchemy`, valore 0).

**Flag.** Un enum `[Flags]` scrive un valore con nome proprio come quel nome e
ogni altra combinazione come nomi uniti da `|`; la lettura combina i nomi:

```toml
mode = "standalone"              # ServerMode.Login | ServerMode.Game has its own name
mode = "login|game"              # reads the same value
flags = "impassable|surface"     # TileFlagType.Impassable | TileFlagType.Surface
```

Gli spazi attorno ai nomi vengono ignorati. Una combinazione viene divisa nelle parti nominate più
grandi, quindi `DirectionType.SouthEast | DirectionType.Running` viene scritto come
`"running|south_east"`, non come singoli bit. Zero viene scritto come il nome del membro
zero quando ne esiste uno (`"none"`) e altrimenti come stringa vuota; una stringa vuota
viene letta come zero solo per un enum di flag. Un enum semplice rifiuta `|`.

Errori:

| Valore | Risultato |
| --- | --- |
| `minimum_account_type = "gm"` | `'gm' is not a AccountType; use one of regular, game_master, administrator.` |
| `minimum_account_type = "1"` | lo stesso messaggio, indicando `1` |
| `minimum_account_type = 1` | `Expected a AccountType name as a string, such as "regular".` |
| `minimum_account_type = true` | `Expected a AccountType name as a string, such as "regular".` |

Un valore viene comunque controllato dal codice che lo carica: `mode = "none"` viene letto, poi la
configurazione rifiuta `ServerMode.None`.

Usato da: `mode` e `realm_directory.minimum_account_type` in `moongate.toml`,
`visibility` nei template di oggetto e le chiavi enum dei [file di dati dello shard](data-files.md),
come `map`, `season`, `music` e `skill`.

## Scrivere un convertitore

Per un tuo tipo, deriva da `TomlConverter<T>` per un tipo chiuso oppure da
`TomlConverterFactory` quando il tipo è generico. Poi registra un'istanza con
`TomlUtils.AddTomlConverter` all'avvio, di solito da `Register` di un plugin, oppure indica
il convertitore in un attributo `[TomlConverter(typeof(...))]` sulla proprietà o sul tipo.

Alcune regole mantengono coerenti i convertitori:

- In `Read`, verifica `reader.TokenType` e genera `reader.CreateException(...)` per un
  valore che non accetti; l'eccezione riceve riga e colonna. Indica il
  testo errato nel messaggio.
- In `Write`, genera `TomlException` per un valore che non ha una forma TOML.
- Formatta e analizza i numeri con `CultureInfo.InvariantCulture`.
- Scrivi la forma che una persona digiterebbe e fai sì che `Read` accetti ciò che produce `Write`.

### Esempio svolto: Serial

`SerialTomlConverter` legge un intero senza virgolette, analizzato nativamente da TOML, oppure lo stesso
valore tra virgolette, e scrive sempre un intero senza virgolette:

```csharp
public sealed class SerialTomlConverter : TomlConverter<Serial>
{
    public override Serial Read(TomlReader reader)
    {
        if (reader.TokenType == TomlTokenType.String)
        {
            var text = reader.GetString();

            if (!Serial.TryParse(text, out var parsed))
            {
                throw reader.CreateException($"'{text}' is not a valid serial.");
            }

            return parsed;
        }

        return new((uint)reader.GetInt64());
    }

    public override void Write(TomlWriter writer, Serial value)
    {
        writer.WriteIntegerValue(value.Value);
    }
}
```

### Esempio svolto: Point2D e Point3D

Un tipo con una propria forma testuale può riusarla. `Point2DTomlConverter` accetta solo una
stringa, la analizza con `Point2D.TryParse` e scrive `ToString`, entrambi con la
cultura invariabile:

```csharp
public sealed class Point2DTomlConverter : TomlConverter<Point2D>
{
    public override Point2D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x, y)\" string for a Point2D.");
        }

        var text = reader.GetString();

        if (!Point2D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Point2D, expected \"(x, y)\".");
        }

        return parsed;
    }

    public override void Write(TomlWriter writer, Point2D value)
    {
        writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
```

`Point3DTomlConverter` è identico con tre coordinate.

### Tipi generici e tabelle

`EnumValueSpecTomlConverterFactory` mostra la struttura della factory: `CanConvert` riconosce ogni
`EnumValueSpec<>` chiuso e `CreateConverter` costruisce il corrispondente
`EnumValueSpecTomlConverter<TEnum>` tramite reflection, quindi una registrazione copre ogni
enum.

`RegionAreaContentTomlConverter` mostra un convertitore che legge sia una stringa sia una
tabella inline: scorre i nomi delle proprietà della tabella fino a `EndTable` e scrive con
`WriteStartInlineTable`, `WritePropertyName` e `WriteEndInlineTable`.

## Vedi anche

- [File di dati dello shard](data-files.md): i file sotto `data/` e i loro campi.
- [Caricamento dei template TOML](templates.md): template di oggetti e bottino e contratto del
  caricatore.
- [Configurazione del server](server-configuration.md): `moongate.toml`.
