# book

Creates a readable document from a loaded [book template](../data-files/books.md)
and puts it in your backpack.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `book <template> [name=value ...]` | No | Yes | GameMaster | Game |

```text
.book welcome_letter contact_name=Vega
.book grammar_of_orcish
```

The template is the case-sensitive filename without `.toml`, from
`<root>/templates/books/`. To create your own scroll:

1. Save this as `<root>/templates/books/messaggio.toml`:

   ```toml
   title = "Message for $player_name"
   author = "Lord British"
   item_template = "readable_scroll"
   content = """
   Dear $player_name,

   Welcome to $server_name!
   Please report to the castle.
   """
   ```

2. Restart the server normally to load the new template.
3. Run `.book messaggio` in game as a GameMaster or Administrator.

A double click opens the parchment; with `item_template = "readable_book"` it opens the client's
book instead ([books and parchments](../data-files/books.md#books-and-parchments)), as
`grammar_of_orcish` does. The invoking character supplies `$player_name`;
the configured server language selects translations when present. Text is saved
at creation and stays unchanged when the document is traded or read by someone else.
This command also preserves any [attachments](../data-files/books.md#letter-attachments)
defined by the template, ready for its backpack bearer to claim once.

Custom `name=value` pairs supply the variables declared in the TOML. Names are
case-sensitive; values are literal strings, with no variable expansion. All declared
variables must be supplied, with no extra names or duplicates. An empty value is
allowed (`contact_name=`); the value may contain `=`. Arguments are separated by
whitespace: quoted values with spaces are not supported. For longer values, use
[`book.give` in Lua](../data-files/books.md#a-welcome-letter).

An unknown template reports its missing source. Invalid values, a missing or full
backpack, busy inventory or unavailable serials fail without creating a document.
After an update, copy the new messages 30181–30184 from the shipped
`data/messages/<language>/moongate.toml` into any preserved existing root.

## See also

- [All commands](../commands.md)
- [Readable text templates](../data-files/books.md)
- [Import book texts](../book-content-import.md)
