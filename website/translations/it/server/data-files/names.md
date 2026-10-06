<!-- translation: {"sourceHash":"6c5d9ff20f6e7287b8db1bffd3db30efebf7b4b63bdd81c18abb52ef8d5f41c3","title":"Nomi"} -->

# Nomi

`names.toml` contiene le liste da cui vengono estratti nomi NPC casuali. Un template
mobile nomina una lista con `name_list`; `INameService.RandomName(listId)` ne sceglie un nome.

```toml
[[names]]
id = "male"
names = [
    "Aaron",
    "Abbott",
]
```

| Campo | Significato |
| --- | --- |
| `id` | Id della lista, univoco senza distinguere maiuscole e minuscole |
| `names` | Nomi |

Il file distribuito ha le venti liste UOX3 (`namelists.dfn`), convertite da
[`mgctl convert uox`](../uox3-migration.md#mobiles-and-name-lists): `male`, `female`,
`orc`, `daemon`, `ratman` e così via. Il loader rimuove spazi iniziali e finali da
id e nomi e restituisce un `NameList` per lista.

## Validazione all'avvio

Il server si arresta quando:

- `names.toml` non esiste;
- un id di lista è vuoto, o usato due volte senza distinguere maiuscole e minuscole;
- una lista è vuota, o un nome è vuoto dopo la rimozione degli spazi.

## Vedi anche

- [Panoramica dei file dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
