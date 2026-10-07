<!-- translation: {"sourceHash":"8c776ffa71db9a8d1db374093700bda8f694693b443716d881e31afe412739ef","title":"book"} -->

# book

Crea un documento leggibile da un [template di libro](../data-files/books.md) caricato
e lo inserisce nel tuo zaino.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `book <template> [name=value ...]` | No | Sì | GameMaster | Game |

```text
.book welcome_letter contact_name=Vega
.book grammar_of_orcish
.book blank_book
```

Il template è il nome del file senza `.toml`, con distinzione tra maiuscole e minuscole, da
`<root>/templates/books/`. Per creare la tua pergamena:

1. Salva questo contenuto come `<root>/templates/books/messaggio.toml`:

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

2. Riavvia normalmente il server per caricare il nuovo template.
3. Esegui `.book messaggio` in gioco come GameMaster o Administrator.

Un doppio clic apre la pergamena; con `item_template = "readable_book"` apre invece il
libro del client ([libri e pergamene](../data-files/books.md#books-and-parchments)), come
fa `grammar_of_orcish`. Il personaggio che esegue il comando fornisce `$player_name`;
la lingua configurata del server seleziona le traduzioni quando presenti. Il testo viene salvato
alla creazione e rimane invariato quando il documento viene scambiato o letto da qualcun altro.
Questo comando conserva anche gli eventuali [allegati](../data-files/books.md#letter-attachments)
definiti dal template, pronti per essere ritirati una sola volta da chi porta il documento nello zaino.

Le coppie personalizzate `name=value` forniscono le variabili dichiarate nel TOML. I nomi
distinguono maiuscole e minuscole; i valori sono stringhe letterali, senza espansione delle variabili. Tutte le variabili dichiarate
devono essere fornite, senza nomi aggiuntivi o duplicati. È consentito un valore vuoto
(`contact_name=`); il valore può contenere `=`. Gli argomenti sono separati da
spazi: i valori tra virgolette contenenti spazi non sono supportati. Per valori più lunghi, usa
[`book.give` in Lua](../data-files/books.md#a-welcome-letter).

Un template sconosciuto segnala la sorgente mancante. Valori non validi, uno
zaino assente o pieno, un inventario occupato o seriali non disponibili causano un errore senza creare un documento.
Dopo un aggiornamento, copia i nuovi messaggi 30181–30184 dal file distribuito
`data/messages/<language>/moongate.toml` in ogni directory radice esistente conservata.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [Template di testi leggibili](../data-files/books.md)
- [Importare i testi dei libri](../book-content-import.md)
