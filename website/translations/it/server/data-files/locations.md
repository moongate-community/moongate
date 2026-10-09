<!-- translation: {"sourceHash":"5989b9108244db8df37989769ee2608ee0fe7b8b64182a020f4e1cc31eb55c03","title":"Luoghi"} -->

# Luoghi

`locations.toml` elenca i luoghi con nome verso cui si sposta lo staff: [`.go`](../commands/go.md) senza argomenti apre un
gump che li elenca per mappa e categoria, mentre `.go <place>` raggiunge un luogo per nome.

```toml
[[location]]
map = "felucca"
category = "Dungeons/Covetous"
name = "Entrance"
location = "(2499, 919, 0)"

[[location]]
map = "malas"
category = ""
name = "Arena"
location = "(1, 2, -3)"
```

| Campo | Significato |
| --- | --- |
| `[[location]]` | Uno per luogo; l'ordine è quello del gump. |
| `map` | `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` o `termur`. |
| `category` | Dove il gump colloca il luogo: le categorie a partire dalla mappa, unite da `/`. Vuoto, oppure omesso, per un luogo elencato direttamente sotto la mappa. |
| `name` | Il nome mostrato. I nomi possono ripetersi: `.go` li distingue tramite le parole della categoria. |
| `location` | Dove arriva il viaggiatore, un `Point3D`. |

Una categoria esiste perché un luogo la nomina: non c'è altro da dichiarare. Le categorie che
differiscono solo per maiuscole e minuscole sono una sola, scritta come nel primo luogo che la usa.

Il file distribuito contiene i 558 luoghi del gump `[Go` di ModernUO, sulle sei mappe. Rigeneralo
da un checkout di ModernUO con
[`moongate-convert modernuo-locations`](../uox3-migration.md#named-places-of-modernuo).

Un luogo di una mappa che il server non carica viene escluso, così come uno esterno alla sua mappa. Il file
può mancare: in questo caso `.go` accetta solo numeri.

Gli script leggono i luoghi con `locations.node(path)` e `locations.find(text, map)`; vedi
il [modulo `locations`](https://moongate.sh/lua/locations/).

## Validazione all'avvio

Il server si arresta all'avvio quando:

- un `[[location]]` non ha `name`, non ha `map` oppure ha una mappa sconosciuta;
- `location` manca o è `(0, 0, 0)`, oppure ha `x` o `y` fuori dall'intervallo da 0 a 65535 o `z` fuori dall'intervallo da -128 a 127;
- `category` ha una parte vuota, come `Dungeons//Covetous`.

## Vedi anche

- [`go`](../commands/go.md): il comando che elenca i luoghi e permette di raggiungerli.
- [Panoramica dei file di dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Verificare le modifiche](../data-files.md#check-your-changes): valida i dati modificati prima di riavviare.
