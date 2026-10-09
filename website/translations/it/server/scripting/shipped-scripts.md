<!-- translation: {"sourceHash":"68d9a239adbdf638d11ebdd95c1e6da2d4d9715a188a84ada30673502332298c","title":"Script forniti"} -->

# Script forniti

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). La distribuzione fornisce questi script sotto
`scripts/`, e `mgctl init` li copia nella root: ciascuno è un esempio da leggere e modificare. Come uno
script viene associato a un template è spiegato in [Script dei mobile](mobile-scripts.md) e [Script degli oggetti](item-scripts.md),
dove sono elencati `wander.lua` e `potion.lua`.

## monster.lua

Il file `scripts/mobiles/monster.lua` della distribuzione è lo script dei mostri che attaccano i giocatori,
seguendo l'IA corpo a corpo di ModernUO: insegue un giocatore e lo combatte standogli accanto. Un template lo adotta con
`script_id = "monster"`; lo fanno le creature il cui `NPCAI` di UOX3 è malvagio, incantatore malvagio o caotico (gli orchi, gli orchi giganti, i lucertoloni, i draghi, i non morti e gli altri 200 circa template
e quelli basati su di essi; [migrazione da UOX3](../uox3-migration.md)); i buoni combattenti e incantatori, che combattono solo i criminali, no. Gli incantatori, tra cui wraith, spectre e lich,
sono incantatori in ModernUO: si avvicinano e combattono come gli altri finché non esisterà la magia. Il comportamento è il modulo
condiviso [`common/creature.lua`](#commoncreaturelua), `creature.new({ hunts = true })`. Un mostro si trova in uno di tre stati:

| Stato | Cosa fa | Termina quando |
| --- | --- | --- |
| wander | Passeggia nella propria casa, l'area della regione di spawn: circa un passo ogni due secondi, soprattutto dritto. Passeggia con `npc.wander`, che lo riporta dall'esterno, come dopo un inseguimento. Un ciclo di pensiero su venti riposa da 15 a 25 secondi, con il suono `idle` e un movimento inattivo | Vede un giocatore |
| chase | Minaccia il giocatore con il suono `start_attack` e un'animazione, passa in modalità guerra e gli si avvicina con `npc.walk_to`, un passo ogni ciclo di pensiero, senza correre. Accanto a lui si orienta verso di lui e lo combatte con `combat.attack`, una volta: colpi, impatti e [morte](../combat.md) sono del servizio di combattimento | Il giocatore si nasconde, se ne va, dista più di 32 caselle o non può essere raggiunto per 20 secondi |
| guard | Smette di combattere, resta in modalità guerra per 10 secondi guardandosi intorno | Vede un giocatore, oppure il tempo termina: torna a wander, in pace |

Un mostro colpito, o mancato, risponde all'attacco (se ne occupa il [servizio di combattimento](../combat.md)) e si rivolge a chi lo combatte
qualunque cosa stesse facendo, passeggiando o facendo la guardia, anche se non lo aveva visto: passa in modalità guerra e lo insegue, senza
minacciarlo di nuovo.

Cerca una preda ogni due secondi mentre passeggia e ogni secondo quando fa la guardia, tra le prime sei di
`npc.mobiles_in_sight`: entro 16 caselle e in linea di vista, da occhio a occhio, la più vicina per prima. La sua preda è qualsiasi giocatore
e, tra gli NPC, quelli con il nome blu, i cittadini, `mobile.notoriety` innocente: un mostro in una città attacca
la gente per strada oltre ai giocatori. Lascia in pace quelli gialli, venditori, banchieri e
guardie, che non si possono ferire, e le altre creature, animali o mostri. Non vede mai un giocatore nascosto, un fantasma, un
game master o un amministratore. Una volta che insegue la preda la segue senza vederla (`npc.can_see` con
`in_sight` false), fino al limite di inseguimento. Una preda che non riesce a raggiungere viene lasciata in pace finché non si muove. La sua minaccia e il suo gesto
sono le azioni di un corpo da mostro; una creatura con corpo umano o animale, come un brigante, non ne esegue,
perché quei corpi numerano le azioni in modo diverso. Ciò che un mostro sta facendo è mantenuto in memoria per seriale, non
salvato, e dimenticato quando muore: dopo un riavvio, o quando nessun giocatore è abbastanza vicino da farlo pensare, riparte
dal movimento casuale. I numeri (16, 32, i tempi) sono costanti all'inizio del file.

**Le guardie cittadine attaccano i mostri**, come in ModernUO: `guard.lua` considera ricercato qualsiasi NPC il cui
`npc.script_id` è `monster` e che si trova in una regione sorvegliata, come un criminale. La guardia appare accanto a lui, lo colpisce
e lo uccide con un colpo.

## guard.lua

Il file `scripts/mobiles/guard.lua` della distribuzione è lo script delle guardie cittadine: quelle presenti nelle
città tramite spawn e quelle chiamate da un giocatore dicendo "guards" (vedi
[`ultima.crime`](../server-configuration.md)). Un template lo adotta con `script_id = "guard"`; lo fanno `guard`,
`m_guard` e `f_guard`. Il server non ha ancora combattimento: una guardia uccide un criminale NPC con
un colpo, come in ModernUO, e si limita a raggiungere un criminale giocatore, perché i giocatori non muoiono ancora. Una
guardia si trova in uno di due stati:

| Stato | Cosa fa | Termina quando |
| --- | --- | --- |
| post | Passeggia intorno alla propria postazione, l'area della regione di spawn, circa un passo ogni quattro secondi, con `npc.wander`, che la riporta anche dall'esterno | Vede un criminale: passa ad arrest |
| arrest | Passa in modalità guerra; quando non è accanto al criminale appare su una casella libera a un passo da lui (`world.spot_beside`; sulla sua se nessuna è libera), con uno sbuffo di fumo dove si trovava e dove arriva e il suono del teletrasporto; dice "Rimpiangerai le tue azioni, porco!" (messaggio 30138). Poi resta sul criminale, rivolta verso di lui, e lo rincorre con `npc.walk_to` quando si muove. Accanto a un NPC criminale colpisce (un'animazione di attacco) e un secondo dopo l'NPC è morto: `mobile.kill`, con la guardia come uccisore, quindi [muore come qualsiasi altro](../death.md) e lascia il cadavere | L'NPC viene ucciso; oppure il criminale è perdonato o il suo tempo termina, si nasconde, lascia la regione sorvegliata, si allontana oltre 24 caselle dalla guardia o dalla postazione, oppure non può essere raggiunto per 10 secondi: ritorno alla postazione, in pace |

Cerca un criminale ogni secondo: il giocatore o NPC più vicino di `npc.nearby` entro 12 caselle il cui
`mobile.criminal` o `mobile.is_murderer` è true (un nome rosso è ricercato come uno grigio), che si trova in una regione sorvegliata (`world.is_guarded`) a non più di 24
caselle dalla postazione della guardia (`npc.home`), e che vede (`npc.can_see`); solo un criminale costa il
controllo lungo la linea di vista. Il riferimento è la postazione, non la guardia, quindi un criminale non può condurre una guardia
fuori città passo dopo passo. Un criminale che non riesce a raggiungere viene lasciato in pace finché non si muove. Non vede mai un
 giocatore nascosto, un game master o un amministratore. Un NPC appena ucciso è ancora presente mentre
cade: la guardia non lo attacca di nuovo. Un teletrasporto rifiutato lascia la guardia dov'è, affinché
corra verso il criminale.

Una guardia chiamata porta la prop `guard.summoned`: è già arrivata accanto al criminale e ha pronunciato la battuta,
quindi resta su di lui in silenzio senza passeggiare, e quando quel criminale viene lasciato andare non arresta
nessun altro e attende di essere mandata via. Ciò che una guardia sta facendo viene mantenuto in memoria per seriale, non salvato.
I numeri (12, 24, i 10 secondi) sono costanti all'inizio del file.

**La guardia arciere**, template `archerguard` (le guardie chiamate a Ilshenar e Malas, sul modello dell'`ArcherGuard` di ModernUO, senza il suo cavallo e le sue statistiche: sono quelle della guardia con un arco), è
questa guardia con un arco in mano e `combat.range` di 10. Davanti a un NPC che attacca, un criminale o un mostro, che sia
entro la sua portata e nella sua vista, non gli si avvicina: resta dov'è, lo guarda e avvia lo scontro
(`combat.attack`), e il servizio di combattimento spara con l'abilità Archery; l'arresto finisce quando il bersaglio è morto.
Fuori portata, o fuori vista, gli si avvicina come ogni guardia, e spara da lì. Con un giocatore è la stessa guardia
delle altre: gli si avvicina e gli sta addosso.

## animal.lua e scared_animal.lua

Gli animali, secondo l'IA animale di ModernUO, usano lo stesso modulo con `hunts = false`: passeggiano nella propria casa, ogni tanto si riposano
con il suono di inattività e un'animazione, e non attaccano mai un giocatore. `scripts/mobiles/animal.lua` (`script_id = "animal"`, le
creature il cui `NPCAI` di UOX3 è 6: orsi, lupi e simili) reagisce quando viene colpito, come un mostro.
`scripts/mobiles/scared_animal.lua` (`script_id = "scared_animal"`, `NPCAI` 12) no: un animale pauroso colpito
smette di combattere e fugge, fino a dodici celle e al massimo dieci secondi, lontano da chi l'ha colpito, poi riprende a passeggiare. Il
servizio di combattimento fa rispondere al colpo ogni NPC colpito tranne uno il cui template ha questo script, che si limita a fuggire.

## common/creature.lua

`scripts/common/creature.lua` è ciò che i tre script sopra condividono, preso con
`local creature = require("common.creature")`. `creature.new(options)` restituisce la tabella che uno script mobile definisce, con il suo
`on_think`; le opzioni sono `hunts` (attacca i giocatori che vede), `flees` (fugge da un colpo invece di
rispondere) e `flee_at` (vedi sotto).

**Arcieri.** Una creatura che impugna un arco o una balestra ha `combat.range` di 10 o 8 ([Combattimento](../combat.md#archers)):
il suo inseguimento si ferma dove la preda è entro quella portata e nella sua linea di vista, `npc.can_see`, la guarda e avvia
lo scontro, e il servizio di combattimento spara. Troppo lontana, cammina finché la preda è un passo dentro la portata; entro la portata ma
senza linea di vista, si avvicina. Non arretra davanti a una preda che si avvicina: neppure gli arcieri di ModernUO,
a meno che il costruttore lo chieda.

**Fuga da feriti**, come le creature di ModernUO: una che combatte (non un animale pauroso, che scappa da un colpo) e ha
meno di `flee_at` percento dei punti vita scappa con una probabilità su dieci a ogni think, per 10-30 secondi,
dritta lontano dalla preda, correndo. La percentuale è il `flee_at` del template, altrimenti l'opzione dello script: 20 per un
mostro, 10 per un animale; un template con `flee_at = -1`, come il `FLEEAT=-1` di UOX3 dà ai non morti, agli elementali e ai
demoni, non scappa mai. Mentre scappa non risponde a un colpo: imposta la prop dell'NPC `combat.passive`, che il servizio di
combattimento legge, e quando finisce torna a passeggiare, e quindi a cacciare di nuovo. Una tua creatura lo prende allo stesso modo: `mycreature = creature.new({ hunts = true })` in
`scripts/mobiles/mycreature.lua`. Una root i cui script sono sostituiti manualmente ha bisogno anche di `scripts/common/`, altrimenti le creature
smettono di pensare.

## orione.lua e vega.lua

Il repository fornisce anche due gatti di Moongate v2, `orione` e `vega` (`templates/mobiles/moongate_cats.toml` con `scripts/mobiles/orione.lua` e `vega.lua`): generali con `.spawn orione` o `.spawn vega`.

## door.lua

La distribuzione fornisce anche `scripts/items/door.lua`, lo script del template
`decoration_door` che [`.decorate`](../commands/decorate.md) assegna a porte e cancelli. Un doppio clic
su una porta chiusa apre lei e la porta collegata (prop `door.link`): la grafica passa alla
successiva, la porta si sposta di lato secondo la prop `facing` e riproduce il suono del suo
`decoration_type` (metallo, legno, cancello o segreta). Un doppio clic su una porta aperta chiude entrambe
quando nessuno si trova in uno dei due vani. Una porta aperta si chiude da sola dopo 20 secondi, poi
riprova ogni 10 secondi mentre il vano è occupato. Una porta che non può spostarsi di lato, come
una al bordo della mappa, resta chiusa. Un NPC che cammina verso un luogo e trova una porta chiusa sulla sua strada la apre tramite `on_npc_use(serial, opener)`: la porta e quella collegata si aprono e si richiudono da sole come per un giocatore, e una porta chiusa a chiave, o una porta doppia con una delle due ante a chiave, resta chiusa senza una parola. Lo stato aperto è la prop `door.open`, con la
posizione chiusa in `door.x`, `door.y` e `door.z`, salvata con la porta, così come il timer di chiusura automatica (il timer `close` della porta, avviato con
`item.start_timer`): una porta lasciata aperta quando il server si arresta si chiude quando torna attivo. Una porta salvata
aperta da una versione precedente non ha timer e resta aperta finché qualcuno la usa. Una porta chiusa con la prop `locked` non
si apre per i giocatori, che leggono "È chiuso a chiave." (messaggio 398, nella lingua del server), a meno che
portino in qualsiasi punto dello zaino una chiave la cui prop `key.value` è il `key.value` della porta
(messaggio 405: la aprono e resta chiusa a chiave); game master e amministratori la aprono
(messaggio 404). La prop proviene dai dati della decorazione
(`props = { facing = "west_cw", locked = true }`), come le porte laterali della banca di New Haven.
`.lock` assegna a una porta un numero di chiave e `.key` ne crea la chiave.

## light.lua

`scripts/items/light.lua` accende e spegne candele, candelabri, lanterne, lampioni, applique e
torce: lo usano il template `decoration_light` e i template delle luci di
`templates/items`. Un doppio clic su una luce spenta le assegna la grafica accesa (le coppie di ModernUO),
una forma della luce se non ne ha e il suono `0x47`; un doppio clic su una accesa le assegna la
grafica spenta e il suono `0x3BE`, conservando la forma per la volta successiva. Una luce senza grafica
spenta, come un braciere, resta com'è. Le luci posizionate da `.decorate` hanno la prop
`protected`: solo game master e amministratori le accendono o spengono. I lampioni cittadini
si accendono e spengono da soli: ogni 30 secondi il server chiama `on_darkness(serial, dark)` su un
lampione il cui punto è diventato buio o luminoso (`ultima.world.lamp_post_light`), e `light.lua`
cambia la grafica silenziosamente.

## food.lua

`scripts/items/food.lua` è lo script di ciò che si può mangiare, come `Food` di ModernUO: i template
convertiti del cibo contengono `script_id = "food"`. Il doppio clic su un pezzo ne mangia uno: il valore di fame del giocatore aumenta
della prop `food.fill` dell'oggetto (3 in sua assenza), fino a 20; recupera da 6 a 8 punti di stamina, emette
il suono e, con un corpo di tipo `Human` (`mobile.body_type`), il gesto di mangiare, e legge quanto si sente sazio
nella lingua del client (messaggi da 500868 a 500872). Un giocatore sazio legge "Sei semplicemente troppo pieno
per mangiare ancora!" (500867) e non mangia nulla.

## drink.lua

`scripts/items/drink.lua` è lo script di ciò che si può bere, come le bevande di ModernUO: i template
convertiti delle bevande contengono `script_id = "drink"`. Un doppio clic ne beve un sorso: il valore di sete del giocatore aumenta
della prop `drink.fill` dell'oggetto (3 in sua assenza), fino a 20, ed emette il suono e, con un corpo di
tipo `Human`, il gesto di bere. La grafica indica quanti sorsi contiene un contenitore pieno: una
brocca o una bottiglia 5, una caraffa 10, un bicchiere o una tazza 1; i sorsi rimasti vengono mantenuti nella prop `drink.uses`.
Una volta vuota, una brocca, un bicchiere o una tazza passa alla grafica vuota, viene rinominata e resta; una bottiglia o caraffa
scompare. Un giocatore dissetato legge "Sei semplicemente troppo pieno per bere ancora!" e non beve nulla.
Riempimento, versamento e ubriachezza non sono ancora presenti.

## Le abilità di osservazione

Quattro abilità della finestra delle abilità, in `scripts/skills/`, come in ModernUO. Il giocatore sceglie un bersaglio e
legge i testi del client come messaggi di sistema (ModernUO li mostra sopra chi è esaminato). Ognuna aspetta il `delay`
di `data/skills.toml`.

- **`anatomy.lua`:** un mobile entro 8 caselle; il controllo da 0 a 100 che riesce dice quanto sembra forte e agile e,
  da 65 punti, quanta resistenza gli resta. Ciò che legge è sbagliato fino a 25 meno uno ogni 4 punti dell'abilità. Un
  controllo fallito legge che non riesce a farsi un'idea delle sue caratteristiche fisiche; se stessi, un PNG invulnerabile
  e un oggetto hanno testi propri.
- **`evaluating_intelligence.lua`:** lo stesso per la mente: da 0 a 120, "He", "She" o "It" (`mobile.is_female`) e, da
  76 punti, il mana rimasto; sbagliato fino a 20 meno uno ogni 5 punti.
- **`forensic_evaluation.lua`:** un cadavere entro 10 caselle, da 0 a 100: un cadavere umano dice da chi è stato ucciso
  (`corpse.killer` e `corpse.killer_name`, "no one" se non da qualcuno); quello di un animale o di un mostro legge
  "You notice nothing unusual.". Un mobile, da 40 a 100, "You notice nothing unusual.", perché non c'è
  una gilda dei ladri. Chi ha disturbato il cadavere e chi lo ha studiato prima non viene ancora registrato.
- **`detecting_hidden.lua`:** un luogo entro 12 caselle, o se stessi: ogni giocatore o PNG nascosto entro un decimo
  dell'abilità in caselle (la metà se il controllo fallisce, e nessuna sotto 10 punti) viene mostrato se l'abilità del
  cercatore più un tiro da -10 a 10 non è inferiore al suo Hiding più il proprio; legge "You have been revealed!". Lo staff
  viene trovato solo da altro staff; l'abilità aspetta 10 secondi.
  Trappole, case e fazioni non ci sono ancora.

## pickaxe.lua e ore.lua

`scripts/items/pickaxe.lua` è lo script dei picconi e delle pale (`script_id = "pickaxe"`) e
`scripts/items/ore.lua` quello dei quattro mucchi di minerale di ferro (`script_id = "ore"`): vedi [Estrazione e fusione](../mining.md).
Uno scavo sceglie un luogo (`target.pick_location`), che dà il `land` della casella e la `graphic` di uno statico scelto
lì: lo script contiene i terreni che sono roccia e gli statici che sono il pavimento di una grotta. Il personaggio colpisce
(`mobile.animate`, `mobile.play_sound`, `timer.after`), nel luogo deve restare del minerale (`harvest.amount`), l'abilità
Mining viene provata tra 0 e 100 (`skill.check`), e uno scavo riuscito toglie dal luogo (`harvest.take`) e dà
un mucchio (`item.give`). Una fusione sceglie una forgia, un oggetto (`item.item_id`, `item.in_range`) o uno statico, prova l'abilità
tra 25 e 75, e trasforma il mucchio in lingotti (`item.consume`, poi `item.give`) oppure ne brucia metà; un singolo minerale
che fallisce rimpicciolisce. Un mucchio su un cursore viene rifiutato (`item.is_held`). Le costanti in cima a ogni script sono i suoi numeri e i suoi elenchi.

## axe.lua

`scripts/items/axe.lua` è lo script delle asce (`script_id = "axe"` su nove asce base, da cui le loro asce lo prendono):
vedi [Taglio della legna](../lumberjacking.md). L'ascia deve essere in mano a chi vi fa doppio clic
(`item.worn_by`). Il punto scelto deve essere un albero entro 2 caselle: `target.pick_location` dà la `graphic` dello
statico scelto, e lo script contiene le grafiche che sono alberi. Il personaggio colpisce da una a tre volte
(`mobile.animate`, `mobile.play_sound`, `timer.after`), nel luogo deve restare della legna (`harvest.amount`), l'abilità
Lumberjacking viene provata tra 0 e 100 (`skill.check`), e un taglio riuscito toglie dal luogo
(`harvest.take`) e dà 10 tronchi (`item.give`). Le costanti in cima allo script sono la distanza, i colpi
e i tronchi; gli alberi sono in `scripts/common/trees.lua`, condiviso con `scripts/items/blade.lua`, lo script di coltelli, pugnali e spade (`script_id = "blade"`), che stacca un legnetto da un albero. Usata sui tronchi nello zaino, l'ascia sega la pila in assi (`item.template`, `item.consume`, poi `item.give`). Un luogo è di un solo tipo di legno, la vena della sua zona (`harvest.vein`): la tabella `WOODS` contiene tronchi e assi di ogni tipo, il Lumberjacking richiesto (`mobile.skills`) e i limiti tra cui il taglio viene provato, e la tabella `FINDS` ciò che un maestro trova insieme ai tronchi. Chi sta tagliando è tenuto in memoria per seriale: un riavvio libera tutti.

## fishing_pole.lua

`scripts/items/fishing_pole.lua` è lo script delle canne da pesca (`0x0dbf_fishing_pole`, `0x0dc0_fishing_pole`,
`script_id = "fishing_pole"`): vedi [Pesca](../fishing.md). Fai doppio clic sulla canna e scegli dell'acqua entro 4 caselle e
in vista (`target.pick_location`, `world.is_water`, `world.line_of_sight`). Il personaggio lancia (`mobile.animate`),
l'acqua schizza 1,5 secondi dopo (`effect.at`, `world.play_sound`) e il risultato arriva dopo 8 secondi
(`timer.after`). Nel luogo devono restare dei pesci (`harvest.amount`), l'abilità Fishing viene provata tra 0 e 100
(`skill.check`), e la presa viene messa nello zaino (`item.give`) e tolta dal luogo (`harvest.take`).
Le costanti in cima allo script sono la distanza, i secondi e ciò che esce. Chi sta pescando è tenuto in
memoria per seriale: un riavvio libera tutti.

## bandage.lua

`scripts/items/bandage.lua` è lo script della benda pulita (`0x0e21_clean_bandage`, `script_id = "bandage"`),
come la Healing classica di ModernUO. Si fa doppio clic, si sceglie a chi è destinata e si aspetta: chi è stato
scelto viene curato, o resuscitato.

- **Portata:** la benda nello zaino, e chi la riceve entro 1 casella; più lontano, il "too far away" del client.
  Il curatore deve vedere chi riceve la benda: uno nascosto o dietro un muro "can not be seen". Usare una benda
  rivela un curatore nascosto. La benda esce dalla pila quando la cura inizia. Una seconda benda dello stesso curatore sostituisce la prima.
- **L'attesa:** 3 secondi per un curatore con 100 di destrezza o più, 4 da 40, 5 sotto; 5 in più per resuscitare
  un fantasma; 9,4 + 0,6 × (120 − destrezza) / 10 su sé stessi. Il curatore deve restare entro 1 casella e vivo,
  altrimenti la cura va persa insieme alla benda.
- **Un vivo ferito:** riesce con una probabilità di (Healing + 10) %, e cura da Anatomy / 5 + Healing / 5 + 3 fino
  a Anatomy / 5 + Healing / 2 + 10 punti; un tiro sotto 1 cura 1 e dice che le bende hanno aiutato appena. Una
  creatura con il corpo di un mostro o di un animale è un caso per Veterinary e Animal Lore, con un punto in più
  ogni 100 punti vita. Chi non è ferito legge "That being is not damaged!" e la benda resta.
- **Un fantasma:** servono 80 punti di Healing e di Anatomy e una probabilità di (Healing − 68) / 50; poi al
  fantasma viene chiesto, nel gump degli ankh, se vuole tornare, e gli costa un decimo della fama, come a un ankh.
- **Abilità:** entrambe vengono provate per la crescita dopo una cura, anche se il tiro non è riuscito, e dopo una
  resurrezione riuscita.

Non ci sono ancora veleno e sanguinamento, quindi nessuna cura per essi; e il terreno dove un fantasma viene
resuscitato non viene controllato, come non lo è a un ankh.

## stealth.lua

`scripts/skills/stealth.lua` è lo script di Stealth, come quello classico di ModernUO. Un giocatore nascosto usa
l'abilità e, se il controllo riesce, può fare alcuni passi senza essere mostrato: un decimo della sua Stealth in passi,
almeno uno (`mobile.set_stealth_steps`; il server li conta in `MoveRequestPacketHandler`). Correre lo mostra sempre.
Nascondersi o essere mostrati di nuovo azzera i passi.

- **Prima del controllo:** chi non è nascosto riceve l'invito a nascondersi prima (502725); chi ha meno di 80 punti di
  Hiding "non è nascosto abbastanza bene" (502726), e chi ha un valore di armatura (`combat.armor_rating`) di 26 o più "non
  può sperare di muoversi in silenzio" (502727): entrambi vengono mostrati.
- **Il controllo** va da -20 a 80 punti, ciascuno aumentato del doppio del valore di armatura. Un successo legge "You begin
  to move quietly." (502730); un fallimento legge "You fail in your attempt to move unnoticed." (502731) e mostra il
  giocatore. L'abilità aspetta 10 secondi in ogni caso.
- **Non c'è ancora:** le regole di Stealth delle versioni successive (il costo dei passi per armatura, il furtivo in sella).

## snooping.lua

`scripts/skills/snooping.lua` è lo script di Snooping, come quello di ModernUO. Non si usa dalla finestra delle abilità:
un doppio clic sullo zaino di un altro mobile non lo apre, il server chiama `on_snoop(user, owner, container)` di questo
script (`ISkillScriptService.Call`). Lo fa per lo zaino e per un sacco al suo interno, e non per un giocatore morto.

- **Lo staff fruga chiunque, sempre:** un game master o un amministratore non ha bisogno di distanza, abilità o regole, non
  perde karma e non viene notato, e può frugare un proprietario morto e un altro membro dello staff. Il comando `hide`
  lo nasconde prima.
- **Regole** per gli altri: entro una casella dal proprietario; niente se il proprietario è morto; un game master o un
  amministratore non si può frugare, e nemmeno un giocatore invulnerabile ("You cannot perform negative acts on your
  target."; Moongate non ha ancora regole sugli atti dannosi per mappa, che questo sostituisce). Un PNG in una regione
  sorvegliata di una mappa diversa da Felucca si fruga solo se non è umano, oppure attaccabile, oppure assassino: lo dice il
  commento di ModernUO, anche se il suo codice lascia frugare chiunque un PNG in una città attiva.
- **Un giocatore che non è staff** perde 4 di karma, come `AwardKarma` di ModernUO prende una perdita (di più da un buon
  nome, niente sotto -400, mai sotto -15000, e la perdita viene comunicata), ed è notato dai giocatori entro 8 caselle ("You notice <nome>
  attempting to peek into <proprietario>'s belongings."): sempre sotto 100 punti di Snooping, con una probabilità pari ai
  punti su cento di passare inosservato.
- **Il controllo** va da 0 a 100 punti: un successo apre lo zaino sul client del giocatore (`item.show_contents`); un
  fallimento legge "You failed to peek into the container." e mostra il giocatore, più probabilmente quanto meno ha di
  Hiding. Lo staff vede sempre.
- **Non c'è ancora:** le trappole dei contenitori. Gli oggetti visti non si possono sollevare: il sollevamento di un
  oggetto che il giocatore non possiede è rifiutato come prima.

## lockpick.lua e treasure_chest.lua

`scripts/items/lockpick.lua` è lo script dei grimaldelli (`0x14fb`, `0x14fc`, `0x14fd` e `0x14fe`,
`script_id = "lockpick"`), come il `Lockpick` di ModernUO. Si fa doppio clic, si sceglie un oggetto chiuso a chiave entro
una casella, e dopo tre secondi viene provata l'abilità; il giocatore deve restare entro una casella.

Si può scassinare un oggetto con queste prop: `locked` (true finché è chiuso), `lock.level` e `lock.max`, i punti di
Lockpicking da cui la prova può appena riuscire e da cui non fallisce mai, e `lock.required`, il minimo per provare. Una
serratura senza `lock.level` "non si può scassinare con mezzi normali"; chi ha meno di `lock.required` "non vede come si
possa manovrare quella serratura". Un successo apre l'oggetto per sempre (`locked` è false, `lock.picker` è il giocatore);
un fallimento rompe il grimaldello una volta su quattro, che esce dalla sua pila. Lockpicking è un'abilità che sale con
l'uso, come le altre.

`scripts/items/treasure_chest.lua` è lo script dei quattro forzieri del tesoro dei dungeon
(`templates/items/treasure_chests.toml`, `script_id = "treasure_chest"`), come i `TreasureChestLevel1` a `4` di ModernUO.
Un forziere nasce chiuso: chiede 57, 72, 84 e 92 punti di Lockpicking per livello, e la prova va da quel valore meno un
tiro da 1 a 10 a quel valore più un tiro da 1 a 10. Un forziere chiuso non si apre ("It appears to be locked."); un game
master lo apre ("That is locked, but you open it with your godly powers."); uno scassinato si apre come ogni contenitore.
Nessuno tranne un game master lascia cadere un oggetto in un forziere chiuso (`can_insert`). Le trappole dei forzieri di
ModernUO non ci sono ancora, e ciò che è già dentro un forziere aperto su un client si può ancora sollevare. I forzieri
creati prima di questa versione non hanno serratura e restano come erano finché non decadono.

## training_dummy.lua

`scripts/items/training_dummy.lua` è lo script dei manichini da allenamento (`0x1070` e `0x1071` rivolti a sud, `0x1074`
e `0x1075` rivolti a est), come il `TrainingDummy` di ModernUO: i template hanno `script_id = "training_dummy"`, e così anche
`decoration_training_dummy`, che `.decorate` dà ai manichini dei file. Si fa
doppio clic su un manichino con un'arma da mischia in mano, o a mani nude: il giocatore si gira e colpisce
(`combat.swing`), il manichino mostra la grafica oscillante da un quarto di secondo, con il suono di un colpo, e dopo
tre secondi torna a riposo, e l'abilità dell'arma (Wrestling per i pugni) viene provata da -25 a 25 punti, quindi può
salire fino a 25.

Un arco o una balestra non possono allenarsi su di esso ("You can't practice ranged weapons on this."), l'arma deve
raggiungerlo (una casella, `combat.range`), un manichino che oscilla ancora fa aspettare il giocatore, e un'abilità a
25 legge "Your skill cannot improve any further by simply practicing with a dummy.". Non c'è il controllo del
giocatore in sella: le cavalcature non esistono ancora.

## archery_butte.lua

`scripts/items/archery_butte.lua` è lo script dei bersagli per il tiro con l'arco (`0x100A` rivolto a est, `0x100B`
rivolto a sud), come l'`ArcheryButte` di ModernUO: i template hanno `script_id = "archery_butte"` e `use_range = 6`, come
`decoration_archery_butte`, che `.decorate` dà ai bersagli dei file, così il giocatore può fare doppio clic da dove tira.
Un mondo decorato prima di questa versione li ha come semplice decorazione: un nuovo `.decorate` li trasforma in questi.

- **Tiro:** con un arco o una balestra, ci si mette davanti al bersaglio, in linea con esso, a cinque o sei caselle, e
  si fa doppio clic. Una freccia o un dardo viene speso (`combat.spend_ammo`), il giocatore tira (`combat.swing` e la
  freccia che vola, `effect.moving`) e l'abilità dell'arma viene provata da -25 a 25 punti: può salire, e il tiro può
  mancare. Tra due tiri allo stesso bersaglio passano due secondi. I testi dicono che cosa non va quando il giocatore sta
  dietro, fuori linea, troppo lontano o troppo vicino.
- **Punteggio:** un tiro a segno vale 50 (il centro, uno su dieci), 10, 5 o 2 punti, e al giocatore viene detto il suo
  totale a quel bersaglio e quanti tiri ha fatto. La freccia può spezzarsi, più probabilmente quante più munizioni
  sono conficcate nel bersaglio (il 2 per cento ciascuna), e allora vale di più e va persa. I testi sono detti a chi
  tira, non agli altri.
- **Raccolta:** doppio clic sul bersaglio entro una casella, quando ci sono frecce o dardi conficcati (prop
  `butte.arrows` e `butte.bolts`): vanno nello zaino e i punteggi vengono azzerati.

I bersagli per freccette non esistono ancora.

## dyes.lua e dye_tub.lua

`scripts/items/dyes.lua` e `scripts/items/dye_tub.lua` tingono i vestiti in due passaggi, come ModernUO;
i template convertiti delle tinture (`0x0fa9_dyes`) e della vasca (`0x0fab_dying_tub`) contengono
`script_id = "dyes"` e `script_id = "dye_tub"`.

1. Fai doppio clic sulle tinture e scegli una vasca: si apre il selettore di colori del client con la vasca,
   che assume il colore scelto, da 2 a 1001, come proprio colore.
2. Fai doppio clic sulla vasca e scegli cosa tingere: assume il colore della vasca, con il suono della tintura
   (`0x23E`).

Si può tingere un oggetto il cui template indica [`dyeable = true`](../templates.md), come gli abiti
convertiti da UOX3. Non deve essere indossato, e il giocatore deve raggiungere l'oggetto, la vasca e le tinture:
trasportati, oppure a terra entro 1 casella. Né le tinture né la vasca vengono consumate, e una vasca mai
tinta ha colore 0, che rimuove il colore. Un oggetto tenuto sul cursore non viene tinto ("Non puoi tingere
quello."), e uno dentro un contenitore a terra conta come troppo lontano: prendilo prima.

I testi sono quelli del client, letti nella sua lingua:

| Testo | Cliloc |
| --- | --- |
| Seleziona la vasca su cui usare le tinture. | 500856 |
| Usa questo su una vasca per tintura. | 500857 |
| Seleziona gli abiti da tingere. | 500859 |
| Non puoi tingere abiti indossati. | 500861 |
| Non puoi tingere quello. | 1042083 |
| È troppo lontano. | 500446 |

Il giocatore può rispondere al selettore di colori molto più tardi, oppure mai. Quando arriva la risposta lo script controlla
di nuovo che tinture e vasca siano raggiungibili, e il server accetta una risposta solo per il selettore
che ha aperto: gli altri emulatori la accettano come arriva. `scripts/common/dye.lua` contiene ciò che i due
script condividono (`dye.reach`, `dye.worn`, `dye.tell`). Le vasche speciali (pelle, mobili, nere,
metalliche) e le tinture dei capelli non sono ancora presenti.

## hiding.lua

`scripts/skills/hiding.lua` è lo [script dell'abilità](../skills.md) Hiding, come quello di ModernUO senza
ciò che richiede un combattimento o una casa. Un giocatore che usa l'abilità viene verificato con
`skill.check(user, "hiding", 0, 100)`: la probabilità è pari ai suoi punti su cento, e il tentativo può aumentare
l'abilità.

- Successo: il giocatore è nascosto (`mobile.set_hidden`), fuori dalla modalità guerra, e legge "Ti sei nascosto
  bene." (cliloc 501240).
- Fallimento: il giocatore viene mostrato, anche se era nascosto, e legge "Non riesci a nasconderti qui."
  (501241).

In entrambi i casi attende prima di un'altra abilità il `delay` di `hiding` in
[`data/skills.toml`](../data-files/skills.md), 10 secondi. Il primo passo lo mostra di nuovo, con "Sei stato
rivelato!" (500814): il server lo fa per ogni giocatore nascosto di un account regolare, a
meno che [Stealth](#stealthlua) non abbia permesso il passo; girarsi sul posto non lo fa. Lo staff si nasconde per osservare e resta nascosto.
Parlare, essere colpiti e la vista di chi sta vicino non lo mostrano ancora.

## Props di rigenerazione

Punti vita, mana e stamina tornano da soli (vedi
[`ultima.regeneration`](../server-configuration.md)). Uno script cambia il ritmo di un mobile con le sue
props, in secondi per punto: `mobile.set_prop(who, "regen.hits", 2)` lo cura cinque volte più velocemente del
valore predefinito; `nil` gli restituisce il ritmo configurato. Le props sono `regen.hits`, `regen.mana` e
`regen.stamina`.

Muoversi sottrae stamina a un giocatore (vedi `fatigue_enabled` in
[`ultima.regeneration`](../server-configuration.md)): correndo, e a ogni passo quando trasporta più di
`mobile.max_weight`. Uno script che dà o toglie oggetti cambia subito ciò che il mobile trasporta; la
barra di stato del giocatore segue al prossimo aggiornamento dello stato.

## common/teleport.lua

I due script di teletrasporto sotto condividono `scripts/common/teleport.lua`, un modulo Lua che prendono con
`local teleport = require("common.teleport")`: `teleport.send(serial, who)` manda un mobile dove indicano le
props dell'oggetto (`teleport.x`, `teleport.y`, `teleport.z`, `teleport.map`), con il fumo di
`source_effect` e `dest_effect` e il suono di `sound_id`, e `teleport.is_on(value)` legge un flag
che i file di decorazione contengono come testo. Un tuo script che teletrasporta può prenderlo allo stesso modo.
Uno script conserva il modulo acquisito: dopo `script reload common/teleport.lua`, ricarica anche gli script che
lo usano (vedi [Ricaricamento e appartenenza](runtime.md#reload-and-ownership)). `mgctl init` aggiunge `scripts/common/` a una
root esistente e conserva gli script già presenti; una root i cui script degli oggetti vengono sostituiti manualmente richiede
anche `scripts/common/`, altrimenti i suoi teletrasporti smettono di funzionare.

## common/numbers.lua

`scripts/common/numbers.lua` è il modo in cui gli script forniti scrivono un numero letto dal giocatore:
`numbers.with_thousands(1234567)` restituisce `"1,234,567"`. Il banchiere e l'assegno bancario lo prendono con
`local numbers = require("common.numbers")`, e un tuo script può fare lo stesso. Una root i cui
script sono sostituiti manualmente ne ha bisogno, altrimenti banchieri e assegni bancari smettono di funzionare.

## teleporter.lua

`scripts/items/teleporter.lua` è lo script del template `decoration_teleporter` che
[`.decorate`](../commands/decorate.md) assegna al `Teleporter` di ModernUO: su `on_move_over`
teletrasporta il giocatore alle props `teleport.x`, `teleport.y` e `teleport.z` con
`mobile.teleport`, mostra uno sbuffo di fumo dove il giocatore è partito (prop `source_effect`) e
arrivato (prop `dest_effect`), poi vi riproduce la prop `sound_id` quando il teletrasporto ne ha una. La prop
`active = false` disattiva un teletrasporto. Viaggiano solo i giocatori, a meno che la prop `creatures` sia true: allora viaggia anche un NPC che vi cammina sopra. Un teletrasporto con la prop `teleport.map`, un numero `MapType`,
porta il giocatore su quella mappa: il client cambia mappa, poi riceve la stagione quando differisce
da quella mostrata, la luce, il meteo e la musica del luogo; quando la mappa non è caricata non succede nulla. Il template ha `visibility = "game_master"`: un oggetto a terra viene inviato solo agli
account consentiti dalla sua visibilità, quindi i giocatori camminano su un teletrasporto che non vedono mai.

## keyword_teleport.lua

`scripts/items/keyword_teleport.lua` è lo script del template `decoration_keyword_teleporter`
che `.decorate` assegna al `KeywordTeleporter` di ModernUO, come il mantra di un
santuario: su `on_speech` teletrasporta il giocatore che dice la prop `substring` (trovata ovunque nel
testo, senza distinzione di maiuscole) o il cui client invia la parola chiave del parlato della prop `keyword`, trovandosi
entro `range` caselle (0, il valore predefinito, è la casella stessa del teletrasporto). Con un `delay`
(`"0:0:1"` o un numero di secondi) il teletrasporto avviene più tardi, se il giocatore è ancora
entro la portata. Destinazione, fumo, suono e `active` sono quelli del teletrasporto semplice.

## public_moongate.lua

`scripts/items/public_moongate.lua` è lo script del template `decoration_public_moongate`
che `.decorate` posiziona su ogni destinazione di [`moongates.toml`](../data-files/moongates.md), come
il `PublicMoongate` di ModernUO: su `on_move_over`, e su `on_use` dalla casella accanto, costruisce un
gump con `gump.create`, una pagina per mappa di `moongates.facets()` e un pulsante per città, prima la
pagina della mappa del giocatore, e riproduce il suono `0x20E`. Un pulsante teletrasporta il giocatore
con `mobile.teleport`, anche su un'altra mappa, e vi riproduce `0x1FE`. Un giocatore che si è allontanato più
di una casella mentre il gump era aperto viene avvisato e resta; scegliere la città del portale
stesso non fa nulla.

## healer.lua

`scripts/mobiles/healer.lua` è lo script dei guaritori (`script_id = "healer"`): a ogni think chiede a
`npc.ghosts_in_sight(serial, 4)` i fantasmi entro 4 celle nella sua linea di vista. Un fantasma che non c'era
al think precedente viene guardato (`npc.look_at`), riceve il suono `0x1F2` e le scintille `SparkleHeal`, e
il gump `resurrect` con l'argomento `healer`, come il `BaseHealer` di ModernUO. Un guaritore aspetta 2 secondi (4 think)
tra due offerte, e un fantasma incontrato durante l'attesa riceve l'offerta quando finisce. Un criminale viene rifiutato con il
testo del client 501222, e un assassino (rosso) con 501223, e un giocatore con karma negativo si sente dire 501224 e riceve comunque l'offerta. Un guaritore malvagio, il cui id di template inizia con `evil` (`evilhealer`, `evilwhealer`), non rifiuta nessuno e non dice nulla. Un guaritore di un
template che finisce con `whealer`, uno errante, fa un passo con `npc.wander` a ogni quarto think. Un guaritore con un negozio vende e compra come un venditore, tramite `scripts/common/shop.lua`: bende, pozioni, ginseng e aglio.

## ankh.lua e resurrect.lua

`scripts/items/ankh.lua` è lo script del template `decoration_ankh`, i due pezzi di ogni
`AnkhWest` e `AnkhNorth` che `.decorate` posiziona. Non ha `on_use`: i vivi non hanno nulla da fare
con un ankh. La sua `on_ghost_use` viene eseguita quando un giocatore morto lo usa con un doppio clic ([Morte e
resurrezione](../death.md#death-of-a-player)): da più di 2 celle dice "È troppo lontano."
(testo del client 500446), altrimenti apre il gump `resurrect` (`templates/gumps/resurrect.xml`). Il suo
pulsante Continue chiama `resurrect.accept` in `scripts/gumps/resurrect.lua`, che, se il giocatore è
ancora morto ed entro 2 celle dall'ankh (8 dal guaritore, se ha offerto un [guaritore](#healerlua)),
chiama `mobile.resurrect`, riproduce il suono `0x214` e l'effetto `SparkleHeal` sul giocatore e gli toglie un
decimo della fama; da cinque omicidi a breve termine toglie anche abilità e statistiche ([Conteggi degli omicidi](../death.md#murder-counts)).
Cancel non fa nulla.

## moongate.lua

`scripts/items/moongate.lua` è lo script del template `moongate`, il portale con una
destinazione che il comando [`moongate`](../commands/moongate.md) posiziona ai piedi di un game master, come
il `Moongate` di ModernUO. Su `on_move_over`, e su `on_use` dalla casella accanto, attende un secondo
con `timer.after`, poi porta il giocatore, se è ancora lì, alle props `teleport.x`,
`teleport.y` e `teleport.z`, sulla mappa della prop `teleport.map` (un numero `MapType` o il suo
nome; la mappa del giocatore in sua assenza), e riproduce `0x1FE`. Un portale senza i tre
numeri, con una mappa inesistente o non caricata, o con un punto fuori dalla mappa dice al
giocatore "Questo moongate non sembra portare da nessuna parte." (messaggio 30114). Toccare nuovamente il portale
durante quel secondo non avvia nulla. Quando il portale
si trova in una regione sorvegliata e la destinazione no (`world.is_guarded`), chiede prima conferma: un
gump con OKAY e CANCEL e il suono `0x20E`; OKAY da più di una casella di distanza dice "È
troppo lontano." (messaggio 393) con `mobile.message`. Le regole ModernUO su sigilli, giovani
giocatori, assassini, lancio di incantesimi, animali e dissoluzione del portale non sono ancora presenti.

## bulletin_board.lua

`scripts/items/bulletin_board.lua` è lo script delle [bacheche](../bulletin-boards.md)
(il template di oggetto `bulletin_board` posizionato da `.decorate`, e `0x1e5e_bulletin_board` e
`0x1e5f_bulletin_board` per una bacheca aggiunta manualmente). Il suo `on_use` chiama `board.open(serial, user)`
e restituisce `true`: il client del giocatore riceve la bacheca e l'elenco dei messaggi, e da
lì legge, pubblica, risponde e rimuove i propri. Qualsiasi template di oggetto con questo script è una bacheca,
ciascun oggetto con i propri messaggi.

## clock.lua

`scripts/items/clock.lua` è lo script degli orologi (template degli oggetti `0x104b_clock` e
`0x104c_clock`, e `decoration_clock` per quelli posizionati da `.decorate`), come il `Clock` di ModernUO: su
`on_use` il giocatore legge sopra l'orologio la parte del giorno ("È pomeriggio") e l'ora al
minuto ("1:07 per l'esattezza") del luogo in cui si trova, da `world.time`, come testi del client inviati
con `item.message_cliloc`.

## fillable.lua

`scripts/items/fillable.lua` è lo script del template `decoration_fillable` che `.decorate`
assegna ai contenitori cittadini, il `FillableContainer` di ModernUO: casse, scatole, forzieri e barili
dei negozi e librerie delle biblioteche. Su `on_use`, prima dell'apertura del contenitore, un
contenitore il cui tempo è arrivato (prop `fill.next`, secondo il conteggio di `world.now()`) e che contiene due cose
o meno, una pila contando per la propria quantità, riceve fino al doppio di ciò che manca per arrivare a tre, ciascuna
un'estrazione della tabella del bottino del proprio tipo con `item.add_loot`; una libreria si riempie fino a cinque libri. Poi attende da 60 a 90 minuti; un riempimento che non ha potuto aggiungere nulla viene riprovato all'apertura successiva.
Non viene eseguito nulla mentre nessuno apre il contenitore, e i tempi sopravvivono al riavvio. Il tipo è la
prop `content_type`, come `baker` per la tabella `fillable_baker` di
`templates/loots/fillable_containers.toml`; in sua assenza il contenitore assume il tipo del venditore più vicino
entro 20 caselle, indicato da `mobile.template`, e lo conserva. Senza venditori intorno resta
vuoto e cerca di nuovo cinque minuti dopo. ModernUO inizia l'attesa quando viene estratto un oggetto e
chiude a chiave e mette trappole al contenitore: questi aspetti non sono ancora presenti. Le tabelle cittadine usano
`templates/loots/randomshields.toml` (uno scudo semplice, `Loot.ShieldTypes` di ModernUO) e i due prodotti di
`templates/items/town_goods.toml` (mazzuolo e scalpello, aste per frecce) mancanti nei file di oggetti convertiti.

## gmtools.lua

`scripts/gumps/gmtools.lua` è lo script del gump degli strumenti del game master
(`templates/gumps/gmtools.xml`), aperto da [`.gmtools`](../commands/gmtools.md). Il gump ha due
slot, riempiti da due funzioni: `tools` disegna la barra laterale, un pulsante per ogni voce della tabella
`tools` nello script, e `panel` disegna il pannello di quello selezionato (`args.tool`, il primo quando
non viene fornito o è sconosciuto). Un clic sulla barra laterale riapre il gump su quello strumento.

Ci sono tre strumenti, meteo, stagione e ora. Il pannello meteo legge `world.weather_profile` e `world.weather` e
ha un pulsante per ciascun tipo, `none`, `rain`, `snow` e `storm`, che chiama `world.set_weather` sul
giocatore, gli dice `The weather of temperate is now storm until the next hour.` e riapre il gump.
Il pannello stagione legge `world.season_here` e la stagione della mappa del giocatore
(`world.season` di `mobile.location(player).map`) e ha un pulsante per ogni stagione e uno per
`auto`, che chiamano `world.set_season` o `world.clear_season`, dicono al giocatore `The season of your map
is now winter.` e riaprono il gump. Il pannello ora legge `world.time`, `world.moon`,
`world.light_here` e `world.global_light` e ha un pulsante per ciascuno di quattro livelli di luce e uno per
`auto`, che chiamano `world.set_global_light` o `world.clear_global_light` e dicono al giocatore `The
global light is now 26.`. Solo staff: gli slot sono vuoti per chiunque altro, e ogni
pulsante ricontrolla `world.is_staff`.

Per aggiungere uno strumento, scrivi una funzione pannello con firma `function(g, player)` e aggiungi
`{ id = "...", title = "...", panel = ... }` a `tools`.

## help_menu.lua

`scripts/gumps/help_menu.lua` è lo script del menu di [aiuto](../help.md)
(`templates/gumps/help_menu.xml`), che si apre con il pulsante Help del paperdoll. `stuck` è il pulsante
«Sono bloccato»: rifiuta un personaggio in prigione (`jail.sentence`) o in combattimento (`combat.target`),
uno che sta già aspettando e uno la cui pausa (la proprietà `help.stuck_until`, secondi dal 1970 da
`world.now`) non è finita, escluso lo staff; prende la città di partenza più vicina da `help.nearest_city`,
dice l'attesa da `help.settings` e, dopo `timer.after` di quei secondi, sposta il personaggio con
`mobile.teleport` se è dove era e se ha ancora il permesso. `commands` esegue `help` con
`commands.execute_as`, `rules` dice il messaggio 30200 e `call` apre `help_page_kind`.

## help_page_kind.lua

`scripts/gumps/help_page_kind.lua` è lo script del secondo passo di *Call a game master*
(`templates/gumps/help_page_kind.xml`). `question`, `bug`, `suggestion` e `harassment` interrogano `help.can_page`;
a un giocatore che non può viene detto perché (messaggi 30213 e 30214) prima che scriva qualcosa. Altrimenti
gli viene detto di scrivere una riga (30211), `prompt.ask` la aspetta e `help.create_page` manda la richiesta
con l'`HelpPageKindType` del pulsante; Esc o una riga vuota dice 30215, un rifiuto alla fine ripete il suo
motivo e una richiesta inviata dice 30212. Può chiamare anche un giocatore in prigione.

## pages.lua

`scripts/gumps/pages.lua` è lo script della coda dello staff (`templates/gumps/pages.xml`, aperta da
[`.pages`](../commands/pages.md)). `rows` riempie lo slot con `help.pages()`, dalla più vecchia, dieci per pagina,
un pulsante e una riga per richiesta (`#1 Gino, Bug, 3 min, open`); una riga apre `pages_detail` su quella
richiesta. Solo per lo staff: lo slot resta vuoto per chiunque altro, e ogni pulsante ricontrolla `world.is_staff`.

## pages_detail.lua

`scripts/gumps/pages_detail.lua` è lo script di una richiesta (`templates/gumps/pages_detail.xml`). `go` porta il
game master dal giocatore (`mobile.location`) o, se è offline, dove ha chiesto; `take` chiama `help.take`;
`answer` prende il testo del campo (`response.text[1]`), rifiuta un campo vuoto e altrimenti chiama
`help.answer` e torna alla coda; `close` chiama `help.close`. Una richiesta chiusa nel frattempo lo dice e non
cambia nulla.

## common/help_pages.lua

`scripts/common/help_pages.lua` è il modulo Lua che i due gump dello staff condividono, preso con
`require("common.help_pages")`: le parole per un genere, una mappa, un'età e uno stato, la riga di una richiesta
e gli argomenti del suo gump di dettaglio.

## jail_sentence.lua

`scripts/gumps/jail_sentence.lua` è lo script del gump della [prigione](../jail.md)
(`templates/gumps/jail_sentence.xml`), aperto da [`.jail`](../commands/jail.md) senza bersaglio,
e da `.jail <name>` sul giocatore con quel nome.
La sua funzione `rows` riempie lo slot: prima un pulsante che fornisce il cursore con `target.pick` e riapre
il gump sul personaggio selezionato, poi le celle di `jail.cells()`, dieci per pagina. Con un
personaggio selezionato, una cella libera ha un pulsante che legge i giorni digitati nel gump e chiama
`jail.send(target, cell, days, who, reason)`, il motivo è ciò che viene digitato nel secondo campo; giorni vuoti, non numerici, frazionari o oltre
`jail.max_days()` non incarcerano nessuno, e il gump si riapre con il motivo. Una cella occupata
mostra il nome, il tempo rimanente come `2d 4h`, `5h 10m` o `12m`, e un pulsante che chiama
`jail.release`. Ogni cella ha un pulsante che vi porta il game master con
`mobile.teleport(who, cell.x, cell.y, cell.z, cell.map)`, sulla mappa della prigione. Un bersaglio già
in prigione ha il proprio rilascio su una riga separata in alto.
Un bersaglio fuori dal mondo, un giocatore trovato da `.jail <name>`, viene mostrato come `(offline)`:
`mobile.name` è nil per lui. `jail.send` risponde allora `JailResultType.Pending` e il game master
viene informato che la cella è riservata fino al login; una cella così mostra `waits for login`, dal `pending`
di `jail.cells()`. Quando il gump viene aperto con `candidates`, più giocatori con lo stesso nome, la
funzione li elenca, al massimo dieci, con account e stato online, ciascuno con un
pulsante che apre il gump su di lui, e omette le celle finché non ne viene scelto uno.
Ogni pulsante ricontrolla `world.is_staff`: il grado potrebbe essere stato rimosso mentre il gump era aperto.

## jail_note.lua

`scripts/items/jail_note.lua` è lo script del template `jail_release_note`, la nota che un
prigioniero trova nello zaino alla fine della pena. Su `on_use` delega a
`book.open`, mostrando `book.content` salvato o il vecchio `jail.text`: giorni scontati, cella, date
e multa pagata. La pergamena condivisa esegue l'escape del testo semplice e scorre i testi lunghi. Una nota senza
testo, come una creata con `.add`, non mostra nulla.

## readable_scroll.lua

`scripts/items/readable_scroll.lua` delega il doppio clic a `book.open`. Il template non impilabile
`readable_scroll` è usato dal [catalogo dei testi](../data-files/books.md).
Crea una lettera personalizzata con `book.give(player, "welcome_letter", { contact_name = "Vega" })`.
Titolo, autore e corpo salvati restano fissi quando un altro giocatore la legge.

## readable_book.lua

`scripts/items/readable_book.lua` delega il doppio clic a `book.open` come la pergamena; per un
oggetto del template `readable_book`, `book.open` invia il libro del client, la copertina e ogni pagina,
invece della pergamena ([libri e pergamene](../data-files/books.md#books-and-parchments)).
I testi importati da ModernUO lo usano: `book.give(player, "grammar_of_orcish")`.
