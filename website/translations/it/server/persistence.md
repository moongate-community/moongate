<!-- translation: {"sourceHash":"a28717d126226b5066ed19f06b09a8eed010dc708a8164f76dcae6340a77a15d","title":"Entità e accesso ai dati"} -->

# Persistenza PostgreSQL: entità e accesso ai dati

Moongate memorizza le entità registrate in PostgreSQL tramite FreeSql. Un deployment
usa un database Accounts gestito dal login e un database Realm per ogni mondo di gioco.
Standalone usa entrambi. Transazioni e salvataggi del mondo non attraversano mai questi database.

Questa pagina è rivolta allo sviluppatore che registra un'entità e la legge o scrive.
Le altre due pagine sulla persistenza sono
[Migrazioni: generare, revisionare e applicare](persistence-migrations.md), per modificare
lo schema, e [Gestire PostgreSQL](persistence-operations.md), per stringhe di connessione,
ruoli, salvataggi del mondo e backup. Per un primo esempio completo, segui
[Creare un'entità persistente](persistence-entity-tutorial.md).

## Due database, quattro nomi ciascuno

Gli stessi due database compaiono con nomi diversi secondo il contesto in cui li
incontri. Sono la stessa cosa:

| Database | Scopo | Sezione TOML | Destinazione della migrazione | Helper di registrazione | Destinazione C# |
| --- | --- | --- | --- | --- | --- |
| Accounts | Dati condivisi di login e account | `[persistence.accounts]` | `--target auth`, `migrations/auth/` | `AddPersistenceAuth<T>()` | `PersistenceDatabaseTarget.Accounts` |
| Realm | Dati di questo mondo | `[persistence.realm]` | `--target world`, `migrations/world/` | `AddPersistenceWorld<T>()` | `PersistenceDatabaseTarget.Realm` |

I valori predefiniti generati chiamano i database `auth` e `world`.

## Registrare le entità

Ogni tipo persistito implementa `IMoongateEntity` e ha un `Serial` stabile e diverso da zero.
Seleziona il database quando lo registri:

```csharp
container.AddPersistenceAuth<Account>();     // Shared Accounts database.
container.AddPersistenceWorld<Character>();  // This realm's database.
```

Registra un'entità solo in un processo che gestisce la sua destinazione: le entità
Auth in `login` o `standalone`, quelle World in `game` o `standalone`. Registrare
un'entità per una destinazione inattiva fallisce all'avvio. Un processo login controlla
solo la connessione Accounts; uno game controlla solo il proprio database Realm.

Moongate crea automaticamente moduli interni, raggruppando le entità per database
e schema PostgreSQL. Non serve una classe modulo separata. Dichiara negli attributi
dell'entità un nome di tabella stabile e qualificato con lo schema:

```csharp
using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

[Table(Name = "inventory.items")]
public sealed class Item : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 100)]
    public string Name { get; set; } = "";
}
```

Regole di mapping:

- Mappa le chiavi `Serial` a `bigint` PostgreSQL con `MapType = typeof(long)`. Non
  contrassegnare la chiave `IsIdentity`; la sequenza viene collegata da una migrazione.
- I nomi delle colonne sono normalmente snake_case minuscolo: `Username` diventa
  `username`, `HashPassword` diventa `hash_password`, `CreatedAt` diventa `created_at`.
  I mapping espliciti `[Column(Name = "...")]` prevalgono e devono anch'essi essere snake_case minuscolo.
- Le normali proprietà scalari pubbliche sono mappate da FreeSql. Ogni proprietà
  complessa richiede un mapping esplicito supportato, un mapping di navigazione o
  `IsIgnore`; un grafo di oggetti senza annotazioni non viene serializzato né salvato a cascata.
- Le colonne `DateTime` sono `timestamp` senza fuso orario. Moongate converte in UTC
  i valori locali prima della scrittura e legge ogni valore come `DateTimeKind.Utc`.
- Le proprietà `Hue` vengono mappate automaticamente a una colonna `integer`.
- Il mapping appartiene al tipo CLR e deve essere identico in ogni proprietario.
  Un modulo seleziona proprietà e destinazione database; non può rimappare il tipo,
  e nessun codice può riconfigurare un tipo persistente tramite un'altra istanza FreeSql grezza.

Registra prima della preparazione dello schema:

```csharp
container.RegisterMoongatePersistence(options)
         .AddPersistenceWorld<Item>();
```

La registrazione non esegue I/O sul database. L'inizializzazione valida l'intero
insieme, comprese registrazioni duplicate, mapping di tabelle e proprietà degli schemi.
Ogni tipo ha esattamente un proprietario, e un tipo CLR non può essere registrato
in entrambi i database nello stesso container.

Il server pubblica `PersistenceReadyEvent` dopo l'inizializzazione e prima dell'avvio
dei servizi, e `PersistenceStoppedEvent` dopo il rilascio del proprietario inizializzato,
prima del rilascio del container. Iscriviti durante la registrazione del plugin con
`container.OnEvent<TEvent>(...)`; vedi
[eventi del ciclo di vita della persistenza](plugins.md#persistence-lifecycle-events).
La libreria di persistenza standalone non pubblica questi eventi del server.

### Moduli espliciti facoltativi dei plugin

Un plugin può implementare `IPersistenceModule` e registrarlo tramite
`AddPersistenceModule<TModule>()` quando richiede un identificatore stabile del modulo
e un elenco esplicito dei tipi di entità posseduti. Dichiara `Id`, `Schema`,
`DatabaseTarget` ed `EntityTypes`. Registra le entità con l'helper Auth/World
corrispondente, la cui destinazione deve coincidere con quella del modulo, oppure
con `AddPersistenceEntity<T>()`, dove il modulo fornisce la destinazione. L'ordine di
registrazione non conta. Un modulo esplicito possiede l'intero schema: includi nella
sua dichiarazione ogni entità registrata in quello schema.

## Valori personalizzati e collezioni come JSONB

Annota un valore personalizzato posseduto o `List<T>` con
`[JsonMap, Column(DbType = "jsonb", IsNullable = true)]` da
`FreeSql.DataAnnotations`. Moongate abilita `FreeSql.Extensions.JsonMap` per entrambe
le destinazioni database; non serve una registrazione aggiuntiva per il tipo del
valore annidato. L'estensione usa Newtonsoft.Json e, con le impostazioni predefinite,
conserva le maiuscole/minuscole dei membri JSON indipendentemente dai nomi snake_case
 delle colonne SQL.

Le collezioni vuote vengono scritte e rilette come `[]`; le proprietà nullable come
SQL `NULL`. Le letture sono scollegate e un upsert esplicito sostituisce l'intero
valore JSON. Il mapping JSON non aggiunge salvataggi a cascata, tracciamento automatico
delle modifiche o aggiornamenti per elemento. I callback degli snapshot devono copiare
in profondità i valori mutabili annidati. Usa entità separate per valori con identità
e ciclo di vita propri.

Segui [il tutorial JSONB](persistence-entity-tutorial.md#7-store-custom-values-as-jsonb)
per un esempio completo di collezione, migrazioni e limiti del serializzatore.

## Letture, scritture e transazioni

Risolvi `IDataAccess<T>` per operazioni indipendenti:

```csharp
var items = container.Resolve<IDataAccess<Item>>();
var item = new Item { Name = "Bandage" };
await items.UpsertAsync(item); // item.Id is assigned automatically.

var loaded = await items.GetByIdAsync(item.Id);
var page = await items.QueryAsync(value => value.Name.StartsWith("B"), 0, 50);
await items.DeleteAsync(item.Id);
```

`GetByIdAsync`, `GetAllAsync` e `QueryAsync` restituiscono valori scollegati. Le query
vengono tradotte in SQL; un'espressione non supportata fallisce anziché passare al
filtraggio client. `GetAllAsync` è intenzionalmente illimitato e pensato per avvio
o amministrazione. Modificare un oggetto restituito non lo salva.

Gli upsert seguono la semantica dell'ultima scrittura che prevale. Non esiste un token
di concorrenza ottimistica né un nuovo tentativo automatico dopo un risultato di commit
incerto. Raggruppa scritture correlate su un database con `ExecuteInTransactionAsync`:

```csharp
await persistence.ExecuteInTransactionAsync(
    PersistenceDatabaseTarget.Realm,
    async transaction =>
    {
        var items = transaction.GetDataAccess<Item>();
        await items.UpsertAsync(first);
        await items.UpsertAsync(second);
    });
```

Il callback deve completarsi in modo asincrono e non deve far uscire la propria facade
della transazione. Una transazione non può attraversare Accounts e Realm. Se un flusso
modifica entrambi, progetta una compensazione o riconciliazione esplicita.

## Assegnazione automatica del Serial

Lascia `Id` a zero su una nuova entità e `UpsertAsync` lo assegna sulla stessa istanza:

```csharp
var account = new AccountEntity { Username = "Mario", HashPassword = passwordHash };
await accounts.UpsertAsync(account);
Console.WriteLine(account.Id); // Assigned on this same instance.
```

- `Id == Serial.Zero` riserva un ID univoco diverso da zero ed esegue un inserimento,
  mai un aggiornamento di una riga esistente. Gli ID già memorizzati esplicitamente vengono saltati.
- Un `Id` diverso da zero viene mantenuto e usa il comportamento esistente di inserimento o aggiornamento.
- L'assegnazione automatica richiede un setter `Id` pubblico. `IMoongateEntity` espone solo un getter.
- Ogni tabella ha la propria sequenza nel proprio schema. Le sequenze usano l'intervallo
  `1..4294967295`, non riciclano mai e sono condivise tra i processi che usano quel database.
  Un'entità i cui serial vivono in un intervallo che non parte da 1 lo dichiara con
  `[SerialRange(min, max)]` (`Moongate.Core.Attributes`): una sequenza generata parte
  quindi da `min`, così una tabella vuota riserva prima `min`, e una sequenza lasciata
  sotto `min` da un vecchio schema generato viene avanzata fino a quel valore.
  `ItemEntity` dichiara `Serial.MinItem..Serial.MaxItem`. L'attributo non limita il
  massimo dell'intervallo; una migrazione revisionata può aggiungere un vincolo di
  controllo, come fa `world.items`. Fornisci esplicitamente i serial di gioco quando
  le regole del dominio richiedono un'identità condivisa.
- La creazione delle sequenze appartiene alle migrazioni dello schema, mai a `UpsertAsync`.
  Il ruolo runtime richiede `USAGE` e `SELECT` sulle sequenze oltre ai permessi delle
  tabelle; `SELECT` consente a un [backup SQL](persistence-operations.md#database-backups)
  di leggerne i valori.

Un inserimento fallito ripristina l'ID dell'entità a zero. Dopo un inserimento riuscito
in una transazione, un rollback successivo lascia l'ID assegnato sull'oggetto;
riprovare con quell'ID è un upsert esplicito. Le prenotazioni non vengono annullate né
riciclate, quindi sono previsti vuoti. Gli errori di conferma del commit hanno un
esito durevole sconosciuto e non vengono ritentati. Non condividere o modificare
un'entità mentre la sua operazione di persistenza è in corso.

`SaveAllAsync` richiede ID stabili e diversi da zero sulle entità vive catturate:
persisti una nuova entità con `UpsertAsync` prima di aggiungerla a una collezione viva
di salvataggio. L'API avanzata
`MoongatePersistenceService.ReserveSerialAsync<TEntity>("schema.sequence", cancellationToken)`
rimane disponibile per sequenze gestite esplicitamente dalle migrazioni; le entità
ordinarie non ne hanno bisogno. Non crea sequenze né alloca intervalli di serial di gioco.

## Snapshot del mondo vivo

Per lo stato posseduto dal game loop, registra una sorgente e un clone scollegato:

```csharp
container.AddPersistenceWorld<Item>(
    () => world.Items.Values,
    item => new Item { Id = item.Id, Name = item.Name });
```

La funzione di clonazione deve copiare ogni valore mutabile annidato; restituire
l'istanza viva viene rifiutato. `SaveAllAsync` cattura le sorgenti tramite il callback
del proprietario fornito e poi scrive una transazione per database attivo. Esegue
upsert delle entità catturate; l'assenza da uno snapshot non implica eliminazione.
Esegui un `DeleteAsync` esplicito per le righe rimosse, o registra una sorgente di eliminazioni:

```csharp
container.AddPersistenceWorld<Item>(() => world.Items.Values, item => item.Snapshot(), world);
```

Un salvataggio scrive solo le entità catturate il cui snapshot è cambiato dall'ultimo
salvataggio confermato. Ogni snapshot riceve un'impronta, i primi 128 bit dello SHA-256
del suo JSON, calcolata fuori dal loop; vengono aggiornate o inserite le entità con
impronta diversa o non catturate dall'ultimo salvataggio confermato, le altre vengono
saltate. Le impronte vengono mantenute solo dopo il commit della transazione, quindi
dopo un salvataggio fallito il successivo riscrive quelle entità. Un'entità che lascia
la sorgente, come un personaggio che esce, perde l'impronta e viene scritta interamente
quando ritorna. Il primo salvataggio dopo un avvio scrive tutto, come ogni dodicesimo
successivo (una volta all'ora con l'intervallo predefinito di cinque minuti): una riga
modificata fuori dal salvataggio del mondo, per esempio da un personaggio che esce
mentre è in corso un salvataggio, viene allora corretta. L'impronta comprende ogni
proprietà con setter, anche privato, e i valori delle struct contenute, come
`Serial.Value`; sono escluse le proprietà con solo getter, calcolate dalle altre.
NaN e gli infiniti producono un'impronta come ogni numero. Il log indica quante entità
sono state catturate e quante scritte.

`IPersistenceDeletionSource.Capture()` viene eseguito sul loop con lo snapshot e
restituisce le identità rimosse dalla sorgente; il salvataggio le elimina nella stessa
transazione degli upsert, poi chiama `Committed()` con esattamente quelle identità.
Un salvataggio fallito non lo chiama, quindi restano in sospeso.
`IDataAccess<T>.ReserveSerialAsync()` prende l'identità successiva dalla sequenza
 dell'entità senza scrivere una riga, per un'entità creata in memoria e salvata più tardi.

La cattura e qualsiasi aggiornamento dello stato del proprietario dopo il commit devono
avvenire e completarsi tramite il game loop, che possiede quello stato. Nell'host,
usa `IPersistenceOperationBarrier` attorno a un'operazione critica e attendi sia il
lavoro sul database sia la sua applicazione nel game loop. Accodare un elemento di
lavoro attende solo l'ammissione; attendi anche il completamento dell'elemento stesso.
Vedi [Game loop e timer](game-loop-and-timers.md) per ammissione e completamento.

Se un callback critico ammesso fallisce o viene annullato, la barriera mantiene la
causa originale e blocca le operazioni critiche accodate e nuove, oltre agli snapshot
successivi e al salvataggio finale. Questo impedisce alla memoria obsoleta di sovrascrivere
un commit database il cui esito potrebbe essere incerto. L'arresto continua a drenare
e fermare, ma il mondo richiede un nuovo host prima di ulteriore persistenza. La regola
è conservativa anche quando la specifica transazione database è stata annullata.
L'annullamento prima dell'ammissione e un errore del solo salvataggio non compromettono la barriera.

La frequenza dei salvataggi e il comportamento del salvataggio finale all'arresto sono
descritti in [Gestire PostgreSQL](persistence-operations.md#world-saves).

## Oggetti del mondo

`ItemEntity` (`world.items`) e `MobileEntity` (`world.mobiles`) sono registrati dal
plugin Ultima per il database Realm nelle modalità game e standalone. Un oggetto si
trova esattamente in un posto, e il database lo controlla:

| Posizione | Colonne | Impostata con |
| --- | --- | --- |
| A terra | `map`, `x`, `y`, `z` | `PlaceOnGround(map, location)` |
| In un oggetto contenitore | `container_id`, `grid_x`, `grid_y`, `grid_index` | `PutInContainer(containerId, gridLocation, gridIndex)`; riletti come `GridLocation` e `GridIndex`, lo slot (da 0 a 124) nella griglia di Enhanced Client |
| Indossato da un mobile | `mobile_id`, `layer` | `Equip(mobileId, layer)` |

Gli oggetti a terra vivono in `IItemService` e nella griglia dei settori mentre il
server è attivo: `IItemService` li carica, insieme a tutto il loro contenuto, all'avvio
(la migrazione `0010` li indicizza per mappa), e il salvataggio del mondo li scrive con
gli oggetti dei personaggi. Anche gli NPC (mobile senza account) vivono in
`IMobileService`: `INpcService` li carica all'avvio con ciò che indossano e trasportano,
e li genera e rimuove. Un mobile rimosso viene eliminato dal successivo salvataggio
del mondo nella propria transazione (`IMobileService` è la sorgente di eliminazioni
dei mobile), e le righe dei suoi oggetti lo seguono tramite le chiavi a cascata.
I suoi oggetti vivi devono prima lasciare `IItemService`: il salvataggio scrive i mobile
prima degli oggetti, e un oggetto vivo di un mobile eliminato farebbe fallire ogni
salvataggio successivo.

Ciò che un personaggio lascia a terra, o una pila a terra che incrementa, viene salvato
anche alla sua uscita, nella stessa transazione dei suoi oggetti e delle eliminazioni
delle pile unite: altrimenti le righe continuerebbero a dire che il personaggio li
trasporta fino al successivo salvataggio del mondo.

Ogni helper azzera gli altri due gruppi, quindi sposta un oggetto solo tramite essi.
Il database rifiuta anche un oggetto dentro sé stesso e due oggetti sullo stesso layer
di un mobile. Eliminare un contenitore o un mobile elimina ricorsivamente ciò che
contiene: uno zaino e tutto il suo contenuto scompaiono con il mobile che lo indossa.

Un nuovo oggetto con `Id = Serial.Zero` riceve l'ID da `world.items_id_seq`, che parte
da `Serial.MinItem`; un CHECK lo limita a `Serial.MaxItem`, e i mobile sono vincolati
allo stesso modo a `Serial.MinMobile..MaxMobile`. Viene memorizzato solo ciò che differisce
dal template dell'oggetto: `name`, `movable` o `visibility` null significano il valore
del template. I valori presenti solo su alcuni oggetti, come cariche, durabilità e tag
degli script, vanno nella colonna JSONB `props`, un dizionario chiave-valore letto e
scritto tramite l'oggetto:

```csharp
item.SetProp(ItemPropKeys.Quality, ItemQualityType.Exceptional); // null removes the key
var charges = item.GetProp<int>(ItemPropKeys.Charges);   // default(T) when missing
var quality = item.GetProp(ItemPropKeys.Quality, ItemQualityType.Regular);
if (item.TryGetProp<LootType>(ItemPropKeys.LootType, out var lootType)) { /* ... */ }
item.RemoveProp(ItemPropKeys.Charges);
```

`ItemPropKeys` definisce i nomi delle chiavi lette dal server (`loot_type`, `charges`,
`durability`, `max_durability`, `quality`, `crafter_id`, `crafter_name`, `uses_remaining`); gli script possono usare
qualsiasi altra chiave. `quality` contiene un `ItemQualityType` (`Low`, `Regular`,
`Exceptional`, come ModernUO); un oggetto senza questa proprietà è `Regular`.
Una proprietà contiene una stringa, un numero, un bool o un enum; qualsiasi altro
valore viene rifiutato all'impostazione. La colonna restituisce gli interi come `long`
e gli enum come numero, quindi `GetProp<T>` converte al tipo richiesto e genera
`InvalidCastException` indicando la chiave se il valore memorizzato non è convertibile.
Un oggetto senza proprietà memorizza `NULL`.

Un mobile mantiene i valori estratti dal template alla creazione: `template_id`
(null per un personaggio giocante), `hits`/`hits_max`, `mana`/`mana_max`,
`stamina`/`stamina_max`, `fame`, `karma`, `armor` e le cinque colonne `resist_*`, oltre
a nome, corpo, aspetto, statistiche e abilità. `title` e `notoriety` sono memorizzati
solo quando differiscono dal template (null significa il valore del template); un
CHECK mantiene `notoriety` tra 1 e 7. `MobileEntity` ha lo stesso dizionario `props`
e gli helper `SetProp`/`GetProp`/`TryGetProp`/`RemoveProp` degli oggetti, con le stesse regole.

## Account

Il plugin Ultima incorporato registra `AccountEntity` nel database Accounts e
`IAccountService` nel container. `CreateAccountAsync` usa `IDataAccess<AccountEntity>`
e lascia che `UpsertAsync` assegni l'`Id` del nuovo account. `ListAccountsAsync`
incapsula `GetAllAsync`: uno snapshot illimitato e scollegato di ogni account,
per amministrazione anziché ricerche a ogni richiesta. `LoginAsync` verifica l'hash
della password e lo stato di blocco. Un login riuscito restituisce un elenco di realm
`0xA8` filtrato; la selezione emette un ticket di handoff Redis monouso che game verifica
senza accesso diretto al database Accounts.

Il plugin Ultima incorporato registra anche un comando per creare account:

```text
account create <username> <password> [Regular|GameMaster|Administrator]
```

Il livello predefinito è `Regular`. La console interattiva maschera la password durante
la digitazione, e l'output del comando non la ripete mai. Il comando è registrato anche
per gli amministratori in gioco, ma l'input dei comandi in gioco non è ancora collegato;
quell'input deve proteggere la password prima di esporre il comando. Nome utente e
password devono essere ciascuno un token perché il sistema dei comandi separa gli
argomenti sugli spazi.

L'indice del nome utente distingue maiuscole e minuscole, come la ricerca del servizio.
I tentativi concorrenti di registrare lo stesso nome utente restituiscono un successo
e `UsernameAlreadyExists` per gli altri. L'email è facoltativa perché non è richiesta
per la creazione. Gli account bloccati non possono accedere; le richieste annullate
propagano `OperationCanceledException`. L'SQL auth core che crea questa tabella è
descritto nel [catalogo auth core](persistence-migrations.md#the-core-auth-catalog).

## Test di carico

Lo [scenario di stress della persistenza](persistence-stress.md) esegue sessioni virtuali
tramite `DataAccess<T>` su un container PostgreSQL isolato, riporta latenza e throughput,
e verifica i dati confermati dopo la riapertura della persistenza. È facoltativo e
non richiede implementazione Ultima Online o file del client.
