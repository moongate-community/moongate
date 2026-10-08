<!-- translation: {"sourceHash":"b53eceff8591c00864633718777c79083b1901aa95772e96ded7fcf8ab8ee761","title":"Caricamento dei template TOML"} -->

# Caricamento dei template TOML

I contenuti dello shard scritti a mano da un autore, come definizioni di oggetti e mobile,
sono un insieme di file TOML sotto `templates/` nella directory radice del server, letti una volta quando lo
shard si avvia. I template della distribuzione vengono copiati lì da
[`mgctl`](mgctl.md). Questa pagina descrive il contratto del caricatore in `Moongate.Server.Ultima` e
i tipi di valore TOML in `Moongate.Core` che rendono comoda la scrittura a
mano dei template; [Tipi di valore TOML](toml-types.md) è il riferimento per le loro forme testuali.
Presuppone [scrivere un plugin](plugins.md), perché un caricatore viene registrato da `Register`
allo stesso modo di un servizio o un provider di metriche.

## Cosa esiste oggi

Il contratto del caricatore, `DataLoaderService`, `EnumValueSpec<TEnum>`, `RangeValueSpec<T>`
e il registro dei convertitori sono presenti e testati. Esistono le strutture dati `ItemTemplate`,
`MobileTemplate` e `LootTemplate`, e [un convertitore](uox3-migration.md) le produce
dai dati UOX3. **I template di oggetto e mobile vengono caricati** (vedi
[Template di oggetto a runtime](#item-templates-at-runtime) e
[Template di mobile a runtime](#mobile-templates-at-runtime)), così come le tabelle del bottino (vedi
[Tabelle del bottino a runtime](#loot-tables-at-runtime)).
Lo stesso contratto carica già i file sotto `data/`, come mappe, razze e
regioni: vedi [File di dati dello shard](data-files.md) per caricatori funzionanti.
Vedi [Stato dell'implementazione](implementation-status.md).

## Template di oggetto a runtime

`ItemTemplatesLoader` legge ogni `*.toml` sotto `templates/items/`, comprese le
sottocartelle, quando il server di gioco si avvia e risolve `base_id` una volta sola:

- un campo che può rimanere non impostato (`name`, `layer`, `weight`, `amount`, `stackable`, …)
  e non è impostato prende il valore del genitore, risalendo la catena;
- `item_id = 0` prende la grafica del genitore;
- `hue` viene ereditato quando non impostato; senza un valore in tutta la catena, usa 0;
- `rarity` ha sempre un valore, quindi viene mantenuto quello del template;
- `tags`, quando impostato, sostituisce quello del genitore; non vengono uniti.

Il server si arresta quando un id è vuoto o usato due volte (il messaggio indica entrambi i file),
un `base_id` non indica alcun template, `base_id` crea un ciclo (`a` → `b` → `a`), oppure un template
risolto fallisce `ItemTemplate.Validate()`.

`IItemTemplateService` fornisce i template risolti (`TryGet`, `Get`, `Count`; gli id
distinguono maiuscole e minuscole). `IItemFactoryService` crea oggetti da essi:

```csharp
var coin = factory.Create("0x0eed_gold_coin", amount: 250);
coin.PutInContainer(backpack.Id, layout.RandomGridPosition(backpack.ItemId)); // or PlaceOnGround / Equip
await factory.SaveAsync(coin);          // the database gives it its serial
```

`Create` costruisce l'`ItemEntity` in memoria, senza seriale né posizione. I valori casuali del
template (`hue`, `amount`, `rarity`) vengono scelti una volta, lì, e memorizzati con
l'oggetto; nient'altro viene copiato dal template, quindi `name`, `movable` e
`visibility` rimangono null e si applicano i valori del template. Una quantità superiore a 1 su un oggetto
non impilabile (`stackable`, altrimenti il flag tiledata `Generic`) genera un'eccezione. Il salvataggio
rifiuta un oggetto senza posizione e assegna a un nuovo oggetto un seriale da
`world.items_id_seq`, dentro `Serial.MinItem..MaxItem`. `SaveAsync(items)` salva un
elenco in una transazione, in ordine (un contenitore prima del suo contenuto), e
`SaveAsync(transaction, item)` salva dentro una transazione aperta dal chiamante.

## Template di mobile a runtime

`MobileTemplatesLoader` legge ogni `*.toml` sotto `templates/mobiles/`, comprese le
sottocartelle, dopo i template di oggetto e risolve `base_id` una volta sola, come fa `GET` di UOX3
(il figlio eredita tutto e sostituisce ciò che imposta):

- un campo non impostato prende il valore del genitore, risalendo la catena;
- `skills`, `resistances` e `sounds` vengono ereditati chiave per chiave: le voci del figlio
  sostituiscono quelle corrispondenti, le altre del genitore rimangono;
- `tags` vengono uniti: le chiavi del figlio aggiungono e sostituiscono quelle del genitore;
- `equipment` e `loot`, quando impostati, sostituiscono quelli del genitore;
- tutto ciò che viene ereditato viene copiato, mai condiviso tra template.

Il server si arresta quando un id è vuoto o usato due volte, un `base_id` non indica alcun template o
crea un ciclo, un template fallisce `MobileTemplate.Validate()`, un `name_list` non indica alcuna lista di
`data/names.toml` (`{gender}` richiede le liste `male` e `female`), un oggetto di equipaggiamento non è
un template di oggetto oppure un id `loot` non è una tabella del bottino. `IMobileTemplateService` fornisce i template risolti (`TryGet`, `Get`,
`Count`).

`IMobileFactoryService` crea NPC da essi:

```csharp
var spawned = await mobiles.SpawnAsync("guard", MapType.Felucca, new Point3D(1602, 1591, 20));
// spawned.Mobile is saved with its serial; spawned.Equipment is what it wears.
```

`Create(templateId)` costruisce la `MobileEntity` in memoria e tira ogni valore casuale
una volta: genere (`random` è 50/50, non impostato è maschio), corpo (quello del template, altrimenti il corpo della razza per
il genere da `races.toml`), nome (quello del template, altrimenti uno da `name_list`, dove
`{gender}` sceglie la lista `male` o `female`), pelle, capelli e barba (dal template,
altrimenti dalla razza; `hair = []` è calvo; le femmine non hanno barba), statistiche (non impostate sono 10), punti vita,
mana e stamina (che assumono per default forza, intelligenza e destrezza), armatura, resistenze, fama,
karma e abilità (punti del template × 10, i decimi memorizzati dal mobile). `title` rimane null: si applica
quello del template. `notoriety` è quello del template, e per un template che non ne ha un umano viene lasciato senza, che
viene letto come innocente, e qualsiasi altro è `attackable` (grigio), come animali e mostri di ModernUO. Agli NPC salvati
prima di questo comportamento viene assegnato il valore all'avvio del server e viene salvato con il mondo.

`SpawnAsync(templateId, map, location)`:

1. rifiuta una posizione esterna alla mappa, prima di tutto, e un template che si risolve
   senza corpo e senza razza;
2. crea il mobile e lo colloca;
3. pubblica `MobileBeforeSpawnEvent`: un handler può modificare il mobile, anche spostarlo; il
   luogo viene poi controllato di nuovo rispetto alla mappa;
4. in un'unica transazione salva il mobile (il suo seriale proviene dall'intervallo dei mobile), poi:
   - uno zaino (`ultima.items.backpack_template`) indossato sul layer `Backpack`, per ogni NPC;
   - ogni voce di equipaggiamento tramite `IItemFactoryService`: una voce con un `gender` viene
     saltata per l'altro genere; l'oggetto viene indossato sul proprio layer (quello del template, altrimenti
     quello dei tiledata per una grafica indossabile); un oggetto senza layer, oppure con un layer occupato,
     va nello zaino, come fa UOX3;
   - l'`gold` estratto, come pile di `ultima.items.gold_template` di al massimo 65535, nello zaino;
   - un tiro di ogni tabella `loot` (una tabella elencata due volte viene tirata due volte), nello
     zaino;
   - tutto nello zaino viene collocato in un punto casuale dentro i limiti di `containers.toml`;
5. dopo il commit pubblica `MobileMovedToWorldEvent` (con il luogo in cui il mobile è stato
   salvato) e poi `MobileAfterSpawnEvent`, senza annullamento: il mobile è ormai
   salvato. Un errore prima del commit lascia il mobile in memoria senza seriale.

Il bus di eventi registra l'eccezione di un handler e prosegue, quindi un handler non può fermare o annullare uno
spawn.

`SaveAsync(mobile)` salva un mobile che ha già il proprio seriale.

## Tabelle del bottino a runtime

`LootTemplatesLoader` legge ogni `*.toml` sotto `templates/loots/`, comprese le sottocartelle,
prima dei template di mobile. Il server si arresta quando un id è vuoto o usato due volte, una voce
imposta sia `item_id` sia `loot_template_id`, un `item_id` non è un template di oggetto, un
`loot_template_id` non indica alcuna tabella, un `weight` è inferiore a 1, un `amount` può produrre valori fuori dall'intervallo da 1
a 65535, le tabelle annidate creano cicli oppure il `loot` di un template di oggetto indica una tabella inesistente. Una tabella senza voci viene conservata, registrata come avviso e non produce
nulla: due tabelle UOX3 distribuite (`randomwands`, `random_useless_junk`) sono vuote nei
dati stessi di UOX3.

`ILootService.Roll(tableId)` sceglie **una** voce, in proporzione a `weight`, e restituisce
ciò che produce, costruito tramite `IItemFactoryService` senza seriale né posizione: nulla
per una voce senza entrambi gli id (`blank` di UOX3), una pila per un oggetto impilabile, quel numero di
oggetti separati altrimenti e, per una tabella annidata, `amount` tiri di essa: `LOOTLIST=randomgems,2` tira `randomgems` due volte. (UOX3 stesso
cerca la voce annidata come lista di oggetti e non genera nulla; Moongate fa ciò che i dati
indicano chiaramente.)

## Il contratto del caricatore

`IDataLoader<TEntity>`, in `Moongate.Server.Ultima`:

```csharp
public interface IDataLoader<TEntity>
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<DataLoaderResult<TEntity>> LoadDataAsync(CancellationToken cancellationToken = default);
}
```

`InitializeAsync` prepara il caricatore, aprendo file o connessioni; `LoadDataAsync`
legge tutto e lo restituisce in un `DataLoaderResult<TEntity>`, la cui unica proprietà
è `IReadOnlyList<TEntity> Entities`. Un caricatore per template di oggetto enumererebbe
ogni file `.toml` nella propria sottodirectory di `templates/` e deserializzerebbe ciascuno
con `TomlUtils`.

## Registrare un caricatore

Chiama `AddUltimaDataLoader<TLoader, TEntity>` da `Register` di un plugin, lo stesso
punto in cui vengono registrati servizi e provider di metriche:

```csharp
container.AddUltimaDataLoader<ItemTemplateLoader, ItemTemplate>(priority: 0);
```

Il caricatore è un singleton, raggiungibile sia tramite il tipo concreto sia come
`IDataLoader<TEntity>`. La registrazione viene aggiunta all'elenco eseguito da
`DataLoaderService` all'avvio.

## Eseguire i caricatori

`DataLoaderService` è un normale servizio di avvio con priorità `-5`, dopo
`IUltimaDataService` a `-10`, perché un caricatore che legge file MUL o UOP richiede il
percorso del client già configurato. Su `StartAsync` esegue ogni caricatore registrato in
ordine crescente di priorità e conserva ogni risultato sotto il suo tipo di entità:

```csharp
IReadOnlyList<ItemTemplate> items = dataLoaderService.GetEntities<ItemTemplate>();
```

Richiedere un tipo per cui non è stato registrato nulla genera subito un'eccezione, indicando il tipo: una
chiamata `AddUltimaDataLoader` mancante fallisce esplicitamente al primo uso, non con un elenco silenziosamente
vuoto. Anche senza caricatori registrati, `StartAsync` termina comunque.

## Campi risolti casualmente

Un campo di template a volte è un valore fisso e a volte significa "scegline uno tra questi ogni
volta che un'entità viene creata da questo template". `EnumValueSpec<TEnum>` copre entrambi
senza richiedere due campi nel tipo del template:

```csharp
public readonly struct EnumValueSpec<TEnum> where TEnum : struct, Enum
{
    public bool IsRandom { get; }

    public static EnumValueSpec<TEnum> FromValue(TEnum value);
    public static EnumValueSpec<TEnum> FromCandidates(IReadOnlyList<TEnum> candidates);
    public static EnumValueSpec<TEnum> Random();

    public TEnum Resolve();
}
```

`Resolve()` viene chiamato nel punto di uso, quando un'entità viene creata dal
template, non durante il caricamento: un campo `random_of` produce un valore diverso
 a ogni spawn. Estrae da `Moongate.Core.Random.BuiltInRng`, il generatore
usato dal resto del codice.

Come testo, le tre forme sono:

| TOML | Significato |
| --- | --- |
| `rarity = "common"` | Sempre `Common` |
| `rarity = "random_of"` | Qualsiasi membro dell'enum, scelto di nuovo a ogni `Resolve()` |
| `rarity = "random_of:rare,epic,legendary"` | Uno di questi tre esatti, scelto di nuovo a ogni `Resolve()` |

L'analisi dei nomi dei membri ignora maiuscole, minuscole e trattini bassi; la scrittura usa
snake_case minuscolo, quindi `FromValue(ItemRarityType.Epic).ToString()` è `"epic"`, corrispondente a come
lo digita un autore, e ciò che viene scritto viene sempre riletto. Vedi
[EnumValueSpec](toml-types.md#enumvaluespec) per ogni forma accettata ed errore. Un
campo lo dichiara tramite il proprio tipo, senza altro:

```csharp
public EnumValueSpec<ItemRarityType> Rarity { get; set; } =
    EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Common);
```

## Campi risolti in un nuovo numero

`RangeValueSpec<T>` è la controparte numerica, generica su `INumber<T>`:

```csharp
public readonly struct RangeValueSpec<T> where T : struct, INumber<T>
{
    public bool IsRandom { get; }

    public static RangeValueSpec<T> FromValue(T value);
    public static RangeValueSpec<T> FromRange(T min, T max);

    public T Resolve();
}
```

Come testo, un numero senza virgolette (`amount = 5`) è fisso; un `min-max` tra virgolette (`amount = "5-10"`)
sceglie un nuovo valore in quell'intervallo inclusivo a ogni `Resolve()`. È accettato anche un singolo
numero tra virgolette (`amount = "5"`). Scrivere un valore fisso produce un numero senza virgolette;
scrivere un intervallo produce la forma tra virgolette. Vedi
[RangeValueSpec](toml-types.md#rangevaluespec) per ogni forma accettata ed errore.

```csharp
public RangeValueSpec<int> Amount { get; set; } = RangeValueSpec<int>.FromValue(1);
```

Le tinte hanno un proprio tipo, `HueSpec`, con la stessa forma testuale fissa o a intervallo e valori
esadecimali come `"0x03EA-0x0422"`; `ItemTemplate.Hue` lo usa. Vedi
[HueSpec](toml-types.md#huespec).

## Registrare un convertitore TOML

`EnumValueSpec<TEnum>`, `RangeValueSpec<T>`, `HueSpec`, `Serial` e i tipi punto
leggono e scrivono tramite convertitori che `MoongateUltimaPlugin` registra una volta con
`TomlUtils.AddTomlConverter`; `Visibility` usa un convertitore indicato da un attributo.
Un template non richiede altro che il tipo del campo. Per le forme accettate e scritte
di ogni tipo, gli errori e come scrivere e registrare un tuo
convertitore, vedi [Tipi di valore TOML](toml-types.md).

## Le strutture dei template

`ItemTemplate`, in `Moongate.Server.Ultima`, è una semplice struttura dati:

| Campo | Scopo |
| --- | --- |
| `Id` | Il nome stabile con cui una tabella del bottino, uno spawn o `additem` indica questo template |
| `BaseId` | L'`Id` di un altro template da cui ereditare i campi non impostati; il caricatore risolve la catena |
| `ItemId` | La grafica base del client; le proprietà fisiche a runtime provengono da `ITileDataService` salvo sostituzioni |
| `Name`, `Comment` | Una sostituzione del nome visualizzato e una nota dell'autore che nessuno legge a runtime |
| `Rarity` | `EnumValueSpec<ItemRarityType>` |
| `ScriptId` | La tabella Lua globale, definita da `scripts/items/<script_id>.lua`, le cui funzioni gestiscono ciò che accade all'oggetto (`on_use`, `on_move_over`, `on_npc_move_over`, `on_speech`, `on_equip`, `on_unequip`, `on_pickup`, `on_drop`, `on_create`, `on_timer`, `on_darkness`) e rispondono alle domande poste prima di un movimento (`can_pick_up`, `can_drop`, `can_equip`, `can_insert`); un identificatore Lua minuscolo, vuoto per nessuno. Vedi [Script degli oggetti](scripting/item-scripts.md) |
| `Movable` | Se non impostato usa i tiledata: spostabile salvo che il peso tiledata sia 255, il "non può essere sollevato" del client. I giocatori non possono raccogliere ciò che non è spostabile; i game master e gli amministratori possono |
| `Weight` | Stone con due decimali (`weight = 0.02` per una moneta); se non impostato usa il peso in stone intere dei tiledata |
| `Amount` | `RangeValueSpec<int>`: la dimensione della pila di un nuovo oggetto, fissa oppure `"10-20"`; se non impostato è 1 |
| `Stackable` | Se non impostato usa il flag tiledata `Generic` |
| `Layer` | Un nome `LayerType` come `one_handed`; se non impostato usa il layer dei tiledata |
| `TwoHandedWeapon` | `two_handed_weapon = true` su un'arma impugnata con entrambe le mani (archi, armi in asta, bastoni), come `TwoHanded` di POL: indossata su `two_handed`, non lascia alcuna mano libera. Qualsiasi altra cosa su `two_handed` (scudi, torce) va nell'altra mano, con un'arma a una mano. I tiledata non possono distinguerli: contrassegnano gli scudi come armi e gli archi come a una mano |
| `WeaponType`, `DamageMin`, `DamageMax`, `Speed` | I campi delle armi del [combattimento](combat.md): `weapon_type` è `sword`, `axe`, `pole_arm`, `mace`, `fencing`, `bow`, `crossbow` o `thrown`, come UOX3 classifica un'arma per grafica, e determina abilità, suoni e colpo; `damage_min` e `damage_max` (da 0 a 65535) sono il danno di un colpo prima dei bonus; `speed` (da 1 a 500) determina il ritardo tra i colpi `15000 / ((stamina + 100) * speed)`. Un oggetto indossato con `damage_max` superiore a 0 è un'arma. Ereditati tramite `base_id` |
| `ArmorRating`, `StrengthRequired`, `MaxHits` | `armor_rating` (da 0 a 500) è l'armatura di un pezzo di armatura (quella di uno scudo non conta ancora); `strength_required` e `max_hits` (durabilità) vengono letti e conservati, ma non ancora usati. Ereditati tramite `base_id` |
| `Dyeable` | `dyeable = true` su ciò a cui una vasca di tintura può assegnare la propria tinta, come `dyeable` di UOX3: gli abiti convertiti lo ricevono da `base_clothing` e un template lo rimuove con `dyeable = false` (una veste da morto). Se non impostato non è tingibile. Vedi [dyes.lua e dye_tub.lua](scripting/shipped-scripts.md#dyeslua-and-dye_tublua) |
| `BuyPrice`, `SellPrice` | Il prezzo a cui i venditori lo vendono e lo acquistano; non impostato significa che non lo commerciano |
| `Decays`, `DecayMinutes` | Se l'oggetto decade a terra e dopo quanti minuti; se non impostato decade quando spostabile, dopo 60 minuti, come in ModernUO. Un oggetto che non può essere raccolto decade solo quando il suo template ha sia `decays = true` sia `decay_minutes`, come le casse del tesoro. Un oggetto decade quando si trova a terra, è spostabile (oppure ha entrambi quei valori) e il suo template è visibile ai giocatori. Il conto alla rovescia (`DecayAt`, salvato con l'oggetto, quindi il tempo di fermo conta) inizia quando atterra a terra, di nuovo quando un oggetto sollevato viene respinto lì e si ferma quando viene raccolto, spostato in un contenitore o indossato; il resto di una pila divisa mantiene il tempo della pila. Un controllo ogni 5 secondi elimina gli oggetti scaduti, un contenitore con il suo contenuto, indipendentemente dalla presenza di un giocatore vicino |
| `UseRange` | Da quante caselle di distanza un giocatore può fare doppio clic sull'oggetto a terra per eseguirne l'`on_use`, da 1 a 24; non impostato vale 2, come la portata di un oggetto in ModernUO. Il bersaglio per tiro con l'arco ha 6, perché si tira da cinque o sei caselle. Quello di un template base è ereditato |
| `Loot`, `Gold` | Ciò che un contenitore contiene quando una [regione di spawn di oggetti](spawns.md#regions-of-items-treasure-chests) lo crea: `loot = ["reagents", "reagents"]` tira ogni tabella del bottino una volta (elencane una due volte per tirarla due volte) e `gold = "1d100+29"` inserisce quella quantità di oro, in pile di al massimo 65.535. Se non impostato prende quello del template base, altrimenti nulla. Solo una regione di spawn riempie l'oggetto: uno creato da un comando o uno script è vuoto |
| `LootType` | `regular`, `newbied`, `blessed` o `cursed`: cosa accade quando il proprietario muore; se non impostato è `regular` |
| `Tags` | Valori liberi per gli script in una tabella `[item.tags]`; i tag espliciti di un figlio sostituiscono l'intera mappa base |
| `Visibility` | Il tipo minimo di account che vede l'oggetto: `regular`, `game_master` o `administrator`, come `realm_directory.minimum_account_type`. Non impostato per default, quindi un template lo eredita tramite `BaseId`; un oggetto che non ne ha in tutta la catena è visibile a tutti. `IsVisibleTo(accountType)` risponde per un osservatore |
| `Hue` | `HueSpec`, `0` indica la colorazione nativa della grafica; un intervallo `"min-max"` tra virgolette ne sceglie una per spawn |
| `MaxItems`, `MaxWeight` | Nullable; impostati solo su un template di contenitore. `MaxWeight` è il peso in stone che un giocatore può inserire nel contenitore, contando ciò che è nei contenitori al suo interno: 400 se non impostato, nessun limite con 0. Lo staff non è limitato, né una cassa bancaria; un oggetto già all'interno si sposta indipendentemente dal peso del contenitore. `MaxItems` è il numero di oggetti che un giocatore può inserire nel contenitore, contati con ciò che è dentro le sue borse e rispetto a ogni contenitore che lo racchiude; unirsi a una pila non ne aggiunge, lo staff è esente e non impostato significa nessun limite. Una [cassa bancaria](bank.md) usa invece `ultima.bank.max_items`. |

I campi presenti anche nel `tiledata.mul` del client (peso, impilabilità, layer,
spostabilità) sono sostituzioni: non impostato significa tiledata, come in POL e ModernUO. Le estensioni
in `ItemTemplateExtensions` restituiscono il valore assegnato a un nuovo oggetto, come
`template.EffectiveWeight(tileDataService)`, e `Validate()` rifiuta peso o prezzo
negativi, un peso con più di due decimali, una quantità inferiore a 1, oro che può produrre valori sotto 0, un tempo di decadimento
inferiore a un minuto, una chiave tag vuota e uno `script_id` malformato.

Gli spawner, per esempio, sono solo per lo staff e i loro figli lo ereditano:

```toml
[[item]]
id = "base_spawner"
base_id = "base_item"
item_id = 7956
visibility = "game_master"

[[item]]
id = "orcspawn"
base_id = "base_spawner"
item_id = 7956
name = "Orc Spawner"
```

`MobileTemplate` definisce un mobile, creatura o NPC umano, una voce `[[mobile]]`
sotto `templates/mobiles/`. Ogni campo tranne `Id` può essere non impostato: viene ereditato tramite
`BaseId` e, se assente in tutta la catena, si applica il valore predefinito sotto. I numeri che variano per
mobile sono dadi ([`DiceSpec`](toml-types.md#dicespec)): `strength = "1d25+95"` produce da 96
a 120; una costante è un intero senza virgolette.

| Campo | Scopo |
| --- | --- |
| `Id`, `BaseId`, `Comment` | Come in `ItemTemplate` |
| `Name` | Un nome fisso come `an orc`; se non impostato ne estrae uno da `NameList` |
| `NameList` | Un id di lista in `data/names.toml`; `{gender}` diventa `male` o `female`, il genere assegnato al mobile |
| `Title` | Mostrato dopo il nome, come `the guard` |
| `Body` | Il corpo disegnato dal client; se non impostato usa il corpo della `Race` per il genere |
| `Gender` | `male`, `female` o `random` (50/50 per ogni mobile); se non impostato è `male` |
| `Race` | `human`, `elf` o `gargoyle`: pelle, capelli e barba provengono da `races.toml` salvo impostazione qui; non impostato per una creatura |
| `SkinHue`, `HairHue`, `BeardHue` | `HueSpec`; se non impostati usano le tinte della razza |
| `Hair`, `Beard` | Id di oggetto, uno scelto; se non impostati usano gli stili della razza per il genere e le femmine non hanno barba |
| `Strength`, `Dexterity`, `Intelligence` | Dadi; se non impostati sono 10 |
| `Hits`, `Mana`, `Stamina` | Dadi; se non impostati sono forza, intelligenza e destrezza |
| `Damage`, `Armor` | Dadi per un colpo disarmato e l'armatura naturale; se non impostati sono `1d4` e 0 |
| `Resistances` | `[mobile.resistances]` con `physical`, `fire`, `cold`, `poison`, `energy`, dadi in percentuale; se non impostate sono 0 |
| `Skills` | `[mobile.skills]`, nomi delle abilità come `resisting_spells` o `tactics`, dadi in punti interi da 0 a 120 |
| `Notoriety` | `innocent`, `ally`, `attackable`, `criminal`, `enemy`, `murderer` o `invulnerable`, il colore del nome; se non impostata è `innocent` per un umano e `attackable` per qualsiasi altro corpo |
| `Karma`, `Fame` | Dadi; il karma può essere negativo |
| `Equipment` | Voci `[[mobile.equipment]]`: `items` (id di template di oggetto, uno scelto), `hue` e `gender` per equipaggiare un solo genere |
| `Loot`, `Gold` | Id di template del bottino e dadi dell'oro tirati nello zaino allo spawn; nessun sistema di cadaveri ancora |
| `Sounds` | `[mobile.sounds]` con `start_attack`, `idle`, `attack`, `hurt`, `death`; uno script di mobile li riproduce per tipo con `npc.play_sound(serial, "idle")` |
| `BloodHue` | Il colore del sangue che la creatura lascia quando è colpita: 0 per il rosso, `-1` per una che non sanguina, come il `BloodHue` di ServUO e il `BLOODCOLOR` di Source-X; quello di un template base è ereditato. Non impostato: rosso. I non morti e i golem hanno `-1`. Vedi [Combattimento](combat.md#the-damage) |
| `FleeAt` | La percentuale dei suoi punti vita (da 0 a 100) sotto cui una creatura che combatte scappa dallo scontro, `-1` per una che non scappa mai, come `FLEEAT` di UOX3; quella di un template base viene ereditata. Non impostato: quella dello script, 20 per un mostro e 10 per un animale. Vedi [Script delle creature](scripting/shipped-scripts.md#commoncreaturelua) |
| `NpcGuild` | La gilda per cui questo maestro di gilda accetta membri: `mages`, `warriors`, `thieves`, `rangers`, `healers`, `miners`, `merchants`, `tinkers`, `tailors`, `fishermen`, `bards` o `blacksmiths`. Non impostato: il PNG non è un maestro di gilda. Vedi [Maestri di gilda](skills.md#guildmasters) |
| `ScriptId` | La tabella Lua globale, definita da `scripts/mobiles/<script_id>.lua`, i cui `on_think`, `on_speech`, `on_spawn`, `on_mobile_in_range`, `on_death` e `on_drag_drop` gestiscono l'NPC; un identificatore Lua minuscolo. Vedi [Script dei mobile](scripting/mobile-scripts.md) |
| `Visibility` | Come in `ItemTemplate` |
| `Movement` | `land`, `water` (un delfino: viene generato e nuota solo in acqua) o `both` (un tricheco: cammina e nuota, e viene generato a terra altrimenti in acqua); se non impostato è `land` |
| `Tags` | Valori liberi per gli script; le chiavi del figlio aggiungono e sostituiscono quelle del genitore |

`Resistances`, `Sounds`, `Skills` e `Tags` vengono ereditati chiave per chiave, quindi una base come
`base_orc` imposta i cinque suoni una volta sola e ogni orco li conserva; ogni altro campo impostato da un
figlio sostituisce quello della base. `Validate()` rifiuta dadi che possono produrre valori sotto 0 (tranne il karma),
un'abilità sconosciuta, un'abilità sopra 120, una resistenza sopra 100, un suono negativo, una
voce di equipaggiamento senza oggetto o con id di oggetto vuoto, una chiave tag vuota e uno `script_id` malformato.

```toml
[[mobile]]
id = "base_orc"
body = 0x11
name = "an orc"
notoriety = "murderer"

[mobile.sounds]
start_attack = 0x1B0
idle = 0x1B1
attack = 0x1B2
hurt = 0x1B3
death = 0x1B4

[[mobile]]
id = "orc"
base_id = "base_orc"
strength = "1d25+95"
karma = -2500
loot = ["randomgems"]

[mobile.skills]
tactics = "1d26+54"

[[mobile]]
id = "guard"
gender = "random"
race = "human"
name_list = "{gender}"
title = "the guard"
notoriety = "invulnerable"
script_id = "wander"

[[mobile.equipment]]
items = ["0x1516_skirt", "0x152e_short_pants"]
gender = "female"
```

Il `templates/mobiles/` distribuito contiene gli NPC di UOX3, convertiti da
[`mgctl convert uox`](uox3-migration.md#mobiles-and-name-lists). Vengono caricati all'avvio game e
standalone (`IMobileTemplateService`).

`LootTemplate` e `LootEntry` sono lo stesso genere di struttura:

| Campo | Scopo |
| --- | --- |
| `LootTemplate.Id` | Il nome stabile con cui un `LootEntry.LootTemplateId` o il bottino alla morte di un NPC indica questa tabella |
| `LootTemplate.Comment` | Una nota dell'autore che nessuno legge a runtime |
| `LootTemplate.Entries` | I risultati ponderati della tabella |
| `LootEntry.Weight` | La quota di questa voce nella tabella, relativa a ogni altra voce; `1` per impostazione predefinita |
| `LootEntry.ItemId` | L'`ItemTemplate.Id` da produrre; non impostato quando viene invece impostato `LootTemplateId` |
| `LootEntry.LootTemplateId` | L'`Id` di un'altra tabella da cui scegliere invece di un oggetto diretto |
| `LootEntry.Comment` | Cos'è `ItemId` o `LootTemplateId`, per una persona che legge il file |
| `LootEntry.Amount` | `RangeValueSpec<int>`, quanti `ItemId` creare |

Per produrre questi file da uno shard UOX3 esistente, vedi
[Migrare da UOX3](uox3-migration.md).

## Liste di NPC e spawn

`templates/npc_lists/` contiene liste di template di mobile da cui uno spawn sceglie, convertite dai blocchi
`[NPCLIST name]` di UOX3. Una voce indica un template di mobile o un'altra lista e viene scelta in
proporzione al suo `weight` (1 per impostazione predefinita); una voce che indica una lista sceglie poi da quella lista:

```toml
[[npc_list]]
id = "jungle"
entries = [{ mobile_id = "gorilla", weight = 20 }, { npc_list_id = "all_trolls", weight = 7 }]
```

`templates/spawns/<map>/` contiene le regioni di spawn, convertite dai blocchi `[REGIONSPAWN n]` di UOX3,
più gli spawner di ModernUO (`modernuo_*.toml` e `trammel/town_new_haven.toml`); la cartella è la
mappa e una chiave `map` in una regione viene ignorata. Uno spawn sceglie da un unico insieme, come UOX3: i suoi `mobile_ids` (peso 1 ciascuno) e
le voci dei suoi `npc_list_ids` con i relativi pesi:

```toml
[[spawn]]
id = "felucca_0"                      # unique
name = "The Hammer And Anvil"
mobile_ids = ["weaponsmith"]          # mobile templates, picked at random with the lists' entries
npc_list_ids = []                     # npc lists
max = 1                               # NPCs alive at once
min_minutes = 480                     # a new one every min_minutes to max_minutes
max_minutes = 600
call = 1                              # NPCs that come at a time
areas = [{ x1 = 1422, y1 = 1547, x2 = 1426, y2 = 1550 }]   # both corners included
exclude = []                          # parts of the areas where nothing spawns
only_outside = false                  # true: never under a roof
# pref_z = 18                         # how high above the ground a spot may be
# z = 36                              # the highest a spot may be, instead of ground + pref_z
```

Entrambi vengono caricati all'avvio, dopo i template di mobile, e un errore al loro interno ferma il server. Il comportamento
 dello spawn delle regioni a runtime, compresi i mobile acquatici, è in [Spawn degli NPC](spawns.md).

## Decorazioni

`templates/decorations/` contiene le decorazioni del mondo assenti dai file di mappa del client: porte, insegne,
luci, mobili, teletrasporti e simili, circa 32.800 posizionamenti nei 108 file caricati (115 file complessivi). È stato
convertito una volta da `Data/Decoration` di ModernUO, più New Haven di ServUO (`trammel/newhaven.toml`,
`havenisland.toml`, `havenmine.toml`, assenti in ModernUO), le insegne dei negozi e del mondo da
`signs.cfg` di ModernUO (`signs.toml`, scritto da
[`mgctl convert modernuo-signs`](uox3-migration.md#signs-of-modernuo)) e i teletrasporti del mondo e dei dungeon
dal suo `teleporters.json` (`teleporters.toml`, scritto da
[`mgctl convert modernuo-teleporters`](uox3-migration.md#teleporters-of-modernuo)), un file TOML per file sorgente, in una cartella
per mappa: `britannia/` (Trammel e Felucca), `trammel/`, `felucca/`, `ilshenar/`, `malas/`,
`tokuno/`, `termur/` e gli insiemi speciali `_ruined_magincia_tram/`, `_ruined_magincia_fel/`,
`_old_magincia/` e `_bounty_boards/`. Una cartella o un file il cui nome inizia con `_` non viene caricato: rinominalo
senza `_` per collocare la sua decorazione. `_old_magincia/` è l'arredamento di Magincia com'era
prima della distruzione: la mappa di un client attuale contiene New Magincia, ricostruita senza quegli
edifici, quindi l'insieme rimane escluso; con un vecchio client, sposta il suo file in `britannia/`.

La vecchia Haven di Trammel è accantonata allo stesso modo: `trammel/_haven.toml`,
`trammel/_haven_additions.toml` e `trammel/_signs.toml`, 631 posizionamenti. Sulla mappa di un client
attuale quella città è una rovina, con un terzo dei suoi edifici scomparso o danneggiato, quindi porte, casse e
librerie starebbero senza muri attorno; Felucca conserva la città nello stesso luogo. Con un
vecchio client, rinomina i tre file senza `_`. ModernUO nomina alcuni suoi file con un
trattino basso iniziale (`_covetous.cfg`): qui sono `covetous.toml`, oppure `despise_additions.toml` dove esiste anche un
semplice `despise.toml`, così il trattino basso significa sempre solo "non caricato". Un mondo decorato
in precedenza conserva gli oggetti che ha già: `.decorate` riconosce ciò che è collocato dal luogo in cui si trova, non dal
file, quindi i file rinominati non collocano nulla due volte e gli oggetti della vecchia Haven rimangono finché non vengono
rimossi oppure il mondo viene decorato nuovamente.

```toml
[[decoration]]
comment = "metal door"
type = "MetalDoor"                # the kind, kept as ModernUO names it
item_id = 0x0675
props = { facing = "west_cw" }    # the kind's settings; facing is a DoorFacingType
locations = [[1411, 1621, 30], [1411, 1622, 30]]
```

Un blocco senza `item_id` è un addon costruito da più grafiche. `extras`, presente in alcuni
file distribuiti, non viene letto: le impostazioni di un blocco sono le sue `props`, le stesse per ogni posizione.

[`.decorate`](commands/decorate.md) le colloca con i template di
`templates/items/decorations.toml`: `decoration`, fisso e mai soggetto a decadimento, per la maggior parte dei tipi;
`decoration_door`, lo stesso con `script_id = "door"`, per i tipi il cui nome contiene `Door`
o `Gate`; `decoration_light`, con `script_id = "light"`, per i tipi di luce di ModernUO
(candele, candelabri, lanterne, lampioni, applique, torce, bracieri); e
`decoration_teleporter`, con `script_id = "teleporter"` e `visibility = "game_master"`, per
il tipo `Teleporter`. Ogni oggetto prende la
 grafica, `hue` e `name` del blocco; le altre impostazioni rimangono nelle proprietà dell'oggetto, con
`decoration_type` = il tipo per una porta o una luce. Una luce riceve anche la propria forma `light` (quella del
blocco o del tipo) e `protected` salvo che il blocco indichi `unprotected`; la grafica
indica già se è accesa. Un blocco di porta con `locked = true` nelle proprietà colloca porte apribili solo dallo
staff. Un oggetto con una proprietà `label_number`, come un `LocalizedSign`, mostra quel testo del
client come nome, salvo che l'oggetto abbia un nome proprio. Il `point_dest = [x, y, z]` di un teletrasporto diventa le proprietà `teleport.x`,
`teleport.y` e `teleport.z`, e il suo `map_dest` la proprietà `teleport.map`, un numero `MapType`.
Un `KeywordTeleporter` prende il template `decoration_keyword_teleporter`, con
`script_id = "keyword_teleport"` e `visibility = "game_master"`, e conserva `substring`, `keyword`, `range` e `delay` come proprietà.
Un `BulletinBoard` prende il template `bulletin_board`, con `script_id = "bulletin_board"`, e
si apre con un doppio clic: vedi [Bacheche](bulletin-boards.md). Un `Clock` prende il template `decoration_clock`, con `script_id = "clock"`, e comunica l'ora con un
doppio clic; uno collocato come semplice decorazione da un'esecuzione precedente diventa un orologio dove si trova. Un
`Blocker` è una semplice decorazione la cui grafica non disegna nulla e non può essere attraversata: i giocatori
vengono fermati da esso senza vederlo e i game master e gli amministratori vedono al suo posto una lapide, come
lo mostra ModernUO.
Un tipo `Fillable...` (cassa, scatola, forziere, barile) o un `LibraryBookcase` prende il template
`decoration_fillable`, con `script_id = "fillable"`: un
[contenitore che si riempie](scripting/shipped-scripts.md#fillablelua) quando viene aperto. Il suo `content_type`
(`Inn`, `ThiefGuild`) viene conservato come nome della sua tabella (`inn`, `thief_guild`), una libreria è una
`library` e uno collocato come semplice decorazione da un'esecuzione precedente diventa riempibile dove si trova.
Un `PublicMoongate` prende il template `decoration_public_moongate`, con
`script_id = "public_moongate"` e la luce `circle300` nelle proprietà salvo che i dati ne indichino
un'altra; `.decorate` ne colloca anche uno su ogni destinazione di
[`moongates.toml`](data-files/moongates.md).
Spawner, contenitori di marcatura, addon e ogni altro tipo il cui nome termina in
`Teleporter`, quelli che richiedono un'abilità, una quest o un doppio clic (`SkillTeleporter`,
`InteractionTeleporter`, ...), non vengono ancora collocati. Le porte delle città non sono in alcun file:
`.decorate` le legge dai telai delle porte della mappa.
