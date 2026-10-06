<!-- translation: {"sourceHash":"701bf6e32972282294750b692be4e7e795f9fb70eac68146649e25d94ad6dddb","title":"Moongate.Persistence.Migrations"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence.Migrations

Individuazione e validazione delle migrazioni SQL PostgreSQL versionate per .NET 10, indipendenti dal driver.

```shell
dotnet add package Moongate.Persistence.Migrations
```

L'SQL principale risiede in `migrations/auth` o `migrations/world`. I pacchetti dei plugin contengono
`migrations/manifest.json` con un `id` stabile e le stesse directory di destinazione.
I file usano sequenze positive di quattro cifre: `0001_create_characters.sql`.

Il catalogo ordina prima il core, poi i plugin per ID e infine la sequenza del componente.
`MigrationHistory.Validate` rifiuta modifiche all'SQL applicato, file mancanti nei
componenti installati e nuove sequenze precedenti inserite. I checksum SHA-256
normalizzano CRLF in LF e ignorano un BOM UTF-8. Le cronologie dei plugin rimossi rimangono.

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Persistence.Migrations.Types.Migrations;

Console.WriteLine(MigrationTarget.World);
```

`MigrationCatalog.Load(directory, pluginsDirectory, target)` legge l'SQL senza
caricare le DLL dei plugin. `MigrationHistory.ReadAsync` usa una factory di
`DbCommand` di proprietà del chiamante; non crea mai oggetti del database né esegue migrazioni.

Usa `mgctl migrate apply` per applicare l'SQL revisionato
con DbUp. Vedi la [guida alla persistenza](https://moongate.sh/server/persistence/).

Distribuito con licenza AGPL-3.0-or-later. Vedi il
[repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
