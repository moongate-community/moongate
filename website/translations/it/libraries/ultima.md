<!-- translation: {"sourceHash":"36b63a5ddfffed54a18ba086625fef51a4938bae549ea01ab3cc449194691ff0","title":"Moongate.Ultima"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Ultima

Lettori dei dati del client Ultima Online e utilità di rendering per risorse MUL/UOP, mappe, grafica e localizzazione.

## Installazione

Richiede .NET 10. Usa la versione del package disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Ultima
```

## Funzionalità

- Lettori dei dati del client Ultima Online, incluse risorse MUL e UOP.
- API per mappe, grafica, gump, animazioni, font, audio e localizzazione.
- Individuazione dei file del client e lettura della versione del client.
- Utilità per bitmap e supporto al rendering tramite SkiaSharp.

## Esempio

Crea una superficie in memoria e convertila in una bitmap SkiaSharp di proprietà del chiamante. Questo esempio non richiede file del client UO.

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Ultima.Imaging;

using var bitmap = new UltimaBitmap(2, 2);
using var image = bitmap.ToImage();

Console.WriteLine($"{image.Width}x{image.Height}");
```

## Leggere grafica del client, una casella della mappa e localizzazione

Il seguente `Program.cs` accetta come argomento una directory del client. Configura la
mappatura globale dei file **prima** di accedere ai lettori statici delle risorse. Usa un'installazione
del client per processo; cambiare `Files.SetDirectory` non è un ricaricamento
coordinato di ogni lettore/cache già inizializzato.

```csharp
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Localization;
using Moongate.Ultima.Maps;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    throw new ArgumentException("Pass an existing Ultima Online client directory");
}
var clientDirectory = Path.GetFullPath(args[0]);
Files.SetDirectory(clientDirectory);

// Land art is a 44x44 image in 16bppArgb1555. This buffer belongs to the caller.
var pixels = new ushort[44 * 44];
if (Art.TryGetLandPixels(0, pixels, out var patched))
{
    Console.WriteLine($"Land art decoded; patched: {patched}, pixels: {pixels.Length}");
}
else
{
    Console.WriteLine("Land art 0 is unavailable");
}

var mapPath = Files.GetFilePath("map0.mul") ?? Files.GetFilePath("map0LegacyMUL.uop");
if (mapPath is not null)
{
    // Use dimensions matching the client map; these are the modern Felucca defaults.
    // The implementation accepts null for Files mapping despite its non-null annotation.
    using var tiles = new TileMatrix(0, 0, 6144, 4096, path: null!);
    var tile = tiles.GetLandTile(0, 0);
    Console.WriteLine($"Map tile: {tile.Id}, Z: {tile.Z}");
}
else
{
    Console.WriteLine("Map 0 is unavailable");
}

var clilocPath = Files.GetFilePath("cliloc.enu");
if (clilocPath is not null)
{
    var strings = new StringList("enu", clilocPath, decompress: false);
    if (!string.IsNullOrEmpty(strings.LoadWarning))
    {
        Console.WriteLine(strings.LoadWarning);
    }
    Console.WriteLine(strings.GetString(3000000) ?? "Localization entry is unavailable");
}
else
{
    Console.WriteLine("English localization is unavailable");
}
```

Esegui con `dotnet run -- /absolute/path/to/client`. La ricerca dei file gestisce i nomi
noti del client senza distinguere le maiuscole. Alcune versioni del client omettono risorse o usano una
dimensione diversa della mappa; controlla esplicitamente gli input richiesti e segnala gli errori di parsing/I/O.
Il lettore della localizzazione prova l'interpretazione alternativa della compressione quando
necessario e segnala i dati parzialmente recuperati tramite `LoadWarning`.

`Art.GetLand`/`GetStatic` offrono API bitmap, ma possono restituire oggetti appartenenti alla cache;
non liberarli o modificarli come fossero copie private. L'API pixel sopra
evita questa ambiguità di proprietà. Un `TileMatrix` creato dalla tua applicazione è
liberabile e possiede gli handle dei file. Le istanze `UltimaBitmap` costruite e
i risultati `ToImage()` creati sono di proprietà del chiamante e devono essere liberati, come nel
primo esempio. Questi esempi mostrano la lettura; non modificano i file del client.

## Dati del client e dipendenze native

Fornisci i tuoi dati del client Ultima Online quando usi i lettori delle risorse e configurane la posizione tramite
`Moongate.Ultima.Io.Files.SetDirectory`. Le risorse del client non sono incluse in questo package.

Il package dipende da SkiaSharp, `SkiaSharp.NativeAssets.Linux.NoDependencies` e `System.IO.Hashing`. NuGet risolve
queste dipendenze. Il rendering richiede il runtime nativo SkiaSharp appropriato per la piattaforma di distribuzione; verifica quel
runtime sui sistemi in cui verrà eseguita la tua applicazione.

Questo package non dipende da altri package Moongate. Fornisce API per i dati del client, non un server di gioco o un client
 di gioco completo.

## Licenza e sorgenti

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
