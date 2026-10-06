<!-- translation: {"sourceHash":"988ddd57fc55d3cb381387f93c7b2bcd0e7308d101d08dff1fb0223c287b0d4b","title":"Contenitori"} -->

# Contenitori

`containers.toml` indica come il client mostra ogni tipo di contenitore:

```toml
[[container]]
name = "default"
gump = 0x003C
bounds = "(44, 65)..(186, 159)"
drop_sound = 0x0048
default = true
items = []

[[container]]
name = "bag"
gump = 0x003D
bounds = "(29, 34)..(137, 128)"
drop_sound = 0x0048
items = [0x0E76, 0x2256, 0x2257]
```

| Campo | Significato |
| --- | --- |
| `name` | Un'etichetta per chi legge il file e per i log. Facoltativa. |
| `gump` | ID del gump aperto dal client. |
| `bounds` | Area del gump in cui si possono posizionare oggetti, un `Rectangle2D`. |
| `drop_sound` | Suono di un oggetto inserito. Omettilo per nessun suono. |
| `items` | ID degli oggetti (grafiche) dei contenitori che usano questa voce. |
| `default` | `true` sull'unica voce usata per i contenitori non elencati. |

`IContainerLayoutService` risolve questi layout. La creazione di oggetti iniziali e mobile
usa i loro limiti per posizionare oggetti negli zaini, e la finestra del contenitore
(`0x24`) si apre con il gump del layout; vedi [Pacchetti](../packets.md).

## Validazione all'avvio

Il server si arresta quando:

- `containers.toml` non esiste;
- non c'è esattamente una voce con `default = true`;
- un `gump` è inferiore a 1, oppure `bounds` è più piccolo di 1x1;
- un ID oggetto viene elencato da due voci.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
