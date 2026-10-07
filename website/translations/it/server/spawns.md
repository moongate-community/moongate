<!-- translation: {"sourceHash":"abbe654db9924d7f4146d557af967de3d5248f514ba6ab0c97cc92cbc0de8f5b","title":"Spawn degli NPC"} -->

# Spawn degli NPC

Il mondo si popola di NPC dalle regioni di spawn, come `[REGIONSPAWN]` di UOX3: ogni
regione mantiene vivi fino a `max` NPC, generandone pochi per volta, e ne genera di
nuovi quando alcuni vengono rimossi o uccisi. I dati distribuiti sono quelli di UOX3,
convertiti da [`mgctl convert uox`](uox3-migration.md): 2778 regioni su Felucca, Trammel
e Ilshenar, per circa 25.000 NPC al massimo, scelti da 446 liste di NPC. UOX3 non ha
spawn per New Haven, quindi `spawns/trammel/town_new_haven.toml` aggiunge i suoi 57
punti di spawn da ModernUO: venditori, banchieri, cittadini e animali della città,
99 NPC in tutto. Malas, Tokuno e TerMur, per cui UOX3 non ha spawn, prendono i propri
dagli spawner di ModernUO tramite
[`mgctl convert modernuo-spawns`](uox3-migration.md#spawns-of-modernuo): 1.206 regioni,
circa 4.200 NPC, nei file `modernuo_*.toml` delle rispettive cartelle; gli spawner
le cui creature non hanno ancora un template sono esclusi.

## Dove si trovano i dati

| Cartella | Contenuto |
| --- | --- |
| `templates/npc_lists/` | Liste di template mobile da cui una regione sceglie, come gli animali di una foresta |
| `templates/spawns/<map>/` | Regioni di spawn; la cartella è la mappa (`felucca`, `trammel`, `ilshenar`, ...) |
| `templates/mobiles/` | [Template mobile](templates.md) nominati da liste e regioni |

Entrambi vengono caricati all'avvio, dopo i template mobile. Un errore arresta il
server indicando file e motivo:

- una lista o regione senza `id`, oppure un id duplicato;
- un template mobile, una lista o un template oggetto sconosciuto;
- una regione con sia NPC sia `item_ids`;
- una lista senza voci o con un ciclo tra le liste annidate;
- una voce di lista con sia `mobile_id` sia `npc_list_id`, con nessuno dei due o con `weight` inferiore a 1;
- una regione senza nulla da generare o senza area;
- `max` o `call` inferiore a 1, `min_minutes` inferiore a 0 o superiore a `max_minutes`;
- angoli invertiti in `areas` o `exclude`;
- una cartella che non è una mappa, o il cui nome non è minuscolo.

## Liste di NPC

```toml
[[npc_list]]
id = "jungle"
entries = [{ mobile_id = "gorilla", weight = 20 }, { npc_list_id = "all_trolls", weight = 7 }]
```

Una voce nomina un template mobile (`mobile_id`) o un'altra lista (`npc_list_id`),
e viene scelta proporzionalmente al proprio `weight` (1 per impostazione predefinita).
Una voce che nomina una lista sceglie nuovamente da quella lista, con i suoi pesi.
Nell'esempio arriva un gorilla 20 volte su 27, un troll 7 volte su 27.

## Regioni di spawn

```toml
[[spawn]]
id = "felucca_0"                      # unique across every map
name = "The Hammer And Anvil"         # shown in the staff messages and .spawns
mobile_ids = ["weaponsmith"]          # mobile templates, weight 1 each
npc_list_ids = []                     # lists whose entries join the pool with their weights
max = 1                               # NPCs alive at once
min_minutes = 480                     # the next spawn comes min_minutes to max_minutes later
max_minutes = 600
call = 1                              # NPCs spawned at a time
areas = [{ x1 = 1422, y1 = 1547, x2 = 1426, y2 = 1550 }]   # both corners included
exclude = []                          # parts of the areas where nothing spawns
only_outside = false                  # true: never under a roof
# pref_z = 18                         # how high above the ground a spot may be (18 when unset)
# z = 36                              # the highest a spot may be, instead of ground + pref_z
```

Una regione sceglie ogni NPC da un unico insieme: i suoi `mobile_ids`, ciascuno con
peso 1, e le voci dei suoi `npc_list_ids` con i rispettivi pesi.

I file distribuiti contengono anche `map = "..."`, scritto dai convertitori. Il server
prende la mappa dalla cartella e ignora la chiave, quindi un `map` diverso dalla
cartella non cambia nulla.

## Come funziona lo spawn

1. **Il controllo.** Ogni 10 secondi (il timer `npc_spawn`) ogni regione il cui tempo
   è arrivato e che ha meno di `max` NPC vivi ne genera fino a `call` altri. Attende
   poi un tempo casuale tra `min_minutes` e `max_minutes`. Una regione già a `max`
   non genera nulla e attende di nuovo.
2. **Riempimento graduale.** All'avvio la prima generazione di ogni regione arriva
   a un momento casuale entro i suoi `min_minutes`, al massimo 10 minuti, e riempie
   subito la regione fino a `max`: un mondo vuoto è pieno circa 10 minuti dopo
   l'avvio, senza che tutti gli NPC arrivino nello stesso momento. Una regione che
   trova posto solo per alcuni NPC conta come riempita e porta gli altri tramite
   `call`. Le generazioni successive seguono di nuovo `call` e i tempi.
   `[ultima.spawns] initial_fill = false` mantiene il comportamento UOX3, dove anche
   la prima generazione porta solo `call` NPC, e una regione con `call` pari a 1
   può richiedere ore per riempirsi. Gli NPC sono salvati con il mondo, quindi dopo
   un riavvio le regioni sono già piene.
3. **La posizione.** Per ogni NPC la regione sceglie prima il template, poi prova
   fino a 100 celle casuali delle proprie `areas`, fuori da `exclude`:
   - un mobile terrestre si trova sulla superficie più alta al massimo `pref_z`
     sopra il suolo (o al massimo `z`), con spazio per una persona sopra di essa;
   - un mobile il cui template ha `movement = "water"` va sull'acqua (non sangue,
     palude o abbeveratoio), e uno con `both` sulla terra, altrimenti sull'acqua;
   - con `only_outside`, una posizione sotto un tetto (uno statico oltre 10 sopra
     di essa) viene rifiutata.

   Una scelta senza posizione viene saltata, e gli altri della stessa `call` vengono
   comunque generati. Una regione che non ha posizionato nulla riprova un minuto
   dopo, dopo un avviso. La prima volta, il server esamina anche tutta la sua area
   (fino a 65536 celle, distribuite uniformemente) cercando un posto per i mobile
   scelti: quando non esiste, come per animali terrestri in mare aperto, la regione
   è disabilitata fino all'avvio successivo (o `.initial_spawn`), e un avviso per
   controllo elenca le regioni disabilitate.
4. **L'NPC.** Viene generato vestito, con il proprio bottino, ed esegue il proprio
   `on_spawn` Lua. Mantiene la regione nella proprietà `spawn.region` e l'area di
   provenienza in `spawn.x1`, `spawn.y1`, `spawn.x2`, `spawn.y2`, salvate con lui fin dall'inizio.
5. **Il conteggio.** Gli NPC vivi di una regione sono quelli il cui `spawn.region`
   è il suo id, contati a ogni controllo. Un NPC rimosso con `.remove`, o ucciso,
   libera il proprio slot, e un riavvio conserva il conteggio.

Le regioni su una mappa che il server non carica vengono saltate, e il log di avvio
ne indica il numero.

## Regioni di oggetti: forzieri del tesoro

Una regione con `item_ids` invece di `mobile_ids` e `npc_list_ids` genera oggetti a
terra. Segue gli stessi controlli, tempi, `max`, `call`, aree e regole di posizione
 di una regione di NPC; ogni generazione sceglie casualmente uno dei template oggetto.
Un oggetto occupa sempre una posizione terrestre; `only_outside`, `pref_z` e `z`
si applicano come agli NPC.

```toml
[[spawn]]
id = "felucca_chest_shared_shame_12"
name = "Treasure chest level 3"
item_ids = ["treasure_chest_level_3"]
max = 1
min_minutes = 5
max_minutes = 10
z = 36
[[spawn.areas]]
x1 = 5398
y1 = 18
x2 = 5402
y2 = 22
```

L'oggetto viene creato con ciò che il template contiene: il suo `gold` in pile e un
lancio per ogni tabella del suo `loot` (vedi [i campi degli oggetti](templates.md#the-template-shapes)).
Mantiene la regione nella proprietà `spawn.region` e conta per la regione mentre
è a terra: quando decade, viene eliminato o tolto da terra, il suo slot si libera
e la regione ne genera uno nuovo al momento successivo. I messaggi allo staff e
il progresso del mondo riguardano solo gli NPC; `.spawns` elenca una regione di
oggetti con i suoi oggetti vivi, e anche `.initial_spawn` la riempie.

I `treasure_chests.toml` distribuiti per Felucca, Trammel e Ilshenar contengono i
forzieri dei dungeon degli spawner ModernUO, scritti da
[`mgctl convert modernuo-chests`](uox3-migration.md#treasure-chests-of-modernuo): 399
regioni per un massimo di 633 forzieri. I quattro template, da `treasure_chest_level_1`
a `treasure_chest_level_4` in `templates/items/treasure_chests.toml`, sono i
`TreasureChestLevel1` fino a `4` di ModernUO:

| Livello | Forziere | Oro | Possibile contenuto |
| --- | --- | --- | --- |
| 1 | Legno | 30-129 | 1-3 gemme di un tipo, un'arma, un'armatura, vestiti, gioielli |
| 2 | Metallo | 70-169 | Fino a due pile di 1-2 reagenti, 1-8 pergamene dei primi cinque cerchi, una pozione, 1-6 gemme |
| 3 | Rinforzato in metallo | 180-419 | Una o due pile di 1-9 reagenti; fino a due di ciascuno tra 1-12 pergamene dei primi sei cerchi, pozioni, 1-9 gemme, vestiti e gioielli; oggetti magici |
| 4 | Dorato | 200-599 | 1-4 pergamene vuote; fino a tre di ciascuno tra 12 reagenti, 16 pergamene, pozioni e 12 gemme; fino a due di vestiti e gioielli; oggetti magici |

Ogni pila di gemme, reagenti o pergamene e ogni pozione arriva una volta su due;
un'arma, un'armatura, vestiti o gioielli un po' meno, perché anche le proprie tabelle
possono non dare nulla; un oggetto magico una volta su cinque, con quattro lanci al
livello 3 e sei al livello 4. Provengono dalle tabelle di `templates/loots/treasure_chests.toml`,
che creano le pile di ModernUO usando gemme, reagenti e pergamene delle altre tabelle
di bottino. Le bacchette di ModernUO sono assenti: non esiste ancora un template di bacchetta.

Un giocatore entro due tile apre un forziere con doppio clic, prende il contenuto e
può inserirvi oggetti, una pila sopra una dello stesso tipo; i giocatori intorno
vedono ciò che entra ed esce. Una regione non posiziona mai un forziere sulla cella
di un altro forziere generato. Un forziere non può essere raccolto, se non dallo staff,
e decade 45 minuti dopo la creazione, aperto o no, con ciò che resta dentro; la
regione ne crea poi uno nuovo da 5 a 10 minuti dopo. I contenitori cittadini funzionano
diversamente: rimangono e [si riempiono all'apertura](scripting/shipped-scripts.md#fillablelua).
I forzieri nascono [chiusi a chiave](scripting/shipped-scripts.md#lockpicklua-and-treasure_chestlua) e non hanno ancora trappola, ogni livello ha un aspetto unico,
e il tempo di decadimento è fisso, mentre ModernUO sceglie da 15 a 74 minuti.

## NPC acquatici e anfibi

`movement` del template mobile indica dove si muovono i suoi NPC: `land` (predefinito),
`water` o `both`. Il convertitore lo prende da `MOVEMENT` UOX3 in `creatures.dfn`:
il delfino, il cavalluccio marino, il kraken e i serpenti marini si muovono in acqua;
il tricheco, l'alligatore e gli elementali d'acqua su entrambi. Vengono generati come
descritto sopra, e `npc.step` li fa nuotare. `.spawn` rifiuta un template acquatico
in una posizione senza acqua, perché lì non potrebbe mai muoversi.

## Comportamento degli NPC

Un NPC generato esegue lo script Lua del proprio template, se presente. Il
[`wander.lua`](scripting/mobile-scripts.md) distribuito mantiene un NPC generato
nella propria area di origine, lo tiene fermo quando l'area è una singola cella,
e lo fa tornare quando è fuori.

## Per lo staff

- Game master e amministratori nel mondo ricevono un messaggio dopo ogni controllo
  che ha generato qualcosa: `Spawn: The Hammer And Anvil (Felucca): 1 NPCs` per una
  regione, oppure `Spawn: 12 NPCs in 9 regions: Yew Woods 3, ... and 4 more` indicando
  al massimo cinque regioni, seguito dal riempimento del mondo: `- world 3120/29064 (10%)`,
  gli NPC vivi di ogni regione rispetto al suo `max`. La stessa riga finisce nel log
  del server al livello Information, così si può seguire il riempimento graduale;
  il dettaglio di ogni generazione è al livello Debug.
- [`.spawns`](commands/spawns.md) elenca le regioni in cui ti trovi, con NPC vivi,
  `max` e minuti alla generazione successiva, oppure `no spot found, retrying` per
  una regione che non ha potuto posizionare nulla.
- [`.initial_spawn`](commands/initial_spawn.md) (amministratori) riempie ogni regione
  fino a `max` al controllo successivo, qualunque sia `initial_fill`, per un mondo
  nuovo o dopo una grande pulizia.
- [`.spawn`](commands/spawn.md) e [`.remove`](commands/remove.md) posizionano e
  rimuovono manualmente singoli NPC; un NPC generato e rimosso viene sostituito
  alla generazione successiva della propria regione.

I messaggi sono nella lingua del server (id 30074-30081, 30088 e 30092, vedi
[Localizzazione](localization.md)).

## Vedi anche

- [Caricare template TOML](templates.md): i campi dei template mobile, compreso `movement`
- [Migrare da UOX3](uox3-migration.md): `--npc-lists-destination` e `--spawns-destination`, e i forzieri ModernUO
- [Scrivere script Lua](scripting.md)
