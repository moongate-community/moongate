<!-- translation: {"sourceHash":"00f179d3224b9a308c0b85ba42a61b55b5cd5be5231d4a968e75c26c2e68ec17","title":"File client e query sul mondo"} -->

# File client e query sul mondo

Il server di gioco legge i file client Ultima Online necessari al gameplay:
proprietà dei tile (`tiledata.mul`), mappe con i loro statici e layout multi di case
e barche. Su questi risponde a due domande di ogni sistema di gioco: può un'entità
mobile fare questo passo, e può questo punto vedere quell'altro; e a una terza
basata sulla prima: quale percorso segue da qui a lì. Il codice C# accede a tutto
questo tramite sei servizi di `Moongate.Server.Ultima`, registrati dal plugin Ultima
nelle modalità game e standalone.

| Servizio | Risponde su | Chiamare da |
| --- | --- | --- |
| `ITileDataService` | Nome, flag, peso, altezza di una grafica terreno o oggetto | Qualsiasi thread |
| `IMapService` | Terreno e statici di una cella della mappa | Game loop |
| `IMultiService` | Componenti di una casa, barca o altro multi | Qualsiasi thread |
| `IMovementService` | Se un passo è consentito e la Z di arrivo | Game loop |
| `ILineOfSightService` | Se un punto ne vede un altro | Game loop |
| `IPathfindingService` | Passi percorsi da un'entità tra due luoghi | Game loop |

I lettori delle mappe condividono buffer e un handle di file per mappa, quindi i
servizi di mappe, movimento e linea di vista appartengono al game loop. Dati dei
tile e multi vengono copiati in memoria e poi solo letti.

## Cosa viene letto all'avvio

| Priorità | Servizio | Legge | Arresta il server quando |
| --- | --- | --- | --- |
| -10 | `IUltimaDataService` | `ultima.ultima_path`, versione client, `tiledata.mul` | manca la directory o `tiledata.mul` |
| -5 | `IDataLoaderService` | [file dei dati dello shard](data-files.md), compreso `data/maps.toml` | un file dati manca o non è valido |
| -4 | `IMapService` | `map{n}.mul` o `map{n}LegacyMUL.uop`, `staidx{n}.mul` e `statics{n}.mul` per ogni mappa di `maps.toml`, dove `n` è il suo `file_index` | a una mappa manca uno dei file |
| -4 | `IMultiService` | ogni multi da `MultiCollection.uop`, o da `multi.idx` con `multi.mul` quando il client non ha un file UOP | nessuno dei due formati è presente, o nessun multi può essere letto |

Messaggi di errore e relative soluzioni sono in
[Problemi comuni all'avvio](getting-started.md#common-startup-problems). Imposta la
directory client con [`ultima.ultima_path`](server-configuration.md#settings-and-validation).
Un servizio di plugin che legge mappe o multi si avvia dopo -4; vedi le
[priorità di avvio](plugins.md#what-register-may-do).

## Dati dei tile

```csharp
var backpack = tileDataService.GetItem(0x0E75);   // "backpack", weight 3, Container | Wearable
var water = tileDataService.GetLand(0x00A8);      // "water", Impassable | Wet
```

| Membro | Significato |
| --- | --- |
| `LandCount`, `ItemCount` | Numero di grafiche terreno e oggetto; quello degli oggetti dipende dalla versione client |
| `GetLand(id)`, `GetItem(id)` | Il tile; `ArgumentOutOfRangeException` per un id fuori intervallo |
| `TryGetLand(id, out tile)`, `TryGetItem(id, out tile)` | Il tile, o false per un id fuori intervallo |

`LandTile` ha `Id`, `Name`, `Flags` e `TextureId`. `ItemTile` ha `Id`, `Name`,
`Flags`, `Weight` (255 significa che non può essere raccolto), `Height`, `StandHeight`
(metà altezza per un `Bridge` come una scala), `Layer`, `Quantity` e `Animation`.
La prima chiamata copia le tabelle in array di sola lettura, così ogni ricerca
successiva è un indice di array.

## Mappe

```csharp
var land = mapService.GetLand(MapType.Felucca, 1602, 1591);        // cobblestones, Z 20
var statics = mapService.GetStatics(MapType.Felucca, 1400, 1500);  // willow tree and leaves, Z 10
var name = tileDataService.GetItem(statics[0].Id).Name;
```

| Membro | Significato |
| --- | --- |
| `Maps` | Mappe caricate, nell'ordine di `maps.toml` |
| `Contains(map, x, y)` | Se la mappa è caricata e la cella si trova al suo interno |
| `GetLand(map, x, y)` | Id della grafica terreno e Z della cella |
| `GetStatics(map, x, y)` | Id della grafica oggetto, Z e tonalità di ogni statico, in ordine del file; vuoto quando non ce ne sono |

Una mappa non caricata genera `KeyNotFoundException`; una cella fuori dalla mappa
genera `ArgumentOutOfRangeException`. Blocchi di celle 8x8 vengono letti la prima
volta che una loro cella viene richiesta e mantenuti in una cache limitata.

## Multi

```csharp
var house = multiService.GetMulti(0x64);   // 148 components, from (-3, -3) to (4, 4), height 36
```

| Membro | Significato |
| --- | --- |
| `Count` | Numero di multi caricati |
| `GetMulti(id)` | Il multi; `KeyNotFoundException` quando il client non ne ha uno con questo id |
| `TryGetMulti(id, out multi)` | Il multi, o false |

Una `MultiDefinition` ha `Id` (la grafica oggetto è `0x4000` più l'id), `Min` e `Max`,
i minimi e massimi offset X e Y, `Height`, l'offset Z più alto, e `Components`.
Ogni `MultiComponent` ha `ItemId`, un `Offset` dal centro del multi come `Point3D`
e `Visible`: i componenti nascosti, come il marcatore centrale, occupano solo spazio.

## Movimento

`IMovementService` usa le regole ModernUO, che corrispondono alla previsione del
client: un'entità mobile è alta 16 unità, sale al massimo 2 unità per passo, si
posiziona a metà altezza di un ponte come una scala, e un passo diagonale richiede
libere entrambe le celle accanto.

```csharp
if (movementService.CheckMovement(MapType.Felucca, from, DirectionType.East,
        MovementAbilityType.Walk, out var newZ))
{
    // the step is allowed; the mover lands at newZ
}
```

- `MovementAbilityType.Walk` si muove su terreno, statici e superfici non d'acqua;
  `Swim` entra in acqua; `Walk | Swim` fa entrambi. `PassDoors`, aggiunto a uno dei
  due, attraversa le porte: game master e amministratori lo hanno.
- Gli oggetti a terra in una cella contano come i suoi statici, secondo i dati tile
  della loro grafica: uno invalicabile nel percorso blocca il passo, come una porta
  chiusa, una cassa o un muro posizionato da `.decorate`; uno di superficie che non
  può essere raccolto, come un pavimento o una scala lì posizionati, può sostenere
  l'entità. Una porta aperta si è spostata sulla cella successiva, quindi il varco
  è libero e quella cella no. Un oggetto sopra la testa o sotto i piedi dell'entità
  non blocca.
- Contano solo i tre bit inferiori della direzione, quindi `DirectionType.Running`
  viene ignorato.
- Un passo che lascia la mappa, o parte fuori, restituisce false con `newZ` uguale
  alla Z iniziale; un passo bloccato restituisce false con la Z su cui si trova l'entità.
- `GetAverageZ(map, x, y)` dà l'altezza del terreno al centro di una cella, dai
  suoi quattro angoli.

## Linea di vista

`ILineOfSightService` percorre la linea 3D intera di POL tra due punti e controlla
ogni punto con le regole ModernUO. Passa entrambi i punti all'altezza degli occhi o
del bersaglio; l'occhio di un mobile è la sua Z più 14:

```csharp
var visible = lineOfSightService.HasLineOfSight(MapType.Felucca,
    new Point3D(1602, 1591, 20 + 14), new Point3D(1610, 1591, 20 + 14));
```

- Statici con flag `Window` o `NoShoot` e terreno bloccano la linea; un ostacolo
  nella cella e all'altezza del bersaglio non la blocca.
- Punti più lontani di [`ultima.line_of_sight.max_distance`](server-configuration.md#settings-and-validation)
  (predefinito 25) lungo X o Y non sono mai visibili, e nemmeno un punto fuori dalla mappa.
- Una mappa non caricata genera `KeyNotFoundException`.
- Il servizio non alloca nulla: gli statici di ogni cella vengono letti una volta,
  anche quando la linea la attraversa più volte.

## Ricerca del percorso

`IPathfindingService.FindPath(map, from, to, ability, allowPartial)` trova con A*
il percorso più breve percorribile da un'entità tra due luoghi e restituisce un `PathResult`:

| Campo | Contiene |
| --- | --- |
| `Kind` | `Found`, `Partial`, `NotFound` o `TooFar` |
| `Steps` | Direzioni da percorrere dall'inizio, una per passo; vuoto quando non c'è un percorso |
| `End` | Dove portano i passi: destinazione, punto più vicino per un percorso parziale, o partenza |

```csharp
var path = pathfinding.FindPath(npc.Map, npc.Location, target.Location, MovementAbilityType.Walk, true);

// A path to where the NPC already stands is found with no steps.
if (path.Kind is PathResultType.Found or PathResultType.Partial && path.Steps.Count > 0)
{
    mobiles.TryMove(npc, path.Steps[0]);
}
```

Ogni passo del percorso è consentito da `IMovementService.CheckMovement`, dall'altezza
raggiunta dal passo precedente, quindi un percorso è fatto di passi realmente
percorribili: una diagonale richiede entrambi i tile accanto, e un nuotatore
(`MovementAbilityType.Swim`) riceve un percorso in acqua. Un passo diritto costa
10 e uno diagonale 14, come ModernUO, UOX3 e Sphere, e la distanza ottile dalla
destinazione guida la ricerca, così il percorso trovato è il più breve entro ciò
che è stato esaminato. La destinazione è raggiunta quando un passo arriva sul suo
tile entro un'altezza dell'entità (16) da essa.

Due impostazioni limitano una ricerca:

| Chiave | Valore predefinito | Limiti |
| --- | --- | --- |
| `ultima.world.pathfinding_range` | 38, come ModernUO | Distanza massima tra partenza e destinazione lungo X o Y; oltre è `TooFar` senza cercare nulla. La ricerca esamina un quadrato di questa dimensione più uno, posizionato per contenerle entrambe |
| `ultima.world.pathfinding_max_nodes` | 1000, come ModernUO | Numero di posizioni espanse dalla ricerca prima di rinunciare con `NotFound` |

Con `allowPartial`, una destinazione irraggiungibile dà il percorso al punto più
vicino tra quelli espansi dalla ricerca (`Partial`), a meno che nessuno sia più
vicino della partenza.

Una ricerca viene eseguita interamente nella chiamata, sul game loop. Misurato su
Trammel del client 7.0 attorno a Britain: un percorso di 4 passi richiede 0,1 ms,
uno di 36 passi 1,3 ms, uno di 48 passi 6,6 ms, e una ricerca che non trova nulla
11–12 ms, le 1.000 posizioni del limite. Quindi un chiamante non deve cercare di
nuovo a ogni tick una destinazione non raggiunta: attendi prima di riprovare, come
i due secondi di ModernUO. `INpcPathService` lo fa per gli NPC: mantiene il percorso
di ciascuno e fornisce il passo successivo (`Next`, poi `Stepped` una volta tentato
il passo). Cerca solo quando non ci sono passi o cambia destinazione, almeno due
secondi dopo l'ultima ricerca dell'NPC, dieci quando quella ricerca non ha raggiunto
la stessa destinazione, e per dieci NPC al secondo nell'intero server; un NPC che
non può cercare fa un passo diritto verso la destinazione. La funzione Lua
[`npc.walk_to`](scripting/mobile-scripts.md#walking-a-path) è costruita su questo.

Un percorso vede ciò che vede il movimento: una porta chiusa, una cassa o qualsiasi
oggetto invalicabile a terra lo blocca, e ci gira attorno. Una ricerca con `MovementAbilityType.OpenDoors` pianifica attraverso le porte chiuse non a chiave, ed è così che `npc.walk_to` cerca per un NPC che apre le porte; il passo stesso viene comunque fermato dalla porta, e l'NPC prima la apre.
Altri mobile non bloccano un percorso, perché il movimento non li considera.
Un tile lungo il percorso ha una sola altezza nella ricerca, quella del percorso
più breve fino a esso, quindi un percorso non può passare sia sopra sia sotto lo
stesso tile, come attraverso un ponte e poi sotto. Il tile della destinazione è
diverso: vi si entra solo all'altezza della destinazione, così una destinazione
su un balcone si raggiunge dalle scale senza fermarsi a terra sotto.

Segui un percorso parziale fino alla fine prima di cercare ancora: una ricerca da
ogni nuovo tile può scegliere un altro punto più vicino, e l'entità camminerebbe
avanti e indietro. Una destinazione su cui nessuno può stare, come un muro, costa
sempre una ricerca intera.

## Non ancora incluso

Mobile e multi posizionati non fanno parte dei controlli di movimento e linea di
vista, e gli oggetti a terra fanno parte solo del movimento: una linea di vista
attraversa ancora una porta chiusa. La posizione di un NPC o di un oggetto lasciato
(`TryGetSpawnZ`, `TryGetDropZ`) considera terreno e statici, non gli oggetti a terra.
Vedi [Stato dell'implementazione](implementation-status.md).
