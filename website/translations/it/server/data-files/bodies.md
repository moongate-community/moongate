<!-- translation: {"sourceHash":"12db13e0b05b3d3af8ce2556f3241c0af1515a61d62f44cfe151b543233fa5b8","title":"Corpi"} -->

# Corpi

`bodies.toml` indica che tipo di creatura è ciascun corpo. C'è un elenco per tipo:
`human`, `animal`, `monster`, `sea` ed `equipment` (`BodyType`). Ogni voce è un
singolo ID o un intervallo inclusivo `"min-max"`, sempre tra virgolette:

```toml
human = [
    "183-186", "400-403", "605-608", "666-667", "694-695", "744-745",
    "750-751", "987-988", "990-991", "994", "1253",
]

sea = [
    "144-145", "150-151",
]
```

Un corpo non elencato conta come `Empty`. Il loader restituisce un `BodyContent` per ID del corpo,
ordinato per ID, non uno per voce. Nessun sistema legge ancora i tipi dei corpi.

## Validazione all'avvio

Il server si arresta quando:

- `bodies.toml` non esiste;
- una voce non è un numero o un intervallo `"min-max"`, un intervallo inizia dopo la propria fine, oppure
  un ID è superiore a 0xFFFF;
- un ID del corpo è elencato sotto due tipi.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
