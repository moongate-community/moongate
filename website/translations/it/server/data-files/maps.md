<!-- translation: {"sourceHash":"5a6404b6656a58810744c4b92693ec6c4db00f4ebd93d78aa82d80942c9b97a4","title":"Mappe"} -->

# Mappe

`maps.toml` elenca i facet dello shard:

```toml
[[map]]
map = "felucca"
file_index = 0
name = "Felucca"
size = "(7168, 4096)"
rules = "FeluccaRules"
season = "desolation"
weather = "temperate"
```

| Campo | Significato |
| --- | --- |
| `map` | Id della mappa inviato al client: `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` o `termur` (`MapType`). |
| `file_index` | Numero dei file client della mappa: `map{n}.mul` o `map{n}LegacyMUL.uop`, `staidx{n}.mul` e `statics{n}.mul`. |
| `name` | Nome mostrato nei log e nei comandi. |
| `size` | Larghezza e altezza in tile, un `Point2D`. |
| `rules` | Nome dell'insieme di regole della mappa. |
| `season` | Stagione del pacchetto 0xBC: `spring`, `summer`, `fall`, `winter` o `desolation`; punto iniziale della rotazione, vedi [Stagioni](#seasons). Valore predefinito `spring`. |
| `weather` | Profilo di `weather.toml` usato dove nessuna regione copre un luogo. Valore predefinito `none`. |
| `music` | Traccia musicale, un nome `MusicType` come `Britain1`, riprodotta dove nessuna regione con musica copre un luogo. Se omessa, lì la musica si ferma; nessuna mappa distribuita ne imposta una, quindi fuori dalle regioni c'è silenzio, come ModernUO. |

Il file distribuito elenca le sei mappe; Felucca e Trammel usano `temperate`, le altre `none`.

Alla creazione del personaggio, il client indica le mappe installate come
`ClientFlags` (`src/Moongate.Ultima/Types/ClientFlags.cs`); nulla le confronta ancora con questo file.

## Validazione all'avvio

Il server si arresta quando:

- `maps.toml` non esiste;
- `weather` di una mappa non è un profilo di `weather.toml`. Il loader delle regioni
  esegue questo controllo, poiché le mappe si caricano prima dei profili meteo;
- alla directory client manca il file mappa, `staidx` o `statics` del `file_index`
  di una mappa. `IMapService` esegue il controllo dopo i loader; rimuovi la mappa
  da `maps.toml` quando il client non ha i suoi file.

## Leggere la mappa dal codice

`IMapService` legge terreno e statici di queste mappe, e `IMovementService` e
`ILineOfSightService` rispondono a domande di movimento e vista su di esse; vedi
[File client e query sul mondo](../world-queries.md).

## Stagioni

Il client disegna la stagione da solo: alberi verdi e fiori in primavera, aspetto
normale in estate, colori autunnali in autunno, alberi spogli e neve in inverno,
alberi morti e terreno grigio in desolazione. Nient'altro cambia: il meteo resta quello della regione.

Un giocatore vede la stagione della propria regione quando questa ne imposta una
(`season` in [`data/regions`](regions.md)), altrimenti quella della mappa.
La stagione della mappa è:

1. quella impostata da un game master con [`.season`](../commands/season.md), fino al riavvio;
2. altrimenti `season` qui. Con `[ultima.world] season_rotation = true` ruota tra
   primavera, estate, autunno e inverno, partendo da quella stagione e cambiando
   ogni `days_per_season` giorni di gioco (12 per impostazione predefinita, un
   giorno reale con il minuto di gioco predefinito). Una mappa `desolation`, Felucca
   come distribuita, non ruota mai. Nessun altro emulatore ruota le stagioni, quindi
   è disabilitato per impostazione predefinita.

La stagione viene inviata (0xBC) al login, quando un giocatore entra in una regione
o viene teletrasportato su una mappa con un'altra stagione, entro un minuto da una
rotazione e subito con `.season`. Viene inviata solo quando differisce dall'ultima
ricevuta dal client; luce e meteo seguono, poiché il client li reimposta.

## Vedi anche

- [Panoramica dei file dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
