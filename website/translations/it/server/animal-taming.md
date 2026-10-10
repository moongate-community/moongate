<!-- translation: {"sourceHash":"1bff52eacae7d6eb7a2da7f30b7962d7939726143a985ac0f04eac9f94ebf1b7","title":"Addomesticare animali"} -->

# Addomesticare animali

Un giocatore con la skill Animal Taming addomestica una creatura selvatica e questa diventa sua: lo segue, obbedisce a
ciò che dice, e il giocatore può cavalcarla o lasciarla in una [stalla](mounts.md#the-stable).

## Come addomesticare

Usare di nuovo la skill mentre addomestichi dice che devi aspettare; un errore in un momento dello script termina
l'addomesticamento, viene registrato nel log, e non blocca nessuno.

1. Usa la skill (la lista delle skill, poi *Use*, oppure il pulsante *Animal Taming* del client). Leggi
   "Tame which animal?" e ottieni un cursore.
2. Scegli una creatura entro 3 tile. Se può essere addomesticata, leggi "You start to tame the creature."
3. Resta entro 7 tile da essa, in vista, per 9 o 12 secondi. Ogni 3 secondi le dici una parola gentile.
4. All'ultimo momento si tira la skill. In caso di successo "It seems to accept you as master." e la creatura è tua; in
   caso di fallimento "You fail to tame the creature." e puoi riprovare subito.

La probabilità è la skill rispetto a ciò che la creatura richiede: nulla a 0,1 punti sotto, una certezza a 49,9 punti
sopra, e nel mezzo cresce in modo lineare. La skill può salire come qualsiasi altra. Le creature che richiedono più
della skill vengono rifiutate alla scelta: "You have no chance of taming this creature."

| Leggi | Perché |
| --- | --- |
| `You can't tame that!` | Ciò che hai scelto non è una creatura: un oggetto, il terreno, o qualcuno sparito |
| `That being cannot be tamed.` | Un giocatore |
| `That creature cannot be tamed.` | La creatura non ha una voce in [`taming.toml`](data-files/taming.md) |
| `That creature looks tame already.` | Ha un proprietario, alla scelta o mentre la addomestichi |
| `You have too many followers to tame that creature.` | I suoi slot non entrano nei tuoi follower |
| `You have no chance of taming this creature.` | Il tuo Animal Taming è sotto ciò che richiede |
| `That is too far away.` | La creatura è a più di 3 tile alla scelta |
| `Someone else is already taming this creature.` | Un altro giocatore la sta addomesticando |
| `You are too far away to continue taming.` | Ti sei allontanato più di 7 tile |
| `You are dead and cannot continue taming.` | Sei morto, anche mentre il cursore era attivo |
| `You can no longer see the creature.` | C'è qualcosa in mezzo |
| `The animal is too angry to continue taming.` | È stata ferita dall'ultima volta, o sta combattendo |

Una creatura addomesticata lascia la sua spawn region, che ne porta un'altra al suo posto, e non va più dietro a nessuno,
qualunque cosa cacciasse prima, e le guardie di una città la lasciano stare: un drago addomesticato non è un mostro per
loro. Si difende comunque quando viene colpita.

## Cosa fa un pet

Una creatura addomesticata segue il suo proprietario: cammina quando è a più di 2 tile di distanza e corre da 7, fa tre
passi per think quando corre per stargli dietro, e quando non riesce ad arrivare in dieci passi, o resta indietro di più
di 16 tile, viene spostata su un tile libero accanto al proprietario, mai dietro un muro (se non ce n'è resta dov'è). A
più di 24 tile, su un'altra mappa, o con il proprietario non nel mondo, resta dove si trova. Una creatura che dorme
perché nessun giocatore è vicino non segue: un pet lasciato molto indietro si ritrova dove stava. Un pet colpito si
difende.

Lo stesso vale per una creatura data da un game master con [`tame`](commands/tame.md), una tolta da una stalla e una che
è stata cavalcata: seguono per impostazione predefinita. L'ordine di un pet resta con lui, tranne in quei tre casi, che
lo dimenticano.

## Cosa puoi dirgli

Pronuncia le parole entro 14 tile, da proprietario e vivo. Con **all** davanti valgono per ogni tuo pet vicino; con il
nome del pet per primo (`a horse stay`) valgono solo per quel pet.

| Dici | Il pet |
| --- | --- |
| `come`, `all come` | Viene da te e resta accanto a te |
| `follow`, `follow me`, `all follow`, `all follow me` | Ti segue |
| `stay`, `all stay` | Resta dov'è |
| `stop`, `all stop` | Smette ciò che sta facendo, anche il combattimento, e resta fermo |
| `guard`, `all guard`, `all guard me` | Resta vicino a te e combatte chi combatte contro di te o contro di lui |
| `kill`, `attack`, `all kill`, `all attack` | Chiede un bersaglio; i pet lo combattono, poi tornano a ciò che facevano |
| `release` (con il suo nome) | Chiede se sei sicuro; con il sì lo lascia andare: torna selvatico, appartiene di nuovo alla sua spawn region, e hai un follower in meno |

Non combatterà te, un altro tuo pet, o ciò che è morto. Mandare i tuoi pet contro un innocente ti rende criminale, come
se lo avessi colpito tu stesso, a meno che quello non stia combattendo contro di te. Un pet che fugge per natura, come un
coniglio, combatte quando lo mandi, e quando viene colpito. Friend, transfer, drop e patrol non sono implementati.

## Lealtà, cibo e obbedienza

Un pet è leale verso di te da 0 a 100, e parte da 100. Il tempo la consuma: ogni ora
(`[ultima.pets] loyalty_drain_minutes`) ogni pet nel mondo perde 10 (`loyalty_drain`), finché anche tu sei nel mondo: un
pet il cui proprietario è assente mantiene ciò che ha. Sotto 10 si guarda intorno disperato; a 0 ha deciso che sta meglio
senza un padrone: torna selvatico, come se lo avessi lasciato andare. Un pet in una stalla o sotto di te non è nel mondo,
quindi non perde nulla; mantiene lì la sua lealtà e torna con essa.

Dagli da mangiare: trascina il cibo su di lui, da due tile o meno. Mangia l'intera pila quando la sua creatura mangia
quel tipo di cibo, e guadagna 10 di lealtà per ogni oggetto (`food_gain`), fino a 100. Un cavallo mangia frutta, verdura e
pane; un cane o un gatto, carne e pesce. Il cibo che non mangia viene restituito, e il pet si ritrae. I tipi che ogni
creatura mangia sono il `food` di [`taming.toml`](data-files/taming.md), gli oggetti di ogni tipo sono in
[`pet_food.toml`](data-files/pet-food.md).

Gli ordini possono essere rifiutati. Ogni ordine tranne *release* tira una probabilità, dal tuo Animal Taming e Animal
Lore rispetto alla skill che la creatura richiede, meno un centesimo per ogni punto di lealtà che le manca (la regola di
ModernUO): una creatura che richiede 29,1 o meno, o un game master, viene sempre obbedita. Con la creatura che richiede
70, Taming 100 e nessun Animal Lore la probabilità è il 22%; con entrambi a 120 è il 99% a piena lealtà. Un pet che
obbedisce guadagna 1 di lealtà (`obey_gain`), anche quando non avrebbe potuto rifiutare; uno che non obbedisce ringhia,
ne perde 3 (`disobey_loss`) e non fa ciò che hai detto; se era la sua ultima lealtà diventa selvatico. Un ordine kill
tira una volta scelto il bersaglio.

## Follower

Un giocatore può avere 5 follower (`[ultima.pets] max_followers`, da 1 a 50). Ogni creatura conta per i suoi slot,
di solito 1: le tue creature che sono nel mondo, e quella che cavalchi. Una creatura in una stalla non conta nulla. Una
tua creatura che un game master cavalca conta per te, non per chi la cavalca. Il conteggio è nella finestra di stato del
tuo personaggio (`followers 2/5`), mostrato di nuovo quando addomestichi, metti in stalla, ritiri, monti o smonti, e
quando una delle tue creature muore. La stalla non guarda il limite quando ritiri un pet, quindi un giocatore può avere
più del limite mettendo in stalla e ritirando.

## Cosa si può addomesticare

Le creature di [`data/taming.toml`](data-files/taming.md): circa 75, con la skill che ognuna richiede e gli slot che
ognuna conta. Un game master può comunque dare qualsiasi creatura che si possa cavalcare con [`tame`](commands/tame.md),
e ignora il limite.

## Legame e rianimazione di un pet

Un pet si lega al suo proprietario attraverso il cibo. Il primo cibo che il proprietario gli dà e che lui mangia avvia il
conteggio; il successivo, dopo una settimana (`[ultima.pets] bonding_days`, 7), lo lega: "Your pet has bonded with you!".
Il proprietario ha bisogno dell'Animal Taming che la creatura richiede, a meno che non richieda 29,1 o meno. Lasciare
andare il pet, o addomesticarlo di nuovo, spezza il legame. Un pet legato resta legato attraverso la stalla e sotto un
cavaliere; il conteggio di un legame non terminato ricomincia dopo di loro. Il `tame` del game master dà un pet al nuovo
proprietario senza il legame. [Animal Lore](#animal-lore) mostra *(bonded)* o *(tame)* accanto al suo nome.

Un pet legato che muore lascia un cadavere che ricorda il proprietario, la lealtà e il legame; un pet che non era legato
muore per sempre (lo staff può comunque rianimarne il cadavere con `resurrect`, che riporta una creatura selvatica). Usa
una benda pulita sul cadavere: il guaritore ha bisogno di 80 punti di Veterinary e di Animal Lore, e riesce con una
probabilità di (Veterinary - 68) / 50; il proprietario è il guaritore o sta entro 3 tile dal cadavere ("The pet's owner
must be nearby"). L'attesa è quella di un fantasma. Il pet rinasce dove giace il cadavere con 10 punti ferita, il suo
proprietario, la sua lealtà e il suo legame, come una nuova creatura; se i suoi slot non entrano più nei follower del
proprietario non può essere rianimato. Gli altri modi di rianimare un pet (un incantesimo, un ankh, i guaritori) non sono
implementati.

## Animal Lore

Usa la skill Animal Lore, scegli una creatura entro 8 tile e, se il controllo della skill (da 0 a 120) riesce, un gump di
due pagine dice cos'è. La pagina mostra le stesse cose qualunque sia la tua skill; la skill decide quali creature puoi
guardare: una addomesticata sempre, una che può essere addomesticata da 100 punti, qualsiasi altro animale o mostro da
110. Una creatura a più di 8 tile o fuori vista viene rifiutata per prima ("That is too far away", "You can no longer see
the creature"). Se fallisci leggi che non ti viene in mente nulla di ciò che sai a memoria.

- Pagina 1: punti ferita, stamina e mana, forza, destrezza e intelligenza, armor rating, danno, e il livello di lealtà,
  da *wild* (una creatura senza proprietario, o senza lealtà) a *wonderfully happy*.
- Pagina 2: wrestling, tactics, magic resistance e anatomy, magery, evaluating intelligence e meditation (`---` sotto
  10 punti), i tipi di cibo che mangia e l'Animal Taming che richiede.

I testi sono quelli del client. Ogni volta che provi ad addomesticare una creatura, Animal Lore ha un tentativo tutto
suo, quindi cresce mentre addomestichi. `pet.lore` dà anche gli slot della creatura, che il gump non mostra. Resistenze,
danno elementale, rigenerazione, barding, istinti di branco e legame non vengono mostrati.

## Per gli script

Il modulo Lua `pet`: `pet.info(creature)`, `pet.followers(player)`, `pet.max_followers()`, `pet.tame(player,
creature)`, `pet.loyalty(creature)`, `pet.control_chance(player, creature)`, `pet.obey(player, creature)` e `pet.feed(player, creature,
item)` e `pet.lore(creature)` (armor, danno, cibi, lealtà, legame e i dati di taming in una tabella) e `pet.corpse(corpse)` (il proprietario che il cadavere di un
pet legato ricorda, e se entra nei suoi follower); gli script delle skill sono
`scripts/skills/animal_taming.lua` e `scripts/skills/animal_lore.lua`.

## Non ancora implementato

Rianimare un pet con un incantesimo o un ankh; gli amici di un pet; il conteggio del legame mantenuto attraverso la
stalla; oro, metallo e cuoio come cibo; friend, transfer, drop e patrol; portare i pet con sé quando il proprietario
viaggia con un gate o un incantesimo; e i pet di un giocatore offline restano dove si trovavano.
