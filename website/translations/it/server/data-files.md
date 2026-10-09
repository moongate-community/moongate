<!-- translation: {"sourceHash":"a22e929a43e54c61b99eaec6000c418148bf357d166ac318849d6e6db6e43774","title":"Panoramica"} -->

# File dei dati dello shard

I file dei dati dello shard descrivono il mondo eseguito dal server di gioco: mappe,
città iniziali, abilità, professioni, razze, contenitori, corpi, meteo, regioni,
messaggi e titoli di reputazione. Sono file TOML sotto `data/` nella radice del server.
Il server li legge una volta all'avvio, nelle modalità game e standalone, tramite i
loader `IDataLoader<T>` di `Moongate.Server.Ultima`. Un file obbligatorio mancante o
non valido arresta il server all'avvio con un errore che indica il problema.
Il [MOTD](motd.md) è facoltativo: un file mancante registra un avviso e non invia
alcun messaggio di benvenuto.

I file sono distribuiti accanto al binario `mgserver`, in `data/`; la copia nel
repository è `moongate_root/data/`. `mgctl` (o `mgserver --initialize-root`) copia
ogni file mancante in `<root>/data` senza mai sostituire quelli esistenti, così un
file modificato sopravvive a un aggiornamento. Dopo un aggiornamento, esegui di nuovo
`mgctl init` per aggiungere nuovi file e confronta i file modificati con quelli
distribuiti per recepire le modifiche upstream. Vedi [Cosa crea](mgctl.md#what-it-creates).

Il codice C# legge le voci caricate tramite `IDataLoaderService`:

```csharp
var cities = dataLoaderService.GetEntities<StartingCityContent>();
```

`GetEntities<T>()` restituisce le voci di un file, o di una directory per regioni e
messaggi. Genera `InvalidOperationException` quando nessun loader è registrato per `T`.

## Panoramica

Scegli un file per aprire esempi, riferimento dei campi e regole di validazione all'avvio.

I loader vengono eseguiti nell'ordine sotto; un loader che controlla un altro file
viene eseguito dopo di esso.

| File | Tipo di contenuto | Caricato dopo / dipende da | Usato al runtime |
| --- | --- | --- | --- |
| <span id="maps"></span><span id="validation-at-startup"></span><span id="read-the-map-from-code"></span>[`maps.toml`](data-files/maps.md) | `MapContent` | primo; il suo `weather` è controllato dal loader delle regioni | Sì, `IMapService` apre i file client di ogni mappa |
| <span id="starting-cities"></span><span id="validation-at-startup-1"></span>[`starting_cities.toml`](data-files/starting-cities.md) | `StartingCityContent` | mappe | Sì, nell'elenco dei personaggi |
| <span id="moongates"></span>[`moongates.toml`](data-files/moongates.md) | `MoongateFacet` | mappe | Sì, da `.decorate` e dallo script moongate |
| <span id="locations"></span>[`locations.toml`](data-files/locations.md) | `NamedLocation` | facoltativo | Sì, da `.go` e dal suo gump |
| <span id="schedule"></span>[`schedule.toml`](data-files/schedule.md) | `ScheduleFile` | facoltativo | Sì, dal servizio del calendario e da `.event` |
| <span id="jail"></span>[`jail.toml`](data-files/jail.md) | `JailFile` | facoltativo; prima dei template dei libri | Sì, da `.jail` e dal suo gump |
| <span id="skills"></span><span id="validation-at-startup-2"></span>[`skills.toml`](data-files/skills.md) | `SkillContent` | città iniziali | No |
| <span id="professions"></span><span id="validation-at-startup-3"></span>[`professions.toml`](data-files/professions.md) | `ProfessionContent` | abilità (ogni abilità iniziale deve esistere) | Sì, creazione dei personaggi |
| <span id="races"></span><span id="validation-at-startup-4"></span>[`races.toml`](data-files/races.md) | `RaceContent` | professioni | Sì, creazione dei personaggi e aspetto dei mobile |
| <span id="banned-names"></span><span id="validation-at-startup-5"></span>[`banned_names.toml`](data-files/banned-names.md) | `BannedNamesContent` | razze | Sì, validazione del nome del personaggio |
| <span id="containers"></span><span id="validation-at-startup-8"></span>[`containers.toml`](data-files/containers.md) | `ContainerContent` | nomi vietati | Sì, posizionamento degli oggetti nello zaino |
| <span id="bodies"></span><span id="validation-at-startup-9"></span>[`bodies.toml`](data-files/bodies.md) | `BodyContent` | contenitori | No |
| <span id="weather"></span><span id="validation-at-startup-10"></span>[`weather.toml`](data-files/weather.md) | `WeatherContent` | corpi | Sì, `IWeatherService` estrae il meteo di ogni profilo e lo invia ai giocatori |
| <span id="regions"></span><span id="areas"></span><span id="parents-and-overlaps"></span><span id="travel-zones"></span><span id="validation-at-startup-11"></span><span id="add-a-region"></span>[`regions/<map>.toml`](data-files/regions.md) | `RegionContent` | meteo (ogni profilo deve esistere), mappe | Sì, `IRegionService` mantiene la regione di ogni giocatore per musica, meteo, stagione e luce; le regole di guardie, abitazioni e viaggio non sono ancora lette |
| <span id="messages"></span>[`messages/<lang>.toml`, `messages/<lang>/*.toml`](data-files/messages.md) | `MessageContent` | regioni | Sì, tramite `ILocalizationService` |
| <span id="names"></span><span id="validation-at-startup-6"></span>[`names.toml`](data-files/names.md) | `NameList` | messaggi | Sì, tramite `INameService` |
| [`motd.toml`](motd.md) | `MotdLine` | dopo template mobile e registrazione delle variabili dei plugin | Sì, a ogni ingresso del personaggio; file facoltativo |
| [`titles.toml`](data-files/titles.md) | `FameKarmaTitle` | dopo MOTD | Mostrato nel titolo del paperdoll |
| [`templates/books/<name>.toml`](data-files/books.md) | `BookTemplate` | dopo i template oggetto | Pergamene personalizzate e libri nativi; gump delle pergamene, copertine/pagine dei libri e libri scrivibili |
| [`templates/shops/<name>.toml`](data-files/shops.md) | `ShopDefinition` | dopo i template oggetto e mobile | Sì, tramite `IShopService`: cosa vende ogni venditore nella sua [finestra del negozio](vendors.md) |
| [`harvest.toml`](data-files/harvest.md) | `HarvestResource` | facoltativo; dopo i negozi | Sì, tramite `IHarvestService`: i pesci della [pesca](fishing.md), per zona |
| <span id="starting-items"></span><span id="validation-at-startup-7"></span>[`starting_items.toml`](data-files/starting-items.md) | `StartingItemSet` | dopo template oggetto e template dei libri (ogni id referenziato deve esistere) | Sì, tramite `IStartingItemsService` |

"No" significa che il file viene caricato e validato, ma nessun sistema di gioco lo
legge ancora. Un errore in un tale file arresta comunque il server.

## Formati dei valori

I nomi dei campi sono snake_case. Alcuni campi usano tipi di valore con una propria
forma TOML; [Tipi di valore TOML](toml-types.md) elenca ogni forma accettata ed errore:

| Tipo | Forma | Esempio |
| --- | --- | --- |
| `Point2D` | `"(x, y)"` tra virgolette | `size = "(7168, 4096)"` |
| `Point3D` | `"(x, y, z)"` tra virgolette | `location = "(1602, 1591, 20)"` |
| `Serial` | intero senza virgolette, decimale o esadecimale, oppure lo stesso tra virgolette | `cliloc = 1150168` |
| `Rectangle2D` | `"(x1, y1)..(x2, y2)"` tra virgolette | `bounds = "(44, 65)..(186, 159)"` |
| `HueSpec` | intero senza virgolette, oppure tonalità o intervallo `"min-max"` tra virgolette | `0x00BF`, `"0x03EA-0x0422"` |
| `DiceSpec` | intero senza virgolette, oppure espressione di dadi tra virgolette | `karma = -2500`, `strength = "1d25+95"` |
| Qualsiasi enum | nome tra virgolette, maiuscole/minuscole e underscore ignorati; flag uniti da `\|` | `map = "felucca"`, `music = "mountn_a"` |

`Rectangle2D` scrive due angoli: il primo incluso e il secondo escluso.
Il formato legacy `"(x, y)+(width, height)"` è ancora accettato in lettura.

Un valore che non corrisponde alla propria forma non viene interpretato e arresta il server.

## Controllare le modifiche

Prima di riavviare un server, carica i file del repository con i loader reali:

```sh
scripts/test.sh fast --filter 'FullyQualifiedName~RepositoryDataFiles'
```

Il test carica ogni file di `moongate_root/data`, nell'ordine del server, e ogni lingua
distribuita. Controlla anche alcuni valori noti, come il numero di abilità e regioni,
quindi aggiornalo quando aggiungi o rimuovi voci.

Per i file della radice di un server in esecuzione, riavvia il server: i file vengono
letti solo all'avvio. Un file errato interrompe l'avvio, e il log mostra l'errore.

## Vedi anche

- [Caricare template TOML](templates.md): contratto `IDataLoader<T>`, registrazione
  ed esecuzione dei loader e convertitori TOML.
- [Localizzazione](localization.md): file dei messaggi e `ILocalizationService`.
- [Preparare una radice del server con mgctl](mgctl.md): come i file dei dati arrivano nella radice.
