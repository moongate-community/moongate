# Localization

The texts the server sends to players come from one language per server, chosen in
`moongate.toml`. The texts live in TOML files under `data/messages/` in the server
root; code reads them through `ILocalizationService` in `Moongate.Server.Ultima`.

Texts that the client already has in its own localized files (cliloc numbers) do not
need this service: the server sends the number and the client shows it in the
player's language. Use `ILocalizationService` for texts that the server writes itself.

## Choose the language

Set the language code in the `[localization]` section:

```toml
[localization]
language = "ita"
```

The code names the file `data/messages/<language>.toml`; the default is `eng`. The
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

`eng.toml` is the reference and must always exist. The server loads it first, then
replaces each text with the one in the chosen language. A message missing from the
chosen language stays in English, so a partial translation still works. The log
reports how many messages fell back:

```text
Found 5462 messages in ita, 3 of them in English
```

### Validation at startup

`MessagesLoader` stops the server at startup when:

- `eng.toml` or the chosen language's file does not exist;
- `eng.toml` has no messages;
- a key is not a number;
- a text is empty or is not a valid composite format, such as a lone `{`;
- a translation needs more values than the English text, which would fail when the
  code passes the English number of values;
- a translation has a number that `eng.toml` does not have.

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

## Add or change a text

1. Add the message to `data/messages/eng.toml` with a number that is not used yet.
2. Add the translation with the same number to the other files. A language without it
   shows the English text.
3. Use the same values, in the same order, in every language.
4. Run `dotnet test --filter RepositoryDataFiles`: it loads every shipped language
   with the real loader.

To add a language, copy `eng.toml` to `data/messages/<code>.toml`, translate the texts
and set `language = "<code>"`. The code may contain only ASCII letters.
