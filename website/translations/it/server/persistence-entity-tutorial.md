<!-- translation: {"sourceHash":"a41a8aaf5346b20a6208802eb77ecfcad4a14d83d8e529c1876dba48b59ebdbf","title":"Creare un'entità persistente"} -->

# Creare un'entità persistente

Questo tutorial crea un `CharacterProfile`, lo memorizza in PostgreSQL e lo legge,
aggiorna, interroga ed elimina tramite `IDataAccess<CharacterProfile>`. Moongate
 gestisce internamente i moduli di persistenza.

L'esempio eseguibile usa il checkout corrente del sorgente Moongate e un'applicazione
console standalone. L'ultimo passaggio mostra come registrare la stessa entità in un plugin Moongate.

## 1. Creare il progetto di esempio

Servono l'SDK .NET 10, un checkout Moongate e un database PostgreSQL vuoto.
Il ruolo database dell'esempio deve poter creare schemi, tabelle e sequenze.
Crea prima il database: Moongate crea schema e tabella dell'entità, non il database stesso.

Dalla radice del repository Moongate, crea un progetto affiancato e aggiungi un riferimento
alla libreria di persistenza:

```sh
dotnet new console --framework net10.0 --name EntityTutorial --output ../Moongate.EntityTutorial
dotnet add ../Moongate.EntityTutorial/EntityTutorial.csproj reference src/Moongate.Persistence/Moongate.Persistence.csproj
```

Il riferimento al progetto mantiene il tutorial sulla stessa versione API del checkout.
Fornisce anche gli attributi FreeSql, il container DryIoc e i tipi Moongate Core usati sotto.

## 2. Definire l'entità

Crea `CharacterProfile.cs` nel nuovo progetto:

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace EntityTutorial;

[Table(Name = "tutorial_entities.character_profiles")]
public sealed class CharacterProfile : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";

    [Column(Name = "level")]
    public int Level { get; set; } = 1;
}
```

| Dichiarazione | Scopo |
| --- | --- |
| `IMoongateEntity` | Fornisce alla persistenza un'identità comune `Serial Id`. |
| `Table(Name = "tutorial_entities.character_profiles")` | Seleziona schema e tabella PostgreSQL. Usa nomi espliciti minuscoli. |
| `IsPrimary = true, MapType = typeof(long)` | Memorizza il `Serial` come chiave primaria PostgreSQL `bigint`. |
| `Column(Name = ...)` | Mantiene stabili i nomi delle colonne database quando cambiano i nomi C#. |
| `StringLength = 100` | Dichiara la lunghezza massima del nome memorizzato. |

Lascia `Id` al valore predefinito (`Serial.Zero`) per una nuova entità. `UpsertAsync`
riserva un ID dalla sequenza PostgreSQL della tabella, inserisce l'entità e riscrive
l'ID nello stesso oggetto. Non servono un nome di sequenza o ulteriori dipendenze da
servizi. Mantieni un setter `Id` pubblico e non usare `IsIdentity = true`. Un ID
esplicito diverso da zero viene mantenuto e usa il normale inserimento o aggiornamento.

I nomi delle colonne sono normalmente snake_case minuscolo: `Username` diventa
`username`, `HashPassword` diventa `hash_password` e `CreatedAt` diventa `created_at`.
I mapping espliciti `[Column(Name = "...")]` prevalgono e devono anch'essi usare
snake_case minuscolo. Mantieni espliciti `[Table(Name = "...")]` qualificato con lo
schema e il mapping dell'identità.

L'esempio iniziale usa proprietà scalari. Per una proprietà che deve restare in memoria,
usa `[Column(IsIgnore = true)]`. Valori personalizzati e collezioni possono usare JSONB
con `[JsonMap]`; vedi [Memorizzare valori personalizzati come JSONB](#7-store-custom-values-as-jsonb).
Un grafo di oggetti senza annotazioni non viene serializzato automaticamente.

## 3. Registrare l'entità e usare il suo accesso ai dati

Sostituisci il `Program.cs` generato con questo programma completo:

```csharp
using DryIoc;
using EntityTutorial;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

var connectionString = Environment.GetEnvironmentVariable("MOONGATE_TUTORIAL_DATABASE")
    ?? throw new InvalidOperationException("Set MOONGATE_TUTORIAL_DATABASE before running the tutorial.");

var options = new PostgreSqlPersistenceOptions(
    [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, connectionString)],
    autoSynchronizeSchema: true);

using var container = new Container();
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<CharacterProfile>();

await using var persistence = container.Resolve<MoongatePersistenceService>();
await persistence.InitializeAsync();

var profiles = container.Resolve<IDataAccess<CharacterProfile>>();
var profile = new CharacterProfile
{
    Name = "Mario",
    Level = 1
};

await profiles.UpsertAsync(profile); // profile.Id is now assigned.

var loaded = await profiles.GetByIdAsync(profile.Id)
    ?? throw new InvalidOperationException("The saved profile was not found.");
Console.WriteLine($"Created: {loaded.Name}, level {loaded.Level}");

loaded.Level = 2;
await profiles.UpsertAsync(loaded);

var updated = await profiles.GetByIdAsync(profile.Id)
    ?? throw new InvalidOperationException("The updated profile was not found.");
Console.WriteLine($"Updated: {updated.Name}, level {updated.Level}");

var page = await profiles.QueryAsync(value => value.Level >= 2, skip: 0, take: 20);
Console.WriteLine($"Profiles at level 2 or higher: {page.Count}");

var deleted = await profiles.DeleteAsync(profile.Id);
Console.WriteLine($"Deleted: {deleted}");
Console.WriteLine($"Missing after delete: {await profiles.GetByIdAsync(profile.Id) is null}");
```

`AddPersistenceWorld<CharacterProfile>()` seleziona il database Realm. Moongate
raggruppa le entità registrate per destinazione e schema in moduli interni; non serve
una classe `IPersistenceModule`. Registra tutto prima dell'inizializzazione o
 dell'anteprima dello schema, perché la preparazione dello schema congela le registrazioni.

Questo esempio standalone abilita esplicitamente la sincronizzazione dello schema,
così `InitializeAsync()` crea la tabella del tutorial. Usa questa impostazione per
il database di sviluppo vuoto dell'esercizio. I normali deployment Moongate hanno
la sincronizzazione dello schema disabilitata e usano il flusso revisione/applicazione
 del passaggio 5.

## 4. Eseguire e controllare il risultato

Imposta `MOONGATE_TUTORIAL_DATABASE` tramite ambiente o gestore dei segreti sull'URI
di connessione del database vuoto. La sua forma è:

```text
postgres://USER:PASSWORD@HOST:5432/moongate_tutorial
```

Sostituisci i segnaposto con i dettagli della connessione e codifica percentualmente
i caratteri riservati nelle credenziali, per esempio `@` come `%40`. Tieni le credenziali
effettive fuori dai file sorgente. Vedi [Configurazione delle connessioni](persistence-operations.md#connections)
per le opzioni URI e l'espansione dell'ambiente nel TOML del server.

Dalla radice del repository, esegui:

```sh
dotnet run --project ../Moongate.EntityTutorial/EntityTutorial.csproj
```

Il programma stampa:

```text
Created: Mario, level 1
Updated: Mario, level 2
Profiles at level 2 or higher: 1
Deleted: True
Missing after delete: True
```

La tabella rimane in PostgreSQL; alla fine l'esempio elimina solo la propria riga
 del profilo. Le operazioni illustrano il contratto della persistenza:

- `UpsertAsync` inserisce un ID mancante o aggiorna la riga con quell'ID. Le scritture
  indipendenti eseguono il commit prima che il task restituito si completi.
- Le letture restituiscono valori scollegati. `loaded.Level = 2` modifica solo
  quell'oggetto; il secondo `UpsertAsync` persiste la modifica.
- `QueryAsync` traduce il predicato in SQL. L'overload paginato limita il risultato
  e lo ordina per identità; le espressioni non supportate falliscono esplicitamente.
- `DeleteAsync` rimuove una riga e restituisce se esisteva. Un `GetByIdAsync`
  successivo restituisce `null`.

Usa le [transazioni](persistence.md#reads-writes-and-transactions) quando più scritture
sulla stessa destinazione devono essere confermate insieme. Una transazione non può
attraversare Accounts e Realm.

## 5. Usare l'entità in un plugin Moongate

Sposta l'entità nella directory `Data` del plugin e aggiornane il namespace.
Fai riferimento alla versione corrispondente di `Moongate.Persistence` come descritto
in [Scrivere un plugin](plugins.md#creating-the-project).

Nel metodo `Register(Container container)` del plugin esistente, registra l'entità
usando `Moongate.Persistence.Extensions`:

```csharp
public void Register(Container container)
{
    container.AddPersistenceWorld<CharacterProfile>();
}
```

L'host gestisce già registrazione, inizializzazione e rilascio della persistenza.
Il plugin registra i tipi di entità e lascia che i suoi servizi ricevano
`IDataAccess<CharacterProfile>` tramite injection nel costruttore. Non eseguire
l'inizializzazione o le operazioni database dell'esempio console dentro `Register`.

Scegli l'helper secondo il proprietario dei dati:

| Helper | Database e dati previsti |
| --- | --- |
| `AddPersistenceWorld<TEntity>()` | Database di questo realm: personaggi, oggetti e stato del mondo. |
| `AddPersistenceAuth<TEntity>()` | Database Accounts condiviso: dati degli account e di autenticazione. |

Registra un tipo una volta, in una sola destinazione per container. L'helper sceglie
il database; l'attributo della tabella sceglie lo schema al suo interno. Questi helper
instradano l'accesso al database nel processo corrente; non implementano comunicazione
tra server login e game.

Per l'entità del mondo sopra, imposta la connessione del realm in
`config/moongate.toml` dell'host:

```toml
[persistence]
auto_sync_schema = false

[persistence.realm]
connection_string = "$MOONGATE_REALM_DATABASE"
```

Imposta quella variabile sull'URI PostgreSQL del realm. Un host `game` controlla solo
il proprio database Realm; `standalone` richiede anche `[persistence.accounts]`.
I valori predefiniti sono database locali `auth` e `world` con `moongate` / `moongate`.
I comandi di anteprima/generazione dello schema si connettono solo alle destinazioni
necessarie per i mapping. Nel deployment, mantieni la sincronizzazione automatica
disabilitata e distribuisci una migrazione SQL versionata con il plugin.

1. Distribuisci il plugin in una radice di riferimento il cui database abbia lo schema precedente.
2. Genera una bozza per World, scegliendo il numero successivo della sequenza del componente:

   ```sh
   mgserver --root-directory /srv/moongate/reference \
     --persistence-schema generate --migration-target world \
     --migration-output ./MyPlugin/migrations/world/0001_create_characters.sql
   ```

3. Revisiona l'SQL e aggiungi `MyPlugin/migrations/manifest.json`:

   ```json
   { "id": "my-plugin" }
   ```

4. Includi `migrations/**/*` nell'output pubblicato del plugin e committalo con
   l'entità. Arresta il realm di destinazione e applica usando una connessione con ruolo schema:

   ```sh
   ./mgctl migrate apply \
     --root-directory /srv/moongate/realm-1 --target world
   ```

5. Avvia il server con la connessione runtime. Verifica sia la cronologia delle
   migrazioni sia lo schema mappato prima di avviare i servizi.

**Dove sono i file?** L'SQL core è in `migrations/auth` e `migrations/world` accanto
al server. L'SQL dei plugin è nella directory `migrations/` di ogni bundle. Il runner
registra file riusciti e checksum in `moongate_migrations.history`. Non modificare
mai un file applicato: aggiungi una nuova migrazione numerata. Genera sullo schema
precedente, non su un database già aggiornato. La bozza include tutti i moduli
registrati per la destinazione, quindi usa una radice di riferimento isolata per il
plugin e verifica la proprietà.

Il precedente esempio console usa intenzionalmente la sincronizzazione automatica
per un database temporaneo. Questa comodità non crea file SQL o cronologia delle
versioni. Usa la [guida alle operazioni sullo schema](persistence-migrations.md#generate-review-and-apply)
per deployment di tipo produttivo, database di riferimento, ruoli e gestione degli errori.

La semplice registrazione dell'entità abilita letture e scritture esplicite. Per
oggetti vivi mantenuti dal game loop, l'overload con sorgente e snapshot di
`AddPersistenceWorld` li include anche in `SaveAllAsync` e nei salvataggi del mondo
 dell'host. Segui [Snapshot del mondo vivo](persistence.md#live-world-snapshots) per
registrare un clone scollegato e catturarlo tramite il loop proprietario. Rimuovere
un oggetto dalla memoria non elimina la sua riga; l'eliminazione rimane esplicita.

## 6. Generare migrazioni automaticamente durante lo sviluppo

Quando l'entità è registrata con `AddPersistenceAuth<TEntity>()` o
`AddPersistenceWorld<TEntity>()`, abilita il flusso di sviluppo:

```toml
[persistence]
auto_sync_schema = false
auto_generate_migrations = true
migrations_directory = "${MOONGATE_ROOT}/migrations"
```

Imposta `MOONGATE_ROOT` sulla radice dei dati del server, o usa una directory sorgente
assoluta. Le migrazioni auth core distribuite (da `0001` a `0004`) devono già essere
applicate, perciò i file generati sotto partono da `0005`. Per una nuova entità
personalizzata registrata con `AddPersistenceAuth<CustomAuthEntity>()`:

1. Avvia il server con la nuova entità registrata. L'avvio scrive
   `migrations/auth/0005_auto_schema.sql`, lo applica e ne registra il checksum.
2. Arresta il server e aggiungi `public DateTime? LastLoginAt { get; set; }` all'entità.
3. Riavvia. L'avvio scrive e applica `0006_auto_schema.sql`; le righe esistenti
   ricevono un valore `last_login_at` null.
4. Riavvia senza modificare l'entità: non viene generata una nuova migrazione.
5. Committa entrambi i file SQL generati e il codice dell'entità. I numeri proseguono
   sempre dopo la migrazione più alta esistente nel componente.

Gli indici dichiarati con `[Index(...)]` sono automatici per una tabella appena
creata quando usano colonne semplici, compresi indici univoci e compositi. Aggiungere
un indice a una tabella esistente richiede revisione: i dati esistenti potrebbero
violare un indice univoco.

Una modifica che richiede revisione lascia una bozza SQL contrassegnata e interrompe
l'avvio. Revisiona il file non applicato e rimuovi `-- moongate:review-required` prima
di riavviare. I file esistenti applicati sono immutabili. Lascia disabilitati i flag
di sviluppo nel deployment e usa il runner standalone delle migrazioni per i file revisionati.

Vedi [Migrazioni automatiche di sviluppo](persistence-migrations.md#automatic-development-migrations)
per directory dei plugin, valori predefiniti delle colonne obbligatorie, baseline dei
database esistenti e recupero dagli errori.

## 7. Memorizzare valori personalizzati come JSONB

Usa una colonna JSONB per piccoli valori posseduti dall'entità, come progressi delle
quest o preferenze. Usa entità/tabelle separate per oggetti con identità e ciclo di
vita propri, come oggetti dell'inventario con un Serial.

Moongate include `FreeSql.Extensions.JsonMap` e lo abilita prima del mapping di ogni
database Accounts o Realm. Aggiungi questa proprietà a `CharacterProfile`:

```csharp
[JsonMap, Column(Name = "quest_progress", DbType = "jsonb", IsNullable = true)]
public List<QuestProgress>? QuestProgress { get; set; } = [];
```

`JsonMap` e `Column` usano `FreeSql.DataAnnotations`. Crea `QuestProgress.cs` nel
progetto del tutorial (usa il namespace `Data` del plugin quando lo sposti lì):

```csharp
namespace EntityTutorial;

public sealed class QuestProgress
{
    public int QuestId { get; set; }
    public bool Completed { get; set; }
    public List<int> Milestones { get; set; } = [];
}
```

`QuestProgress` è un valore nel profilo: non richiede `IMoongateEntity`, un ID o una
propria registrazione di persistenza. Gli stessi attributi supportano una proprietà
oggetto personalizzata al posto di una lista.

Dopo aver applicato la migrazione della colonna, salva tramite l'accesso ai dati esistente:

```csharp
var profile = new CharacterProfile
{
    Name = "Mario",
    QuestProgress =
    [
        new() { QuestId = 10, Completed = false, Milestones = [1, 2] },
        new() { QuestId = 25, Completed = true }
    ]
};
await profiles.UpsertAsync(profile);
```

La colonna `quest_progress` contiene un array JSON, non righe figlie separate:

```json
[
  { "QuestId": 10, "Completed": false, "Milestones": [1, 2] },
  { "QuestId": 25, "Completed": true, "Milestones": [] }
]
```

L'estensione usa Newtonsoft.Json. Con le impostazioni predefinite del serializzatore,
i nomi dei membri JSON mantengono le maiuscole/minuscole C#; la convenzione snake_case
SQL si applica al nome della colonna, non ai membri nel documento. Mantieni stabili
i nomi JSON memorizzati; cambiare il nome di un membro DTO non genera una migrazione
di colonna SQL né trasforma automaticamente i documenti esistenti. Gli attributi
System.Text.Json non configurano questo serializzatore.

Una lista vuota viene memorizzata come `[]`. Con `IsNullable = true`, una proprietà
null è SQL `NULL` e viene riletta come null. `= []` inizializza le nuove istanze C#;
non garantisce che i null del database vengano convertiti in liste vuote.

Le letture ricostruiscono valori scollegati. Modificare una lista o un elemento non
scrive in PostgreSQL finché non chiami `UpsertAsync`, o un salvataggio del mondo
registrato non lo cattura. Un normale upsert sostituisce l'intero valore JSON
memorizzato; non esiste un'unione automatica per elemento. Gli aggiornamenti concorrenti
seguono il normale comportamento dell'ultima scrittura che prevale.

Per i salvataggi del mondo vivo, il callback dello snapshot deve copiare sia la lista
sia ogni valore mutabile al suo interno, comprese liste annidate come `Milestones`.
Copiare solo l'entità o usare `new List<QuestProgress>(live.QuestProgress)` continua
a condividere gli oggetti delle quest con il mondo vivo.

Il normale flusso delle migrazioni si applica anche alle colonne JSONB. Quando la
generazione delle migrazioni di sviluppo aggiunge una colonna JSONB nullable senza
un valore predefinito SQL dichiarato, le righe esistenti rimangono SQL `NULL`, anche
se la proprietà C# parte con `= []`. Moongate rimuove in questo caso il riempimento
implicito di FreeSql con oggetto/array vuoto, quindi non attiva trigger di aggiornamento
sulle righe esistenti. Revisiona l'SQL generato tramite altri flussi dello schema prima di applicarlo.

Aggiungere o cambiare una colonna è una modifica dello schema; cambiare solo la forma
del payload JSON richiede una strategia esplicita di compatibilità o una migrazione
dati revisionata. La memorizzazione JSONB non implica che ogni operazione LINQ su
collezioni annidate venga tradotta in SQL.
