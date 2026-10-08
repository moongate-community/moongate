<!-- translation: {"sourceHash":"19a5776e50cff1f7acfbe94cd26f0e863e69faa5347eb5790129048155509a19","title":"Migrare da UOX3"} -->

# Migrare da UOX3

`mgctl convert uox` converte le definizioni oggetto `.dfn` e le liste di bottino
[UOX3](https://github.com/UOX3DevTeam/UOX3) in TOML `ItemTemplate` e `LootTemplate`
Moongate, e NPC, liste di NPC, regioni di spawn e liste di nomi UOX3 in `MobileTemplate`,
TOML di liste NPC e spawn e `names.toml`. Altri sei comandi convertono
[insegne](#signs-of-modernuo), [teletrasporti](#teleporters-of-modernuo),
[luoghi nominati](#named-places-of-modernuo), [forzieri del tesoro](#treasure-chests-of-modernuo),
[spawner](#spawns-of-modernuo) e [testi dei libri](book-content-import.md) ModernUO.
Le strutture scritte sono descritte in [Caricare template TOML](templates.md#the-template-shapes);
il server le carica all'avvio da `templates/`.

## Eseguirlo

Da un checkout del sorgente:

```sh
dotnet run --project src/Moongate.Ctl -- convert uox \
  --source <file-or-directory> --destination <dir> [--loot-destination <dir>] \
  [--mobile-source <dfndata> --mobile-destination <dir> --names-destination <file>] \
  [--starting-items-destination <file>] [--scripts-source <js-dir>] \
  [--npc-lists-destination <dir> --spawns-destination <dir>]
```

Archivi di rilascio e immagini Docker distribuiscono lo stesso tool come `mgctl`
(`/app/mgctl` nell'immagine); vedi [Conversione dei contenuti UOX3](docker.md#uox3-content-conversion)
per un esempio `docker run` quando non c'è un SDK .NET locale.

`--source` è un singolo file `.dfn` o una directory cercata ricorsivamente per ogni
`.dfn` al suo interno. `--destination` riceve un `<name>.toml` per `.dfn` sorgente,
allo stesso percorso relativo, contenente un `[[item]]` per blocco con un proprio
`id=`. `--loot-destination` è facoltativo; senza di esso i blocchi `LOOTLIST` vengono
saltati. Un'invocazione senza argomenti stampa l'help ed esce con `0`; un argomento
obbligatorio mancante esce con `1`. I tre argomenti mobile vanno insieme (vedi
[Mobile e liste di nomi](#mobiles-and-name-lists)). `--scripts-source` è la cartella
`data/js` di UOX3. Usandola, un oggetto il cui script UOX3 ha un equivalente Lua
Moongate riceve il suo `script_id`: lo script del `script=` del blocco, altrimenti
quello che `jse_objectassociations.scp` ([ENVOKE]) assegna alla sua grafica, cercato
per numero in `jse_fileassociations.scp` ([SCRIPT_LIST]). Oggi `item/lights.js`
diventa `light` (`scripts/items/light.lua`); gli altri script sono esclusi. Con o
senza questa opzione, ciò che UOX3 chiama cibo (tipo oggetto 14, sul blocco o su
quello da cui prende i campi) riceve `script_id = "food"` (`scripts/items/food.lua`),
eccetto ciò che UOX3 classifica come cibo ma nessuno mangia così com'è: la ciotola
di farina (`0x0a1e_bowl_of_flour`) e il pesce magico (`base_magic_fish`). Ciò che
UOX3 chiama bevanda (tipo oggetto 105) riceve allo stesso modo `script_id = "drink"`
(`scripts/items/drink.lua`), al posto del suo `pitchers.js`, eccetto il barattolo
di miele (`0x09ec_jar_of_honey`). Una cartella a cui manca uno dei due file esce con `2`.

Viene convertito anche ciò che legge il combattimento: `damage=min max`, `spd`,
`str`, `def` e `hp=min max` di un oggetto diventano `damage_min`, `damage_max`,
`speed`, `strength_required`, `armor_rating` e `max_hits` (un valore non numerico,
o pari a 0, viene omesso), e il tipo di arma (`weapon_type`) segue la grafica di
un blocco con `id=`, secondo la tabella del `GetWeaponType` di UOX3 (una grafica
non elencata da UOX3 combatte con i pugni). Le ere UOX3 (`t2a`, `lbr`, `aos`, `tol`)
mantengono i propri valori, e il tipo va sull'oggetto che ha la grafica, da cui
lo ereditano. Vedi [Combattimento](combat.md).

Le tinture (tipo oggetto 208) ricevono `script_id = "dyes"` e la tinozza da tintura
`script_id = "dye_tub"` (`scripts/items/dyes.lua`, `scripts/items/dye_tub.lua`). UOX3
classifica la tinozza tramite la grafica `0x0FAB` in `itemtypes.dfn`, non nel blocco,
quindi il convertitore fa lo stesso: un blocco nominato con quella grafica.

`--npc-lists-destination` e `--spawns-destination` vanno insieme e richiedono
`--mobile-source`. Convertono i blocchi `[NPCLIST name]` sotto `npc/` in
`templates/npc_lists` (le voci `20|gorilla` mantengono il peso, `NPCLIST=trolls`
diventa una lista annidata) e i blocchi `[REGIONSPAWN n]` sotto `spawn/` in
`templates/spawns/<map>/`, una cartella per mappa dal `WORLD=` della regione
(0 Felucca, 1 Trammel, 2 Ilshenar), altrimenti dalla cartella sorgente. Il `GET=`
di una regione prende i campi della regione nominata, con prevalenza dei propri,
ma mai mappa, NPC, liste o ere, come UOX3; un'intestazione definita due volte mantiene
l'ultima definizione. Un `NPCLIST=x` senza peso dentro una lista importa le voci di
x (UOX3 le inserisce); un `n|NPCLIST=x` con peso resta una singola scelta.
Le regioni il cui `ERAS=` esclude `tol` (l'era moderna; il predefinito `lbr` di UOX3
mantiene le stesse) vengono saltate, gli angoli di esclusione invertiti e
`MINTIME`/`MAXTIME` vengono ordinati, e l'output viene riletto e controllato come
fanno i loader del server. `MINTIME`/`MAXTIME` restano in minuti come scritti (UOX3
li tronca a un byte). Regioni che generano solo oggetti, e NPC o liste che non si
risolvono, vengono esclusi e contati.

Ogni blocco di ogni file sorgente viene letto prima di risolvere qualsiasi catena
`get=`, perché la destinazione può vivere in un altro file: i dati UOX3 mantengono
le varianti di orientamento di una spada accanto alla definizione base, ma un
`base_item` condiviso altrove. Un `//comment` finale viene prima rimosso da ogni
riga, come fa il motore UOX3; i dati reali ne attaccano uno direttamente alla
parentesi di apertura di un blocco (`{//approximately 1%`). Il testo dopo la
parentesi di apertura (`{ Random Hair`) è un'etichetta, non una riga del blocco.

## Cosa viene mappato

Verificato sui dati UOX3 reali:

| UOX3 | ItemTemplate | Nota |
| --- | --- | --- |
| L'`id=` proprio del blocco | `ItemId` | Una lista (`id=0x0c4f 0x0c50`, uno scelto casualmente in UOX3) mantiene la prima grafica; refusi come `0x0x04FC` e `0x15b6]` vengono tollerati. Un blocco senza un proprio `id=` si converte solo quando ha un padre (sotto), con `item_id = 0`: il loader del server prende la grafica del padre |
| L'intestazione del blocco, o `name=` quando l'intestazione è solo un esadecimale | `Id` | Passato tramite `StringUtils.ToSnakeCase`; `name=` è testo libero ("brocca di vino") |
| `name=` | `Name` | Riportato invariato; UOX3 non separa un identificatore dal testo visualizzato |
| Un `get=` a destinazione singola (altrimenti `getlbr=`) | `BaseId` | Solo quando quella destinazione è stata convertita; `get=a b`, un alias casuale senza proprio `id=`, non converte nulla |
| `movable=1` o `3` / `2` | `Movable = true` / `false` | `0` o assente lo lascia non impostato, quindi decide tiledata |
| `weight=` | `Weight` | Diviso per 100: UOX3 pesa in centesimi di stone |
| `amount=` | `Amount` | Dimensione fissa di una pila |
| `pileable=` | `Stackable` | |
| `layer=` | `Layer` | Numero di layer UOX3 come nome `LayerType` |
| `layer=2` senza `type=107` (scudo) o `dir=` (luce) | `two_handed_weapon = true` | Come decide UOX3 all'equipaggiamento. Una torcia spenta (`0x0F64`) non ha nessuno dei due, quindi UOX3 e il convertitore la trattano come a due mani; i template distribuiti lo disabilitano |
| `dyeable=` o `dye=` | `Dyeable` | Lo stesso tag in UOX3; `0` scrive `dyeable = false`, che rimuove ciò che una base aveva dato |
| `value=buy sell` | `BuyPrice`, `SellPrice` | Un numero imposta entrambi |
| `decay=` | `Decays` | `1` è true, qualsiasi altro valore false |
| `newbie` o `newbie=1` | `LootType = newbied` | |
| `custominttag=name value`, `customstringtag=name text` | `Tags` | Ogni riga, così un blocco può impostarne più di uno |
| `color=` o `colour=` | `Hue` | Valore fisso, non intervallo; non impostato non scrive una tonalità, quindi il loader del server prende quella del template base |
| `weightmax=` | `MaxWeight` | UOX3 lo conta in centesimi di stone, come `weight=`: `weightmax=40000` è 400 stone, intero e arrotondato per eccesso |
| `visible=1`, `2` o `3` / `visible=0` | `Visibility = game_master` / `regular` | Nascosto, magicamente invisibile o nascosto ai GM tengono tutti l'oggetto lontano dai giocatori; `visible=0` viene scritto così sostituisce un padre nascosto; assente lo lascia non impostato |

Tutto il resto non ha ancora un posto in `ItemTemplate` e viene scartato: campi delle
statistiche di combattimento, `colorlist`, `script=` e campi multi e geometria.
`BaseId` è solo un puntatore: il convertitore non appiattisce i campi di un padre
nei figli; il loader risolverà la catena quando esiste. Un blocco padre senza un
proprio `id=`, come `[base_coin]`, ha le righe inserite in ogni figlio che lo usa con
`get=`, come UOX3, con prevalenza delle righe del figlio. Una moneta riceve così
`weight = 0.02` e `stackable = true`, e il suo `base_id` è il primo antenato con
`id=` (`base_item`). Un `name=` ereditato diventa il nome del template ma mai parte
dell'id: gli id provengono solo dalle righe proprie di ciascun blocco.

I numeri sono letti come li legge UOX3 (`stoi(value, nullptr, 0)`): esadecimali con
`0x` o decimali, quindi `layer=0x08` è un anello. Un tag senza valore, come il semplice
`decay=` di `baseitem.dfn`, è ignorato come lo ignora UOX3. Un'intestazione definita
due volte mantiene l'**ultima** definizione, come UOX3, e i file sorgenti vengono
letti in ordine ordinale affinché il risultato non dipenda dal filesystem.
`[BESTSKILL n]` segue i numeri delle abilità UOX3, dove Imbuing è 55 e Mysticism 56
(l'inverso di `SkillType`).

Un blocco senza un proprio `id=` ma con un solo padre (`get=x`, altrimenti `getlbr=x`)
che è stato convertito è anch'esso un template, id = intestazione: oggetti magici,
diari e altre varianti UOX3 (`[glacialstaff] get=0x0df1 name=glacial staff color=0x0480`)
e alias a destinazione singola. Mantiene `item_id = 0`, che il loader del server
riempie dal suo `base_id`, e la catena viene risolta in passaggi ripetuti, così anche
una variante di variante viene convertita. I dati distribuiti hanno 9665 template
oggetto in questo modo; le voci del bottino e l'equipaggiamento NPC che nominano
un tale blocco ora si risolvono in esso.

## Tabelle del bottino

I blocchi `[LOOTLIST name] { ... }` UOX3 sono tabelle di bottino pesate. Si convertono
in `--loot-destination` (`templates/loots/`, accanto a `templates/items/`) come un
`<id>.toml` per tabella, nominato con l'Id della tabella anziché del file sorgente:
i dati UOX3 reali definiscono tutte le 71 tabelle in un unico `lootlists.dfn`, e
revisionarne una non richiede di caricare tutte le altre. Ogni riga di voce è:

```text
weight|entry[,amount]
```

`weight` è `1` per impostazione predefinita quando manca il prefisso `weight|`.
`entry` è un'intestazione oggetto, risolta tramite la stessa mappa usata da `get=`;
`LOOTLIST=other`, una scelta pesata annidata da un'altra tabella; oppure il letterale
`blank`, una reale probabilità pesata di non lasciare nulla. `amount` è un conteggio
singolo o `min max` (spazio, non trattino) e si mappa su `LootEntry.Amount`, un
`RangeValueSpec<int>`.

| UOX3 | LootTemplate / LootEntry | Nota |
| --- | --- | --- |
| Nome dell'intestazione del blocco, dopo `LOOTLIST ` | `LootTemplate.Id` | Anch'esso tramite `StringUtils.ToSnakeCase`; i nomi reali sono camelCase ("eartheleLoot") |
| Prefisso `weight\|` di una voce | `LootEntry.Weight` | Valore predefinito `1` |
| Voce con intestazione oggetto | `LootEntry.ItemId` | Riempie anche `Comment` con il `name=` proprio dell'oggetto, quando presente |
| `LOOTLIST=other` | `LootEntry.LootTemplateId` | Solo quando `other` è stato convertito |
| `blank` | Né `ItemId` né `LootTemplateId` impostati | Reale probabilità pesata di nulla |
| `,amount` finale | `LootEntry.Amount` | `RangeValueSpec<int>`; `min max` (spazio) diventa un intervallo |

`ITEMLIST=`, l'equivalente UOX3 che "genera ogni voce", è una meccanica diversa,
non una scelta pesata, e non appare mai nei dati reali `lootlists.dfn`; viene scartato,
come ogni voce che il convertitore non riesce a risolvere.

## Mobile e liste di nomi

Con `--mobile-source` impostato sulla cartella `dfndata` UOX3, la stessa esecuzione
converte, dopo gli oggetti:

- ogni blocco sotto `npc/` (non `npc/npclists`) in un template `[[mobile]]`, un file
  per file sorgente sotto `--mobile-destination`, id = intestazione in snake_case;
- le venti liste `[RANDOMNAME n]` di `npc/namelists.dfn` in `--names-destination`.

Legge anche `creatures/creatures.dfn` (suoni, e `MOVEMENT=WATER` o `BOTH` come
`movement`), `colors/colors.dfn` (liste di colori) e `../dictionaries/dictionary.ENG`
(nomi numerici e titoli). Equipaggiamento e bottino vengono risolti sugli oggetti
e sulle tabelle del bottino della stessa esecuzione.

Ogni blocco npc viene convertito, così l'ereditarietà resta un `base_id` anziché
essere copiata: `GET=x` e `GETLBR=x` diventano `base_id = "x"`. LBR è l'era predefinita
UOX3; gli altri tag di era (`GETUO`, `GETAOS`, …) sono ignorati, anche se vengono
convertiti anche i blocchi che nominano.

| UOX3 | Template | Nota |
| --- | --- | --- |
| `ID=0x0190` / `0x0191` | `race = "human"`, `gender` | analogamente elfo 0x25D/0x25E e gargoyle 0x29A/0x29B; la razza fornisce il corpo |
| `ID=` altro | `body` | |
| `RACE=0/1/2` | `race` human / elf / gargoyle | solo quando il blocco non imposta un corpo non umano (`[giantrat]` UOX3 ha `RACE=2` su un ratto); le altre razze UOX3 sono scartate |
| `NAME=`, `TITLE=` | `name`, `title` | un numero è un id del dizionario; `#` prende il commento `//` della riga |
| `NAMELIST=n` | `name_list` | 1 `male`, 2 `female`, 3 `orc`, 5 `daemon`, …; `male`/`female` seguono il genere, poiché i dati UOX3 hanno NPC femminili nella lista maschile |
| `STR`, `DEX`, `INT` | `strength`, … | `96 120` diventa il dado `1d25+95`; un valore è una costante |
| `HPMAX` (altrimenti `HP`), `MANAMAX`, `STAMINAMAX` | `hits`, `mana`, `stamina` | dadi |
| `DAMAGE`, `DEF` | `damage`, `armor` | dadi |
| `RESISTFIRE/COLD/POISON/LIGHTNING`, `ELEMENTRESIST` | `resistances` | lightning è energia |
| tag delle abilità (`MAGERY=500 700`) | `[mobile.skills]` | decimi in punti, limite 120; `MAGICRESISTANCE` è `resisting_spells` |
| `KARMA`, `FAME`, `GOLD` | `karma`, `fame`, `gold` | dadi |
| `FLAG=INNOCENT/NEUTRAL/EVIL` | `notoriety` innocent / attackable / murderer | |
| `EQUIPITEM=x`, `EQUIPITEM=listobjectN` | `[[mobile.equipment]]` | una lista fornisce ogni oggetto di `[ITEMLIST N]`, uno scelto casualmente; pesi e righe `blank` vengono scartati; le liste di capelli e barba 13–15 vengono saltate. Un blocco oggetto senza proprio `id=` viene seguito: `getlbr=x` dà x, `get=a b` dà entrambi |
| `COLOR`, `COLORLIST` dopo un `EQUIPITEM` | `hue` di quella voce | una lista di colori solo quando è una serie ininterrotta di tonalità |
| `LOOT=list,n` | `loot` | tabella del bottino, n volte |
| `CUSTOMINTTAG`, `CUSTOMSTRINGTAG` | `tags` | |
| suoni `[CREATURE id]` | `[mobile.sounds]` | sul template il cui blocco imposta il corpo |

`GET=m_guard f_guard`, un maschio e una femmina della stessa razza, diventa un unico
`guard` con `gender = "random"`, `name_list = "{gender}"` e l'equipaggiamento indossato
solo da uno dei due filtrato per `gender`. I suoni impostati diversamente (gli umani
muoiono con un urlo maschile o femminile) restano non impostati anziché dare a una
femmina il suono maschile; qualsiasi altro campo diverso prende il valore maschile
e viene segnalato. Un NPC `f_` o `m_` il cui corpo è dell'altro genere (`[f_scribe]`
UOX3 ha `ID=0x0190`) prende il genere del prefisso, così la coppia si unisce comunque.
Qualsiasi altro `GET` a due destinazioni (`[dragon] GET=graydragon reddragon`, scelta
casuale in UOX3) diventa un template il cui `base_id` è la prima destinazione e viene
contato; una coppia le cui destinazioni non esistono (`shepherd`) viene saltata.

Due errori noti nei dati oggetto UOX3 vengono corretti alla lettura dei blocchi
(`UoxDataFixes`): `necro_sleeves` e `necro_leggings` nominano guanti e tunica di cuoio
come padri; ricevono maniche (`0x13cd`) e gambali (`0x13cb`) di cuoio.

`FLEEAT` diventa il `flee_at` del template (da 0 a 100, oppure `-1` per una creatura che non scappa mai; i non morti, gli
elementali e i demoni hanno `-1`); un valore fuori da questi limiti viene scartato.

Un mobile per cui Moongate ha uno script riceve il suo `script_id`: un banchiere
(`NPCAI=8`) riceve `banker` (`scripts/mobiles/banker.lua`), una guardia cittadina
(`NPCAI=4`) riceve `guard` (`scripts/mobiles/guard.lua`), le creature che attaccano tutti
(`NPCAI=2` malvagio, `11` incantatore malvagio e `88` caotico, e i non morti dei cimiteri per nome: `skeleton`,
`zombie`, `ghoul`, `headless`, `wraith`, `spectre`, `lich`) ricevono `monster`
(`scripts/mobiles/monster.lua`; gli incantatori combattono corpo a corpo finché non esisterà la magia; i buoni
combattenti e incantatori, `5` e `10`, combattono solo i criminali e non hanno ancora uno script), un animale
(`NPCAI=6`) riceve `animal` (`scripts/mobiles/animal.lua`) e un animale pauroso (`NPCAI=12`) riceve `scared_animal`
(`scripts/mobiles/scared_animal.lua`); i template basati su di essi ricevono lo script tramite `base_id`.

Scartati, senza un posto ancora: resto dell'AI e del vagabondaggio (gli altri valori di `NPCAI`,
`NPCWANDER`, `FX*`, velocità), abilità di addomesticamento e bardo (`TOTAME`,
`CONTROLSLOTS`, `TOPROV`, `TOPEACE`), negozi (`SHOPKEEPER`, `SHOPLIST`), `PACKITEM`,
`CARVE`, `FOOD`, `PRIV`, `SCRIPT` e gli altri tag senza un campo. L'esecuzione stampa
quante volte ogni tipo di valore è stato scartato.

Anche i mobile scritti vengono riletti: id univoci, ogni `base_id`, oggetto di
equipaggiamento, tabella del bottino e lista di nomi si risolve, e ogni template
supera `MobileTemplate.Validate()`.

## Oggetti iniziali

Con `--starting-items-destination <file>` (che richiede `--mobile-source`), l'esecuzione
converte anche `newbie/newbie.dfn` in un unico `starting_items.toml` di `[[set]]`,
dopo i mobile e usando gli stessi id oggetto.

| UOX3 | Insieme | Nota |
| --- | --- | --- |
| `[BESTSKILL n]` | `skill` | abilità con id n; `[BESTSKILL X]` e sezioni vuote vengono saltati |
| `[DEFAULT ALL]` | `common = true` | lo riceve ogni personaggio |
| `[DEFAULT MALE]`, `[DEFAULT FEMALE]` | `race = "human"`, `gender` | UOX3 li dà solo ai corpi umani |
| `[DEFAULT ELF MALE]`, `[DEFAULT GARG FEMALE]`, … | `race`, `gender` | |
| `PACKITEM=item,amount,newbie` | `[[set.items]]`, `equip = false` | `amount` 1 resta non impostato; `newbie` 0 o 1 diventa `false` o `true` |
| `EQUIPITEM=item,hue,newbie` | `[[set.items]]`, `equip = true` | |

Gli oggetti si risolvono come l'equipaggiamento npc: `listobjectN` dà ogni oggetto
di `[ITEMLIST N]`, un blocco oggetto senza proprio `id=` viene seguito. Gli oggetti
che non risolvono nulla vengono scartati e contati. Il file viene riletto e ogni
oggetto deve esistere. Le regole proprie di UOX3 (le tre abilità migliori, quattro
con abilità iniziali estese, e `STARTGOLD`) non sono dati e non vengono convertite.
Imposta `ultima.starting_items.best_skills` nella configurazione del server. Il
convertitore aggiunge all'insieme comune le voci proprie di Moongate per gli oggetti
presenti nella sorgente: prima 1000 monete d'oro al posto di `STARTGOLD`, poi tre
pagnotte e infine una brocca d'acqua (vedi il [file distribuito](data-files/starting-items.md)).

## Insegne di ModernUO

Le insegne dei negozi e del mondo provengono da `signs.cfg` ModernUO, il file che il
suo `[SignGen` posiziona:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-signs \
  --source <ModernUO>/Distribution/Data/signs.cfg --destination moongate_root/templates/decorations
```

Scrive un `signs.toml` per [cartella di decorazioni](templates.md#decorations)
(`britannia` per le insegne sia di Trammel sia di Felucca), sostituendo quello di una
precedente esecuzione. Le insegne solo di Trammel sono quelle della vecchia Haven,
una rovina sulla mappa di un client attuale: vengono scritte in `trammel/_signs.toml`,
che [non viene caricato](templates.md#decorations). In ogni file un testo del client
diventa un `LocalizedSign` con `label_number`, uno scritto un `Sign` con `name`,
e le insegne di Luna e Umbra mantengono la tonalità della propria città. Una riga
che non è un'insegna arresta l'esecuzione indicando sé stessa.
[`.decorate`](commands/decorate.md) le posiziona.

## Teletrasporti di ModernUO

I teletrasporti del mondo e dei dungeon provengono da `teleporters.json` ModernUO,
il file che il suo `[TelGen` posiziona:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-teleporters \
  --source <ModernUO>/Distribution/Data/teleporters.json --destination moongate_root/templates/decorations
```

Scrive un `teleporters.toml` per cartella di mappa delle
[decorazioni](templates.md#decorations) (`felucca`, `trammel`, `ilshenar`, `malas`,
`tokuno`, `termur`), sostituendo quello di una precedente esecuzione: un blocco
`Teleporter` per destinazione, con `map_dest` quando la destinazione è su un'altra
mappa. Una voce con `back` riceve anche il teletrasporto dalla destinazione alla
sorgente, e una voce successiva sostituisce una precedente sulla stessa cella entro
12 di altezza, come `[TelGen`. Una voce che non è un teletrasporto arresta l'esecuzione
indicando sé stessa, e non viene scritto nulla. [`.decorate`](commands/decorate.md) li posiziona.

## Luoghi nominati di ModernUO

I luoghi del gump `[Go` ModernUO, un file JSON per mappa con categorie dentro categorie,
diventano [`locations.toml`](data-files/locations.md), che [`.go`](commands/go.md)
elenca e raggiunge:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-locations \
  --source <ModernUO>/Distribution/Data/Locations --destination moongate_root/data/locations.toml
```

Legge `felucca.json`, `trammel.json`, `ilshenar.json`, `malas.json`, `tokuno.json` e
`termur.json`, quelli presenti, e scrive un `[[location]]` per luogo con mappa,
categorie unite da `/`, nome e posizione, sostituendo il file di una precedente
esecuzione. Un luogo senza nome o senza tre numeri arresta l'esecuzione indicando
sé stesso, e non viene scritto nulla.

## Forzieri del tesoro di ModernUO

I forzieri dei dungeon sono voci degli spawner ModernUO, da `TreasureChestLevel1`
a `4`, accanto alle creature o da soli. Questo comando prende solo i forzieri su
ogni mappa e lascia le creature ai convertitori degli spawn:

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-chests \
  --source <ModernUO>/Distribution/Data/Spawns --destination moongate_root/templates/spawns
```

Legge le ere `shared` e `post-uoml` e scrive un `treasure_chests.toml` per cartella
di mappa con forzieri, sostituendo quello di una precedente esecuzione: una
[regione di spawn degli oggetti](spawns.md#regions-of-items-treasure-chests) per
spawner con forzieri, che sceglie tra i livelli elencati dallo spawner, con i suoi
ritardi, il suo raggio di origine come area e la somma dei limiti delle voci dei
forzieri come `max`, al massimo il conteggio dello spawner. Un forziere di un altro
livello viene contato nel report ed escluso. Senza alcun forziere fallisce e non scrive nulla.

## Spawn di ModernUO

UOX3 non ha spawn per Malas, Tokuno e TerMur. Il comando `modernuo-spawns` li prende
dagli spawner [ModernUO](https://github.com/modernuo/ModernUO):

```sh
dotnet run --project src/Moongate.Ctl -- convert modernuo-spawns \
  --source <ModernUO>/Distribution/Data/Spawns --maps malas,tokuno,termur \
  --mobiles moongate_root/templates/mobiles --destination moongate_root/templates/spawns
```

Legge le ere `shared` e `post-uoml` di ogni mappa (il mondo di un client moderno) e
scrive le regioni di spawn in `<map>/modernuo_<file>.toml`, come
`malas/modernuo_doom.toml`, sostituendo i file `modernuo_` scritti da una precedente
esecuzione nelle cartelle delle mappe convertite; gli altri file della cartella
restano intatti, e una mappa senza nulla da scrivere mantiene i propri file.
L'id di una regione nomina era, file e indice dello spawner al suo interno
(`malas_modernuo_post_uoml_south_12`), così rimane uguale quando un'esecuzione
successiva, con più template, risolve più mobile. Con `--only Guildmaster` converte solo le voci delle classi il cui nome finisce così, in
`modernuo_guildmasters.toml`, e lascia stare gli altri file `modernuo_`: così i maestri di gilda di Trammel, Felucca e
Ilshenar vengono collocati senza sovrapporre gli altri spawn di ModernUO a quelli di UOX3. Usalo per mappe non coperte da
UOX3: su Felucca o Trammel aggiungerebbe gli spawn ModernUO sopra quelli UOX3.
Una regione prende la mappa nominata dallo spawner, non sempre quella della cartella,
e va nella cartella di quella mappa, poiché il server prende la mappa di una regione
dalla cartella: le Yomotsu Mines e il Fan Dancer's Dojo si trovano nella cartella
`tokuno` ModernUO e sulla mappa Malas, quindi le loro regioni sono in
`malas/modernuo_yomutso_mines.toml` e `malas/modernuo_fan_dancers_dojo.toml`, con id
che iniziano ancora con `tokuno_`. Uno spawner diventa:

- `mobile_ids`: le sue voci, ogni classe ModernUO trovata tra i template `--mobiles`.
  Il comando prova prima un alias della propria tabella (`Minter` è `banker`,
  `GreatHart` è `hart`, i guildmaster sono il venditore del proprio mestiere), poi
  la classe in snake case (`GreatHart` è `great_hart`), poi la classe ignorando gli
  underscore degli id, infine il nome breve UOX3 di un elementale (`DullCopperElemental`
  è `dullcopperele`). Una classe senza template è contata come `unknown mobile <Class>`,
  e uno spawner senza voci rimaste viene saltato.
- `max`: il suo `count`. Una voce il cui `maxCount` è sotto il conteggio, come un
  singolo guaritore vagante tra le bestie, diventa una regione propria con quel
  limite (il suo id termina con il mobile); le altre voci condividono una regione
  con il resto del conteggio, meno la quota delle voci senza template corrispondente.
- `min_minutes` e `max_minutes`: i suoi ritardi, almeno un minuto; `call` 1.
- `areas`: il suo `spawnBounds` se presente, altrimenti il quadrato del raggio di
  origine attorno alla posizione, la sola posizione quando non ha raggio, come ModernUO.
- `z`: il massimo di `spawnBounds`, altrimenti 16 sopra la posizione, così uno
  spawner in una grotta non genera sul terreno sovrastante. ModernUO esamina ogni
  altezza, quindi qui uno spawner esterno ampio resta sul terreno circa al proprio
  livello. Il suo `walkingRange` è escluso: lo script Lua degli NPC decide come vagano.

Un nome di mappa sconosciuto esce con `2`, come un `--source` mancante e una cartella
`--mobiles` senza template, che altrimenti sostituirebbe i file distribuiti con nulla.
Gli spawn distribuiti di Malas, Tokuno e TerMur provengono da questo comando; le
classi che riporta sconosciute sono gli NPC ancora da scrivere.

## Verificare l'output

Dopo aver scritto ogni file, il convertitore rilegge tutto dal disco, come un loader
reale, e lo controlla: nessuna coppia di oggetti o tabelle del bottino condivide un
Id, e ogni `BaseId`, `LootEntry.ItemId` e `LootEntry.LootTemplateId` nomina qualcosa
che è stato scritto. Questo rileva errori nel passaggio TOML di scrittura e rilettura
e due intestazioni, come `Base-Item` e `base_item`, che collidono solo dopo entrambe
passate da `ToSnakeCase`. Qualsiasi problema esce con `1` e li elenca tutti, con
prefisso `Verification failed:`; un'esecuzione corretta stampa
`Verified <N> item(s) and <M> loot table(s) read back from disk`.
