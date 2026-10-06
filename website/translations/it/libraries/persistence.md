<!-- translation: {"sourceHash":"108b3b07bc7372d16f09712b83c96197d71d504dc43957847a001f13eb1f4031","title":"Moongate.Persistence"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Persistence

Persistenza PostgreSQL asincrona per entità identificate da seriale, basata su FreeSql.

## Installazione

Moongate.Persistence richiede .NET 10 e PostgreSQL. Usa la versione di Moongate disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Persistence
```

`InitializeAsync` apre e verifica ogni database di runtime configurato con `SELECT 1`,
anche quando nessuna entità usa quella destinazione. Ogni successo registra `Postgres connection successful`;
un errore genera un'eccezione prima che il sistema sia pronto. Le connessioni vengono verificate prima delle migrazioni o della sincronizzazione
dello schema, e i database mancanti non vengono mai creati automaticamente. Il server
configura sia Accounts sia Realm in ogni modalità; le integrazioni della libreria selezionano le
proprie destinazioni in `PostgreSqlPersistenceOptions`.

## Funzionalità

- Registrazione delle entità Auth/World con moduli interni e schemi PostgreSQL automatici.
- Letture asincrone di entità scollegate, filtri tradotti in SQL, upsert ed eliminazioni.
- Scritture raggruppate in una transazione asincrona per una singola destinazione database.
- Verifiche di disponibilità dell'SQL versionato, anteprima dello schema proposto e sincronizzazione esplicita per lo sviluppo.
- Generazione SQL per lo sviluppo attivabile esplicitamente, con file immutabili, esecuzione isolata e controlli di revisione persistenti.
- Snapshot `SaveAllAsync` controllati dal proprietario per le entità attive gestite dall'applicazione.

## Esempio

Definisci una mappatura stabile tramite attributi in `Player.cs`. Lascia `Id` a zero per l'assegnazione automatica al primo `UpsertAsync`.

<!-- nuget-smoke:Player.cs -->

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[Table(Name = "sample_players.players")]
public sealed class Player : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";
}
```

`Program.cs` risolve la connessione durante l'esecuzione, applica esplicitamente lo schema di esempio, conferma due scritture insieme e
legge un valore scollegato in modo asincrono. Imposta `MOONGATE_PERSISTENCE_DATABASE` su un URI di connessione `postgres://user:password@host:5432/database`
per un database di sviluppo vuoto prima di eseguirlo.

<!-- nuget-smoke:Program.cs -->

```csharp
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

var connectionString = Environment.GetEnvironmentVariable("MOONGATE_PERSISTENCE_DATABASE")
    ?? throw new InvalidOperationException("Set MOONGATE_PERSISTENCE_DATABASE before running this example.");
var options = new PostgreSqlPersistenceOptions(
    [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, connectionString)],
    autoSynchronizeSchema: true);

using var container = new Container();
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<Player>();

await using var persistence = container.Resolve<MoongatePersistenceService>();
await persistence.InitializeAsync();
var mario = new Player { Name = "Mario" };
await persistence.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, async transaction =>
{
    var players = transaction.GetDataAccess<Player>();
    await players.UpsertAsync(mario);
    await players.UpsertAsync(new Player { Name = "Luigi" });
});

var player = await container.Resolve<IDataAccess<Player>>().GetByIdAsync(mario.Id);
Console.WriteLine(player?.Name);
```

`AddPersistenceWorld<T>()` seleziona il database Realm; `AddPersistenceAuth<T>()`
seleziona il database Accounts condiviso. Moongate crea i moduli interni dagli
attributi di tabella qualificati con lo schema, quindi non è richiesta una classe di modulo. Entrambi gli helper
accettano anche una sorgente attiva e una funzione esplicita di snapshot scollegato. Le dichiarazioni
esplicite `IPersistenceModule` rimangono disponibili per i plugin che ne hanno bisogno.

Le opzioni di connessione accettano URI `postgres://` e `postgresql://`, comprese
credenziali con codifica percentuale, host IPv6 e opzioni di query come `sslmode` e
`connect_timeout`. Sono supportate anche le stringhe di connessione native Npgsql.

Le distribuzioni ordinarie mantengono disabilitata la sincronizzazione automatica. Genera e revisiona l'SQL versionato, poi applicalo con il
comando `mgctl migrate apply`. Configura `PostgreSqlPersistenceOptions.MigrationCatalogFactory` per le
verifiche delle migrazioni necessarie in un host personalizzato; `mgserver` (il progetto Moongate.Server) lo collega automaticamente. `SynchronizeSchemaAsync` rimane una
comodità riservata allo sviluppo e non registra la cronologia.

## Comportamento e ambito

Le letture restituiscono entità scollegate. Modificare un'istanza restituita non la salva; chiama `UpsertAsync` o `DeleteAsync`. Nelle scritture
prevale l'ultimo scrittore e non è previsto un token di concorrenza ottimistica. Un callback di transazione copre una sola destinazione Accounts o Realm
e non viene mai ritentato dopo un risultato di commit incerto.

Un `Id` pari a zero su `UpsertAsync` viene assegnato da una sequenza PostgreSQL gestita dalle migrazioni
e riscritto tramite il setter pubblico `Id` dell'entità. Gli ID diversi da zero vengono conservati.
Le sequenze sono per tabella, condivise tra processi e limitate all'intervallo `uint` diverso da zero;
non allocano gli intervalli UO dei mobile/oggetti. Il ruolo di runtime richiede `USAGE` sulla sequenza e `SELECT` affinché l'esportazione
dei dati ne conservi i valori.
Gli inserimenti falliti ripristinano zero; un successivo rollback della transazione conserva un ID assegnato.
Le prenotazioni non vengono mai recuperate. Le nuove entità non richiedono nomi di sequenza nei propri servizi.

`SaveAllAsync` richiede ID diversi da zero già assegnati; salva prima le nuove entità con
`UpsertAsync`. Acquisisce le sorgenti attive registrate e conferma una transazione indipendente per destinazione database. Le funzioni di
snapshot devono copiare in profondità lo stato mutabile annidato. Un'entità assente viene conservata; l'eliminazione è sempre esplicita. Vengono
scritti solo gli snapshot la cui impronta (i primi 128 bit dello SHA-256 del loro JSON) è cambiata dall'ultimo salvataggio confermato;
il primo salvataggio e ogni dodicesimo scrivono tutto.

FreeSql può generare normale DDL additivo dello schema. Usa `OldName` per le rinomine supportate e scrivi SQL esplicito revisionato per
le trasformazioni semantiche dei dati. I downgrade sono gestiti dall'operatore. `IPersistenceDataExporter.ExportDataAsync` scrive i
 dati di un database come uno script `COPY` che psql può ripristinare; pianificare e ruotare i file è compito del server.

Gli oggetti personalizzati e `List<T>` possono usare `[JsonMap, Column(DbType = "jsonb", IsNullable = true)]`
da `FreeSql.DataAnnotations`. Questo pacchetto include e abilita `FreeSql.Extensions.JsonMap`
per entrambe le destinazioni, usando Newtonsoft.Json. Con le impostazioni predefinite, i membri JSON conservano i
nomi C#; il nome della colonna SQL segue comunque snake_case. Le liste vuote vengono salvate e rilette come `[]`,
le proprietà nullable come SQL `NULL`. Gli upsert sostituiscono il valore JSON completo e le letture rimangono
scollegate. Gli snapshot del salvataggio del mondo devono copiare in profondità le collezioni e i loro elementi mutabili.
Vedi il [tutorial JSONB](https://moongate.sh/server/persistence-entity-tutorial/#7-store-custom-values-as-jsonb).

Le altre proprietà complesse richiedono ancora una mappatura supportata di colonna/navigazione oppure
l'omissione esplicita con `IsIgnore`. Un grafo di oggetti privo di annotazioni non viene serializzato né elaborato in cascata
implicitamente. Le modifiche al contenuto JSON non migrano automaticamente i documenti esistenti.

Le mappature sono immutabili, basate solo su attributi e identiche ovunque per un tipo CLR persistente. I moduli selezionano proprietà e
destinazione; non rimappano i tipi. Non riconfigurare autonomamente questi tipi tramite un'altra istanza FreeSql grezza. Il confronto
dello schema FreeSql confronta il database attuale con gli attributi attuali. L'SQL versionato e il registro dei checksum risiedono in
`Moongate.Persistence.Migrations`; il runner separato li applica atomicamente.

`FreeSql.Provider.PostgreSQL` 3.5.311 risolve attualmente Npgsql 5.0.18. Questa limitazione nota del provider non deve essere
nascosta con una sostituzione silenziosa della versione principale di Npgsql. Aggiorna la combinazione provider/driver solo dopo aver eseguito i
test di compatibilità PostgreSQL.

## Approfondimenti

Vedi la [guida alla persistenza e alle operazioni](https://moongate.sh/server/persistence/) per revisione dello schema, proprietà dei plugin,
salvataggi del mondo, transazioni e responsabilità del backup dei database.

## Migrazioni di sviluppo

Il server supporta `persistence.auto_generate_migrations = true` insieme a una
`persistence.migrations_directory` esplicita. Scrive e applica SQL additivo
all'avvio; le modifiche non sicure o non supportate rimangono contrassegnate per la revisione. Questa modalità è disattivata
per impostazione predefinita e non può essere combinata con `auto_sync_schema`. Gli indici semplici sulle colonne,
compresi gli indici univoci e compositi, sono automatici solo sulle tabelle create nella
stessa migrazione. Gli indici su tabelle esistenti e le espressioni
 o opzioni di indice non supportate richiedono revisione.

Le integrazioni autonome della libreria possono fornire `DevelopmentMigrationOptions` con
un'implementazione di `IDevelopmentMigrationRunner` e un resolver esplicito dei componenti.
Mantieni l'esecuzione delle migrazioni isolata dal driver PostgreSQL di FreeSql. Vedi la
[guida alle migrazioni di sviluppo](https://moongate.sh/server/persistence-migrations/#automatic-development-migrations).

## Licenza e sorgenti

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
