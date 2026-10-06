<!-- translation: {"sourceHash":"373069084d413fd4e1c26778834ce390df5a591551a29d8db12599415647590e","title":"Moongate.Core"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Core

Primitive condivise, geometria, collezioni, helper di configurazione e utilità per le applicazioni Moongate.

## Installazione

Richiede .NET 10. Usa la versione del pacchetto disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Core
```

## Funzionalità

- Identità delle entità con `Serial` e il contratto `IMoongateEntity`.
- Punti bidimensionali e tridimensionali, rettangoli e interfacce geometriche.
- Collezioni, buffer e helper di uso generale.
- Helper di configurazione TOML con nomi snake_case, informazioni sulla versione e utilità per gli indirizzi di rete.

## Esempio

Crea una posizione e un identificatore persistente di entità:

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;

var position = new Point3D(100, 200, 5);
var id = new Serial(1);

Console.WriteLine($"{id}: {position.X}, {position.Y}, {position.Z}");
```

## Ricette per configurazione, versione e indirizzi

Inserisci questo modello in `ShardOptions.cs`:

```csharp
public sealed class ShardOptions
{
    public string ShardName { get; set; } = "Moongate";
    public int GamePort { get; set; } = 2593;
}
```

Esegui questo `Program.cs` per scrivere e rileggere un file TOML e ispezionare l'assembly chiamante:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Moongate.Core.Utils;

var path = Path.Combine(Path.GetTempPath(), $"moongate-options-{Guid.NewGuid():N}.toml");
try
{
    await TomlUtils.SerializeToFileAsync(new ShardOptions(), path);
    var restored = await TomlUtils.DeserializeFromFileAsync<ShardOptions>(path)
        ?? throw new InvalidOperationException("Configuration was empty");
    Console.WriteLine(await File.ReadAllTextAsync(path)); // shard_name, game_port
    if (restored.ShardName != "Moongate" || restored.GamePort != 2593)
    {
        throw new InvalidOperationException("TOML round trip failed");
    }
}
finally
{
    File.Delete(path);
}

var assembly = Assembly.GetExecutingAssembly();
Console.WriteLine($"Application version: {VersionUtils.GetVersion(assembly)}");
Console.WriteLine($"Application codename: {VersionUtils.GetCodename(assembly)}");

var addresses = NetworkUtils.GetLocalIpAddresses()
    .Where(address => address.AddressFamily == AddressFamily.InterNetwork &&
                      !IPAddress.IsLoopback(address))
    .Distinct();
foreach (var address in addresses)
{
    Console.WriteLine(address);
}
```

`TomlUtils.Serialize`/`Deserialize<T>` lavorano con stringhe; le varianti per file e file asincroni
creano le directory superiori durante la scrittura. Le opzioni predefinite usano
nomi `snake_case` più gli eventuali convertitori aggiunti con il metodo thread-safe
`TomlUtils.AddTomlConverter`. Opzioni `TomlSerializerOptions` esplicite sostituiscono questi valori predefiniti
per quella chiamata.
Le scritture sovrascrivono la destinazione e non sono atomiche; gli errori di parsing, serializzazione e I/O
si propagano al chiamante. Il comportamento del server che crea i valori predefiniti se mancanti
appartiene al suo `ConfigHelper`, non a ogni scrittura `TomlUtils`.

`VersionUtils.GetVersion()` senza un assembly legge la versione di **Moongate.Core**.
Passa l'assembly della tua applicazione per leggerne la versione, rimuovendo i metadati di compilazione
successivi a `+`. Il nome in codice legge il valore `AssemblyMetadata("Codename", "...")` e
restituisce una stringa vuota quando assente. Il server incorpora la propria proprietà MSBuild `Codename`
in questi metadati tramite la configurazione del progetto; dichiarare semplicemente
una proprietà MSBuild arbitraria in un altro progetto non la incorpora automaticamente.

`VersionUtils.GetBuildTime` e `GetBuildConfiguration` leggono le voci `AssemblyMetadata`
`BuildTime` (ISO 8601, UTC) e `BuildConfiguration` (`Debug` o `Release`) che un'applicazione scrive
alla compilazione; restituiscono `null` e una stringa vuota quando l'assembly non le contiene.
`VersionUtils.FormatHeader` sostituisce `{Version}`, `{Codename}`, `{Configuration}` e `{BuildTime}` nel
 testo di un'intestazione, scrivendo `unknown` per ciò che l'assembly non contiene.

`NetworkUtils.GetLocalIpAddresses()` elenca gli indirizzi unicast locali senza
filtrare loopback, interfacce inattive, duplicati o famiglia di indirizzi. Applica i
filtri adatti alla tua applicazione, come sopra. Sono indirizzi di interfacce locali,
non il tuo indirizzo NAT pubblico. `GetListeningAddresses(endpoint)` formatta gli
indirizzi locali della famiglia dell'endpoint con la sua porta; non ispeziona la
tabella dei socket del sistema operativo per individuare i listener effettivi.

## Dipendenze e ambito

Questo pacchetto non dipende da altri pacchetti Moongate. Le sue dipendenze esterne comprendono DryIoc, Humanizer, Serilog,
ShaiRandom, Tomlyn e ZLinq; NuGet le risolve automaticamente.

Fornisce elementi costitutivi condivisi. L'archiviazione asincrona delle entità in PostgreSQL è fornita da `Moongate.Persistence`; il trasporto
TCP è fornito da `Moongate.Network`.

## Licenza e sorgenti

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
