# Localization

The texts the server sends to players come from one language per server, chosen in
`moongate.toml`. The texts live in TOML files under `data/messages/` in the server
root; C# code reads them through `ILocalizationService` in `Moongate.Server.Ultima`,
and Lua scripts through the `localization` module.

Texts that the client already has in its own localized files (cliloc numbers) do not
need this service: the server sends the number and the client shows it in the
player's language. Use `ILocalizationService` for texts that the server writes itself.

## Choose the language

Set the language code in the `[ultima.localization]` section:

```toml
[ultima.localization]
language = "ita"
```

The code names the file `data/messages/<language>.toml` and the directory
`data/messages/<language>/`; the default is `eng`. The
server ships these languages:

| Code | Language |
| --- | --- |
| `eng` | English |
| `ita` | Italian |
| `ger` | German |
| `fre` | French |
| `spa` | Spanish |
| `por` | Portuguese |
| `pol` | Polish |
| `cze` | Czech |

The language applies to the whole server, in game and standalone modes. A change
takes effect at the next start. See the
[configuration reference](server-configuration.md#settings-and-validation).

## Message files

Each file is a `[messages]` table of `number = "text"`:

```toml
[messages]
0 = "Questo oggetto non ha più cariche."
691 = "{0} è stato ucciso da {1}! [Terremoto]\n"
1737 = "[{0:x} {1:x} {2:x} {3:x}]"
```

- **Number:** the message id. The same number is the same message in every language.
- **Text:** a .NET composite format. `{0}`, `{1}`, ... are the values the code fills in,
  in order. `{0,6}` pads a value to 6 characters and `{0:x}` writes a number in hex.
  Write `{{` and `}}` for a literal brace.

The files come from the UOX3 dictionaries (`data/dictionaries/dictionary.*`), with
the UOX3 printf placeholders (`%s`, `%i`, `%d`) turned into `{0}`, `{1}`, ...

### English fallback

English is the reference and must always exist. The server loads it first, then
replaces each text with the one in the chosen language. A message missing from the
chosen language stays in English, so a partial translation still works. The log
reports how many messages fell back:

```text
Found 5630 messages in ita, 3 of them in English
```

### Validation at startup

`MessagesLoader` stops the server at startup when:

- English or the chosen language has neither its file nor a toml file in its directory;
- English has no messages;
- a key is not a number;
- a text is empty or is not a valid composite format, such as a lone `{`;
- a text needs more than 16 values;
- a translation needs more values than the English text, which would fail when the
  code passes the English number of values;
- a translation has a number that English does not have;
- the same number is in two files of one language, or twice in one file (`1` and `01`).

### Split a language into several files

Besides `data/messages/<language>.toml`, the server reads every `*.toml` file in the
directory `data/messages/<language>/` and merges them all into one set of messages:

```text
data/messages/
  eng.toml            # shipped: the standard texts, from UOX3
  eng/
    moongate.toml     # shipped: Moongate's own texts, numbers from 30000
    shard.toml        # your own texts
    quests.toml
  ita.toml
  ita/
    moongate.toml
    shard.toml
```

The server ships two files per language: `<language>.toml` with the standard texts and
`<language>/moongate.toml` with [Moongate's own messages](#moongates-own-messages).

- The file and the directory can both exist, or only one of them.
- Every file has the same format: a `[messages]` table of `number = "text"`.
- The files of the directory are read in name order. Subdirectories and files that do
  not end in `.toml` are ignored.
- Write the directory name in lower case: on Linux `ENG/shard.toml` is not read. The
  case of the file name and of the `.toml` extension does not matter.
- A number can be in one file only. The same number in two files of one language stops
  the server and the error names both files.

Keep your shard's texts in files of your own in `data/messages/eng/` so that an update
of the shipped `eng.toml` and `eng/moongate.toml` does not overwrite them.

## Read a message from code

Resolve `ILocalizationService` from the container, for example through a
constructor:

```csharp
public class BoatHandler
{
    private readonly ILocalizationService _localization;

    public BoatHandler(ILocalizationService localization)
    {
        _localization = localization;
    }

    public string BoardMessage()
    {
        return _localization.Get(1); // "Si sale a bordo della barca."
    }
}
```

| Member | What it does |
| --- | --- |
| `Language` | The configured code in lower case, such as `ita`. |
| `Get(id, values...)` | The message with `{0}`, `{1}`, ... replaced by `values`, and `{{`, `}}` turned into single braces. Throws `KeyNotFoundException` for an unknown id and `FormatException` when fewer values are given than the text needs. |
| `TryGetText(id, out text)` | The text as written in the file, without filling in values; false for an unknown id. |

```csharp
_localization.Get(691, "Bob", "un drago"); // "Bob è stato ucciso da un drago! [Terremoto]\n"

if (_localization.TryGetText(691, out var text))
{
    // text is "{0} è stato ucciso da {1}! [Terremoto]\n"
}
```

Values are formatted with the invariant culture, so numbers do not change with the
host's regional settings.

The service is registered by the Ultima plugin in game and standalone modes. It reads
the messages the first time a text is asked for, after `IDataLoaderService` has run
the loaders at startup; asking earlier fails because the messages are not loaded yet.

## Read a message from Lua

Scripts use the `localization` module, which calls `ILocalizationService`:

```lua
local text = localization.get(691, 'Bob', 'un drago')
-- "Bob è stato ucciso da un drago! [Terremoto]\n"

local raw = localization.text(691)    -- "{0} è stato ucciso da {1}! [Terremoto]\n"
local missing = localization.text(99999) -- nil

log.info('Server language: {Language}', localization.language()) -- "ita"
```

| Function | What it does |
| --- | --- |
| `localization.get(id, ...)` | The message with `{0}`, `{1}`, ... replaced by the extra arguments. A whole Lua number is passed as an integer, so `{0:x}` works on it; `nil` is written as `nil`. An unknown id or too few arguments raise a Lua error, which a script can catch with `pcall`. |
| `localization.text(id)` | The text as written in the file, or `nil` for an unknown id. Use it to check whether a message exists. |
| `localization.language()` | The server language code, such as `ita`. |

```lua
local ok, err = pcall(localization.get, 99999)
-- ok is false, err contains "No message has id 99999."
```

The Ultima plugin registers the module in game and standalone modes, with the
other data services. `definitions.lua` declares it for editor completion, with
`localization.text` returning `string?`.

## Moongate's own messages

Numbers from 30000 are Moongate's, not UOX3's, all translated in every shipped language.
They live in `data/messages/<language>/moongate.toml`, apart from the standard texts:

| Id | Text | Used by |
| --- | --- | --- |
| 30000–30004 | Common, Uncommon, Rare, Epic, Legendary | Tooltip rarity |
| 30005 | [Cursed] | Tooltip loot type |
| 30006, 30007 | Weight: 1 stone, Weight: {0} stones | Tooltip weight |
| 30008–30038, 30050–30052, most of 30055–30112, 30115–30120, 30122 and 30126 | Target canceled., Unknown command: {0}, Usage: {0}, The world has been saved in {0} seconds., {0} now has {1} fame., ... | Command replies and broadcasts (`CommandMessages`) |
| 30039–30049, 30053–30054, the rest up to 30113, 30121 and 30127 | One per built-in command | Command descriptions in `help` |
| 30114 | This moongate does not seem to go anywhere. | Texts of the item scripts, read with `localization.get` |
| 30123, 30124 | You are hungry., You are starving: your wounds will not heal until you eat. | What a player reads as its hunger drops |
| 30128, 30129 | You are thirsty., You are parched: your stamina will not come back until you drink. | What a player reads as its thirst drops |
| 30130–30132 | You are simply too full to drink any more!, It is empty., You drink, and feel less thirsty. | The texts of `scripts/items/drink.lua` |
| 30133 | You are overloaded: you carry {0} stones of {1}. | What a player reads when it puts something down while carrying more than it may |
| 30134–30137 | You have entered {0}., You have left {0}., You are now under the protection of the guards of {0}., You have left the protection of the guards of {0}. | What a player reads when it walks into or out of a named place, and when the protection of its guards changes |
| 30138 | Thou wilt regret thine actions, swine! | What a guard says when it comes for a criminal |
| 30148, 30149 | You have been jailed for {0} days: {1}, Reason: {0} | What a prisoner reads when a reason was given, and the reason on its release note |
| 30151 | the remains of {0} | The name of the corpse an NPC leaves |
| 30152 to 30154 | Kills the NPC you target…, {0} is dead., Players cannot die yet. | The `kill` command |
| 30164 to 30167 | Raises the NPC whose corpse you target…, {0} is back., That is not a corpse., That corpse cannot be raised. | The `resurrect` command |
| 30155 to 30157 | You must wait {0} seconds before posting again., That message is not yours., The board is busy: post again in a moment. | What a player reads at a [bulletin board](bulletin-boards.md) |
| 30168 | You may not use skills in jail. | What a prisoner reads when it uses a [skill](skills.md) |
| 30181, 30182 | Opens the gump of the game master's tools…, The gmtools gump is missing: templates/gumps/gmtools.xml. | The `gmtools` command |
| 30150 | No character is named {0}. | What `jail <name>` answers when no player has the name |
| 30160 to 30163 | Shows the version the server runs…, Moongate {0} "{1}" ({2}), built {3}., Shows how long the server has been running…, Up for {0}, since {1}. | The `version` and `uptime` commands |

The header of the command texts in `eng/moongate.toml` lists the ids of both sets.

Tooltips also use UOX3's 9055 "[Blessed]", and `.account` its 555 "An account by that
name already exists!". Polish and Czech write the plural weight
abbreviated ("kam."), since one text with `{0}` cannot follow their plural forms.

## Add or change a text

1. Add the message with a number that is not used yet: a text of Moongate's code to
   `data/messages/eng/moongate.toml`, a text of your shard to a file of your own in
   `data/messages/eng/`.
2. Add the translation with the same number to the other files. A language without it
   shows the English text.
3. Use the same values, in the same order, in every language.
4. Run `dotnet test --filter RepositoryDataFiles`: it loads every shipped language
   with the real loader.

To add a language, copy `eng.toml` to `data/messages/<code>.toml` and
`eng/moongate.toml` to `data/messages/<code>/moongate.toml`, translate the texts and set
`language = "<code>"`. The code may contain only ASCII letters.
