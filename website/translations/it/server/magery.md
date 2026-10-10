<!-- translation: {"sourceHash":"b1f432fb8816da6a2a090d329e95f52ec5ad31ff6f7a423fb60530a927524b2a","title":"Magery"} -->

# Magery

Un mago lancia gli incantesimi di Magery da un libro degli incantesimi o li legge da una pergamena, con le regole del gioco
classico: parole di potere, un ritardo in cui il lanciatore sta fermo, un cursore di mira, reagenti, mana e una prova di
abilità. Comprende il libro, il motore di lancio, gli otto cerchi (salvo gli incantesimi di lucchetti e trappole) e Reactive Armor.

## Il libro degli incantesimi

- Un libro contiene fino a 64 incantesimi, numerati come li numera il client: otto cerchi da otto.
- Un doppio clic lo apre, in mano o nello zaino (non in una borsa al suo interno). Gli incantesimi che contiene compaiono
  nella finestra del client; un clic su uno lo lancia.
- Una pergamena lasciata su un libro portato dal suo proprietario scrive l'incantesimo nel libro, con un suono. Una
  pergamena di una pila viene consumata. Un incantesimo che il libro contiene già viene rifiutato ("That spell is already
  present in that spellbook.") e la pergamena torna indietro.
- Gli incantesimi di un libro sono un numero conservato nell'oggetto, `spellbook.spells`; un libro che non lo ha contiene ciò
  che dice il tag `spells` del suo template. `spellbook` è vuoto, da `spellbook1` a `spellbook1to8` contengono dal primo
  cerchio ai primi otto, e `spellbook_full` contiene tutti i 64. Lo staff ne dà uno con `.add spellbook_full`.

## Il lancio

Il lancio parte dall'icona dell'incantesimo del client, da una macro, dalla finestra del libro o da un doppio clic su una
pergamena (i tipi 0x27 e 0x56 del comando di testo e il comando esteso 0x1C del client).

1. Viene rifiutato, con il testo classico, quando il lanciatore è morto, sta già lanciando, è bloccato, non si è ancora
   ripreso dal lancio precedente o non ha il mana del cerchio, oppure quando nessun libro che indossa o porta contiene
   l'incantesimo.
2. Il lanciatore dice le parole di potere sopra la testa, fa il gesto dell'incantesimo (non in sella) e non può muoversi per
   il ritardo del cerchio: 0,5 secondi per il primo, un quarto di secondo in più per ciascuno dei successivi.
3. Quando il ritardo finisce arriva il cursore di mira (12 caselle, in linea di vista), oppure l'incantesimo ha effetto
   subito se non ne chiede. Mettere via il cursore termina il lancio e non costa nulla. Dopo il ritardo il lanciatore
   aspetta 0,75 secondi prima di un altro lancio.
4. Sul bersaglio il lancio prende i reagenti dallo zaino (una pergamena porta i suoi), controlla di nuovo il mana e prova
   Evaluating Intelligence, che può crescere a ogni prova, e Magery. Un successo paga il mana, consuma una pergamena ed
   esegue l'incantesimo. Un fallimento fallisce: i reagenti si perdono, il mana no, una pergamena resta.
5. Il danno subito mentre il ritardo corre, un colpo o un tick di veleno, rovina un incantesimo sopra il primo cerchio: il
   lanciatore viene avvisato e aspetta tanto più a lungo quanto meno del ritardo era trascorso, da un secondo fino a un
   quinto di secondo. Il primo cerchio non viene mai rovinato, e nemmeno un lancio che aspetta il suo bersaglio. Un
   lanciatore che muore, di qualunque cosa, non lancia più.

| Cerchio | Mana | Ritardo | Finestra di Magery di un libro | Finestra di Magery di una pergamena |
| --- | --- | --- | --- | --- |
| 1 | 4 | 0,5 s | da 0 a 40 | da -50 a -10 |
| 2 | 6 | 0,75 s | da 10 a 50 | da -30 a 10 |
| 3 | 9 | 1,0 s | da 20 a 60 | da 0 a 40 |
| 4 | 11 | 1,25 s | da 30 a 70 | da 10 a 50 |
| 5 | 14 | 1,5 s | da 40 a 80 | da 20 a 60 |
| 6 | 20 | 1,75 s | da 50 a 90 | da 30 a 70 |
| 7 | 40 | 2,0 s | da 60 a 100 | da 40 a 80 |
| 8 | 50 | 2,25 s | da 70 a 110 | da 50 a 90 |

## Il primo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Clumsy | blood moss, nightshade | Abbassa la destrezza del bersaglio di 1 più un decimo della Magery del lanciatore, per 1,2 secondi a punto |
| Create Food | aglio, ginseng, radice di mandragora | Un cibo a caso nello zaino, o ai piedi quando è pieno |
| Feeblemind | ginseng, nightshade | Come Clumsy, sull'intelligenza (e sul massimo di mana di un giocatore) |
| Heal | aglio, ginseng, seta di ragno | Un decimo della Magery e da 1 a 5 punti ferita; rifiutato per un bersaglio avvelenato, morto o con tutti i punti ferita |
| Magic Arrow | cenere sulfurea | Dopo mezzo secondo, da 4 a 7 danni da fuoco, tre quarti se resistito, scalati da Evaluating Intelligence contro Resisting Spells e dalla Magery, raddoppiati contro un mostro o un animale |
| Night Sight | cenere sulfurea, seta di ragno | Vede al buio per 15-39 minuti, con la luminosità che dice la Magery (26 a 100) |
| Weaken | aglio, nightshade | Come Clumsy, sulla forza: i punti ferita massimi di un giocatore calano di conseguenza |
| Reactive Armor | aglio, cenere sulfurea, seta di ragno | Per 25 secondi e mezzo secondo a punto di Magery del lanciatore, una parte di ogni colpo in mischia che arriva da distanza di un braccio torna a chi lo ha dato: il 10 per cento e un quarto di per cento a punto della Magery di chi la indossa quando viene colpito (35 a 100). Una freccia non torna indietro, una guardia non ne è mai ferita, e un attaccante che cade per questo termina il colpo |

Un incantesimo dannoso rende il lanciatore l'aggressore del bersaglio: un criminale contro un innocente che non combatte, e
un PNG reagisce. Una maledizione su una statistica che è forte uguale o di più resta. Un incantesimo dannoso su un bersaglio
che non può essere colpito, come un venditore o un banchiere, viene rifiutato con "You cannot perform negative acts on your
target." prima che reagenti e mana siano spesi.

Un detenuto in [prigione](jail.md) non lancia alcun incantesimo: legge "You cannot cast spells here.", quindi non può fare
Recall o Teleport per uscire. Lo staff non ne è mai vincolato.

## Il secondo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Agility | blood moss, radice di mandragora | Alza la destrezza di 1 più un decimo della Magery del lanciatore, per 1,2 secondi a punto |
| Cunning | radice di mandragora, nightshade | Come Agility, sull'intelligenza |
| Strength | radice di mandragora, nightshade | Come Agility, sulla forza |
| Cure | aglio, ginseng | Può far finire un veleno: la probabilità è (10000 + 75 a punto di Magery - 1750 per ogni livello del veleno, il minore essendo 1) / 100 per cento. Una cura riuscita avvisa il bersaglio e il lanciatore, una fallita il lanciatore |
| Harm | nightshade, seta di ragno | Subito, da 1 a 15 danni, tre quarti se resistito, scalati come gli altri incantesimi di danno, interi a qualunque distanza |
| Protection | aglio, ginseng, cenere sulfurea | Aggiunge un decimo dei punti di Magery del lanciatore all'armatura del bersaglio, per 1,2 secondi a punto |
| Magic Trap, Magic Untrap | | Disabilitati, vedi sotto |

Un potenziamento di una statistica che è forte uguale o di più del nuovo resta; uno più debole viene sostituito. Un
potenziamento e una maledizione della stessa statistica si sommano. Protection viene rifiutato, prima che si spenda
qualcosa, per un bersaglio che ce l'ha già. Si somma a ciò che assorbe un colpo, come l'armatura di un pezzo, ed è
l'incantesimo classico dei tempi prima che gli incantesimi difensivi cambiassero (non impedisce che un lancio venga
disturbato).

## Il terzo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Bless | aglio, radice di mandragora | Come Agility, su forza, destrezza e intelligenza insieme |
| Fireball | perla nera | Una palla di fuoco vola al bersaglio e, mezzo secondo dopo, fa da 10 a 16 danni, scalati come Magic Arrow |
| Poison | nightshade | Avvelena il bersaglio se non resiste. Il livello dipende da Magery e Poisoning insieme, meno 10 per ogni casella oltre tre: oltre 199,8 il veleno mortale una volta su dieci e altrimenti il maggiore, oltre 170,2 il maggiore, oltre 130,2 il normale e altrimenti il minore |
| Teleport | blood moss, radice di mandragora | Il lanciatore si trova nel punto scelto, in linea di vista, con uno sbuffo in entrambi i punti. Rifiutato prima che si spenda qualcosa quando il lanciatore è troppo carico per muoversi, nessuno può stare lì, vi sta un mobile o vi giace un oggetto invalicabile come una porta chiusa, o una regione vieta un teletrasporto in uscita dal suo punto o in entrata nella destinazione |
| Telekinesis | blood moss, radice di mandragora | Usa un oggetto da lontano come farebbe un doppio clic: un contenitore si apre, una porta si muove. Rifiutato per ciò che non ha un uso |
| Wall of Stone | blood moss, aglio | Tre pezzi di muro di traverso sulla via dal lanciatore al punto, che bloccano il movimento per dieci secondi; nessun pezzo dove sta un mobile, dove il lanciatore non vede o dove giace già un oggetto invalicabile. Rifiutato in una città sorvegliata |
| Magic Lock, Unlock | | Disabilitati, vedi sotto |

## Il quarto cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Arch Cure | aglio, ginseng, radice di mandragora | Cure su chiunque sia vivo entro due caselle dal punto scelto, con una probabilità un poco più bassa, l'uno per cento in meno |
| Arch Protection | aglio, ginseng, radice di mandragora, cenere sulfurea | Protection su chiunque sia vivo entro tre caselle dal punto scelto e non l'abbia già |
| Curse | aglio, nightshade, cenere sulfurea | Abbassa le tre statistiche del bersaglio insieme, come fanno Clumsy, Feeblemind e Weaken ciascuno |
| Fire Field | perla nera, cenere sulfurea, seta di ragno | Cinque pezzi di fuoco di traverso sulla via, per 20 secondi: chi calpesta un pezzo o ci sta dentro brucia per 2 danni una volta al secondo (1 quando una prova di Resisting Spells riesce); il fuoco non blocca. Nessun pezzo dove il lanciatore non vede o dove giace un oggetto invalicabile. Rifiutato in una città sorvegliata |
| Greater Heal | aglio, ginseng, radice di mandragora, seta di ragno | Quattro decimi della Magery e da 1 a 10 punti ferita, con i rifiuti di Heal |
| Lightning | radice di mandragora, cenere sulfurea | Subito, un fulmine da 12 a 20 danni, scalati come Fireball |
| Mana Drain | perla nera, radice di mandragora, seta di ragno | Toglie da 1 a 100 mana al bersaglio (al massimo quello che ha) se non resiste, cosa che fa 99 volte su cento qualunque sia la sua abilità |
| Recall | perla nera, blood moss, radice di mandragora | Il lanciatore viene portato nel punto con cui è segnata una runa, con il suo suono a entrambi i capi |

I campi sono oggetti con un tempo: Wall of Stone e Fire Field lasciano a terra oggetti `magic_wall_of_stone` e
`magic_fire_field_*`, che uno script termina allo scadere del tempo (sono conservati con il mondo e terminano anche dopo un
riavvio). Il lanciatore di un fuoco è l'aggressore di chi brucia, come per un colpo: un innocente che brucia lo rende un
criminale, un PNG reagisce, e un invulnerabile o un morto viene lasciato in pace.

### Recall e rune

Una runa di richiamo è l'oggetto `recall_rune`; segnata, contiene un punto (`rune.x`, `rune.y`, `rune.z`, `rune.map`). Lo
staff ne segna una con [`.mark_rune`](commands/mark_rune.md) nel punto in cui si trova; l'incantesimo Mark, del sesto cerchio,
lo fa per i giocatori. Recall viene rifiutato, prima che si spenda qualcosa, per ciò che non è una runa, una runa non
segnata, un criminale, un lanciatore troppo carico per muoversi, una runa di un'altra mappa, un punto in cui nulla può
stare o che un mobile o un oggetto invalicabile occupa, e una regione che non permette un richiamo in uscita dal suo punto
o in entrata nella destinazione (i flag `recall_out` e `recall_in` delle regioni).

### Regole delle regioni

`teleport_in`, `teleport_out`, `recall_in` e `recall_out` delle [regioni](data-files/regions.md) vengono lette: un viaggio
viene rifiutato quando una qualunque regione che copre il punto disattiva la regola, non solo quella che vi si applica. Gli
script lo chiedono con `world.travel_allowed`.

### Lasciati disabilitati

Magic Lock, Unlock, Magic Trap e Magic Untrap non hanno uno script, e lanciarli dice che l'incantesimo è disabilitato: gli
unici lucchetti del gioco sono quelli delle porte (letti dallo script della porta, aperti da chiavi e grimaldelli), e un
contenitore non ha né un lucchetto né una trappola su cui agire. Verranno costruiti con i lucchetti e le trappole dei
contenitori.

### Semplificato

- Protection e Arch Protection aggiungono armatura, come l'incantesimo classico; la regola successiva per cui il protetto non
  viene disturbato dal danno, e la penalità che l'accompagnava, non sono costruite.
- Recall non controlla un combattimento in corso (il gioco classico lo rifiuta per un po' dopo un colpo dato a un
  giocatore; il motore non ha ancora un simile calore di combattimento), e gli animali di un mobile non lo seguono.
- A un detenuto è rifiutato ogni incantesimo, il che ferma anche un viaggio fuori dalle celle; un portale o una
  cavalcatura non chiedono della pena.

### Quale epoca

Dove il gioco classico è cambiato con le sue espansioni, gli incantesimi seguono la più antica, i giorni senza espansioni,
come le tabelle di lancio del primo cerchio: Harm ferisce intero a qualunque distanza (il calo con la distanza è venuto con
la Second Dawn), Protection e Arch Protection aggiungono armatura, Reactive Armor rimanda una parte di un colpo in mischia
secondo la Magery di chi la indossa, e Fire Field dura 20 secondi qualunque sia la Magery. Le regole successive (una
Reactive Armor che assorbe, una Protection che protegge dal disturbo, un Fire Field più lungo) non sono costruite.

## Il quinto cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Blade Spirits | perla nera, radice di mandragora, nightshade | Viene evocato uno spirito di lama nel punto scelto: combatte per il lanciatore per 80-119 secondi, vale un seguace e non obbedisce a nessun ordine. Rifiutato in una città sorvegliata, in un punto bloccato e quando i seguaci non hanno posto per lui |
| Dispel Field | perla nera, aglio, cenere sulfurea, seta di ragno | Un pezzo di un campo degli incantesimi, o un portale di Gate Travel (e il portale all'altro capo), sparisce in uno sbuffo. Un moongate semplice è "troppo caotico"; nient'altro può essere dissolto |
| Incognito | blood moss, aglio, nightshade | Una tonalità della pelle a caso e un nome a caso del sesso del lanciatore per 1,2 secondi a punto di Magery (al massimo 144); capelli e barba restano come sono. Termina con il tempo, o con una morte |
| Magic Reflection | aglio, radice di mandragora, seta di ragno | Il lanciatore è avvolto in un riflesso che rimanda indietro, una volta, il primo incantesimo dannoso riflettibile che lo ha per bersaglio: l'incantesimo raggiunge il suo lanciatore, che resta il lanciatore ed è ferito, maledetto o immobilizzato dal proprio incantesimo, senza alcun crimine. Non ha tempo, un secondo lancio viene rifiutato e una morte lo termina |
| Mind Blast | perla nera, radice di mandragora, nightshade, cenere sulfurea | Dopo mezzo secondo, metà della differenza tra la statistica più alta e quella più bassa del bersaglio (ciascuna al massimo 150), scalata come Magic Arrow, al massimo 45; la metà se il bersaglio resiste |
| Paralyze | aglio, radice di mandragora, seta di ragno | Il bersaglio resta immobile per 7 secondi più un quinto di secondo a punto di Magery (27 a 100), tre quarti se resiste. Non può camminare, girarsi né lanciare; il suo lancio è rovinato. Un bersaglio già immobile viene avvisato e non si spende nulla |
| Poison Field | perla nera, nightshade, seta di ragno | Cinque pezzi di veleno di traverso sulla via, per 20 secondi: chi calpesta un pezzo o ci sta dentro viene avvelenato al livello normale una volta al secondo. Rifiutato in una città sorvegliata |
| Summon Creature | blood moss, radice di mandragora, seta di ragno | Un orso polare, un orso bruno, un orso nero, un cavallo, un tricheco, un pollo, uno scorpione, un serpente gigante, un lama, un alligatore, un lupo grigio, una melma, un'aquila, un gorilla, un leopardo delle nevi, un maiale, una cerva o un coniglio, scelto a caso, accanto al lanciatore, per tanti secondi quanti sono i punti di Magery. Viene rifiutato se non ci sono due posti di seguace liberi; l'animale vale poi i suoi posti, come quando è domato. Il lanciatore non può tenerlo cavalcandolo né mettendolo in una stalla |

## Il sesto cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Dispel | aglio, radice di mandragora, cenere sulfurea | Una creatura evocata viene disfatta, in uno sbuffo, con la probabilità (50 + 100 a punto di Magery sopra la sua difficoltà / il doppio del suo fuoco) per cento. Una che resiste lo dice al lanciatore e il lanciatore ne è l'aggressore. Ciò che non è un'evocazione non può essere dissolto |
| Energy Bolt | perla nera, nightshade | Un dardo vola al bersaglio e, mezzo secondo dopo, fa da 24 a 41 danni, scalati come Fireball |
| Explosion | blood moss, radice di mandragora | Due secondi e mezzo dopo il lancio, da 23 a 44 danni al bersaglio se è ancora vivo, scalati come Fireball |
| Invisibility | blood moss, nightshade | Il bersaglio viene nascosto, come con l'abilità Hiding, per 1,2 secondi a punto di Magery; non ha passi di Stealth, quindi il primo passo che fa, un colpo o un lancio lo mostrano. Il suo combattimento si ferma. Rifiutato per lo staff e per una creatura invulnerabile, come un venditore |
| Mark | perla nera, blood moss, radice di mandragora | La runa di richiamo nello zaino viene segnata con il punto del lanciatore, come fa [`.mark_rune`](commands/mark_rune.md). Rifiutato per ciò che non è una runa, una runa che non è nello zaino e una regione con il flag `mark` spento |
| Mass Curse | aglio, radice di mandragora, nightshade, cenere sulfurea | Curse su chiunque sia entro due caselle dal punto scelto |
| Paralyze Field | perla nera, ginseng, seta di ragno | Cinque pezzi di traverso sulla via, per 20 secondi: chi calpesta un pezzo resta immobile per 7 secondi più un quinto di secondo a punto di Magery. Rifiutato in una città sorvegliata |
| Reveal | blood moss, cenere sulfurea | Chi è nascosto entro 1 casella più un ventesimo della Magery dal punto scelto viene mostrato, con uno sbuffo e un suono (lo staff nascosto resta tale) |

## Il settimo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Chain Lightning | perla nera, blood moss, radice di mandragora, cenere sulfurea | Un fulmine su chiunque sia entro due caselle dal punto scelto, mezzo secondo dopo, per 27-48 danni divisi per il loro numero quando sono più di due; la metà della parte se il bersaglio resiste |
| Energy Field | perla nera, radice di mandragora, cenere sulfurea, seta di ragno | Cinque pezzi di energia di traverso sulla via, per 2 secondi più 0,28 di secondo a punto di Magery (30 a 100), che bloccano il movimento. Rifiutato in una città sorvegliata |
| Flame Strike | cenere sulfurea, seta di ragno | Una colonna di fuoco sul bersaglio e, mezzo secondo dopo, da 27 a 48 danni; tre quinti se il bersaglio resiste |
| Gate Travel | perla nera, radice di mandragora, cenere sulfurea | Un portale si apre dove sta il lanciatore e un altro nel punto di una runa segnata, ciascuno verso l'altro, per 30 secondi |
| Mana Vampire | perla nera, blood moss, radice di mandragora, seta di ragno | Tutto il mana del bersaglio passa al lanciatore, fino al suo massimo, se il bersaglio non resiste (98 volte su cento); la sua paralisi finisce e il suo lancio è rovinato |
| Mass Dispel | perla nera, aglio, radice di mandragora, cenere sulfurea | Dispel su ogni creatura evocata entro otto caselle dal punto scelto, ciascuna con la sua probabilità |
| Meteor Swarm | blood moss, radice di mandragora, cenere sulfurea, seta di ragno | Una palla di fuoco per chiunque sia entro due caselle dal punto scelto, mezzo secondo dopo, da 27 a 48 danni divisi per il loro numero; la metà della parte se il bersaglio resiste |
| Polymorph | blood moss, radice di mandragora, seta di ragno | Il lanciatore prende il corpo di una di diciotto forme (un pollo, un cane, un lupo, una pantera, un gorilla, tre orsi, un uomo, una melma, un orco, un uomo-lucertola, una gargolla, un orco gigante, un troll, un ettin, un demone o una donna), scelta da un elenco, per 1,2 secondi a punto di Magery. Il primo lancio apre l'elenco e non spende nulla; la scelta lancia l'incantesimo |

## L'ottavo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Earthquake | blood moss, ginseng, radice di mandragora, cenere sulfurea | Chiunque sia entro una casella più un quindicesimo della Magery del lanciatore perde sei decimi dei suoi punti ferita, subito: almeno 10 per una creatura che non è un giocatore, al massimo 75. Rifiutato in una città sorvegliata |
| Energy Vortex | perla nera, blood moss, radice di mandragora, nightshade | Viene evocato un vortice nel punto scelto: combatte per il lanciatore per 80-119 secondi. Vale un seguace e non obbedisce a nessun ordine |
| Resurrection | blood moss, aglio, ginseng | Al fantasma di un giocatore entro una casella dal lanciatore viene chiesto di tornare in vita (il gump degli ankh e dei guaritori), con il costo che lì hanno; alla risposta il posto viene chiesto di nuovo, e un fantasma che non ci sta più dove giace resta fantasma ("Thou can not be resurrected there!") |
| Summon Air Elemental, Summon Earth Elemental | blood moss, radice di mandragora, seta di ragno | Un elementale accanto al lanciatore, per tanti secondi quanti sono i punti di Magery. Vale due seguaci |
| Summon Water Elemental | blood moss, radice di mandragora, seta di ragno | Come sopra, per tre seguaci |
| Summon Fire Elemental | blood moss, radice di mandragora, cenere sulfurea, seta di ragno | Come sopra, per quattro seguaci |
| Summon Daemon | blood moss, radice di mandragora, cenere sulfurea, seta di ragno | Un demone, come sopra, per cinque seguaci; costa al lanciatore 70 punti di karma |

### Creature evocate

Una creatura evocata è fatta da un template di mobile (`bladespirit_summon`, `energyvortex_summon`, `airele_summon`,
`earthele_summon`, `firele_summon`, `waterele_summon`, `daemon_summon`, o un animale dell'elenco di Summon Creature) ed è un
seguace del lanciatore: ha la proprietà `owner` e vale i `control_slots` del suo template, quindi gli incantesimi che ne
evocano una vengono rifiutati, prima che si spenda qualcosa, quando i seguaci del lanciatore non hanno posto per lei (un
animale domato vale come prima). Ha l'ordine `guard` di un animale: resta vicina al lanciatore e combatte chi combatte lei o
il lanciatore, e le parole del lanciatore (stay, come, release...) la comandano, tranne per uno spirito di lama e un vortice di energia,
che nessuno comanda (la proprietà `pet.uncontrollable`: non sentono parole e non prendono cibo). Se ne va in uno sbuffo quando il suo tempo è
finito, quando il suo padrone muore, lascia il gioco o la lascia andare, e quando un Dispel la disfa; una che viene uccisa
non lascia cadavere. Il tempo è la proprietà `summon.until`, quindi una creatura salvata se ne va al momento giusto dopo un
riavvio, al suo primo pensiero vicino a un giocatore. La cavalcatura e la stalla rifiutano una creatura evocata (la
proprietà `summon.until`), quindi un cavallo o un lama di Summon Creature è un prestito dell'incantesimo e mai un animale
da tenere.

### Incantesimi d'area

Chain Lightning, Meteor Swarm, Mass Curse, Earthquake e Mass Dispel risparmiano il lanciatore, i morti, gli
invulnerabili, le creature del lanciatore e un giocatore che appare innocente (blu), a meno che il lanciatore non sia un
assassino; una creatura dal nome blu che nessun giocatore possiede non viene risparmiata. I campi di veleno e di paralisi
risparmiano anch'essi i morti, gli invulnerabili, le creature del lanciatore e un giocatore che appare innocente, ma il
lanciatore stesso ne è colpito quando calpesta un pezzo, come nel gioco classico. Il
lanciatore è l'aggressore di ciascuno che viene colpito, come per un colpo. Vengono rifiutati, prima che si spenda qualcosa,
quando non c'è nulla da toccare ("This spell won't work on that!") e, tranne Mass Dispel, in una città sorvegliata.

### Paralisi e travestimento

Paralyze, Paralyze Field e Mana Vampire usano due stati di un mobile. `mobile.paralyze(who, seconds)` lo immobilizza, cosa
che un mobile immobile non può annullare camminando e un lancio non può iniziare, e un combattente immobile mantiene il
combattimento ma non può colpire; il tempo di fine è conservato con il mobile e un giocatore che entra dopo un riavvio, o
un PNG al suo primo pensiero, viene liberato al momento giusto. Come nel gioco classico qualunque danno (anche quello di
un veleno) termina una paralisi, e lo fanno anche Clumsy, Weaken, Feeblemind, Curse, Poison, Mana Drain e Mana Vampire
lanciati sul paralizzato. `mobile.set_frozen` rileva il blocco: termina la paralisi in corso senza liberare il mobile,
quindi un blocco dello staff non viene tolto dal tempo di una paralisi.
`mobile.disguise(who, { name, name_list, body, hue }, seconds)` sostituisce il suo aspetto e lo restituisce allo scadere del
tempo, a una morte, all'accesso dopo un riavvio o, per un PNG, al suo primo pensiero dopo uno; Incognito e Polymorph lo
usano. Una morte termina anche una paralisi e una Magic Reflection.

### Semplificato nei cerchi dal quinto all'ottavo

- Magic Reflection è l'uso singolo classico dei primi giorni: rimanda un incantesimo, poi sparisce. Il lanciatore è
  l'aggressore di chi la indossa, come se fosse il suo bersaglio, e poi il suo stesso incantesimo lo raggiunge: subisce il
  danno, la maledizione o la paralisi da solo, con le proprie abilità, come se si fosse ferito da sé, senza nessuno da
  incolpare. Un Mana Vampire rimandato indietro svuota il lanciatore dentro se stesso, cioè non sposta nulla.
- Polymorph lancia due volte: il primo lancio apre l'elenco, il secondo, dopo la scelta, è il lancio. Incognito non cambia i
  capelli né la barba.
- Invisibility è lo stato nascosto dell'abilità Hiding: senza passi di Stealth, il primo passo mostra il bersaglio; dopo
  un riavvio un giocatore ancora nascosto viene mostrato dal suo primo passo.
- Gate Travel non chiede un combattimento in corso (il motore non ha un calore di combattimento), un sigillo né un libro di
  rune, e dice al lanciatore "You are not allowed to travel there." per un punto in cui una regione vieta un portale.
- Nel gioco classico dei primi giorni solo Blade Spirits e Summon Creature impiegavano quattro volte il ritardo del loro
  cerchio, e qui è così (`cast_delay_scale` in [`data/spells.toml`](data-files/spells.md)); Energy Vortex e gli elementali
  si lanciano con il ritardo del loro cerchio.
- Una semplificazione: gli incantesimi d'area (Chain Lightning, Meteor Swarm, Mass Curse, Earthquake e Mass Dispel)
  risparmiano sempre il lanciatore. Nel gioco classico dei primi giorni Meteor Swarm ed Earthquake lo risparmiavano, ma
  Chain Lightning e Mass Curse colpivano anche lui se stava a portata.
- Una semplificazione: un'evocazione, una Chain Lightning, un Earthquake e simili che non trovano nessuno da colpire
  vengono rifiutati prima che si spenda qualcosa. Il gioco classico prendeva il mana e i reagenti prima di cercare i
  bersagli.
- I seguaci di una creatura evocata si contano con i `control_slots` del template: il gioco classico dei primi giorni dice
  uno spirito di lama 1, un vortice di energia 1, un elementale d'aria e di terra 2, uno d'acqua 3, uno di fuoco 4, un
  demone 5, e il convertitore li scrive qualunque cosa dicano i dati di UOX3.

## Provalo

`.add test_kit_magery` dà una sacca che, alla prima apertura, si riempie con un libro completo, 20 di ogni reagente, tre
pergamene per ogni incantesimo costruito e quattro rune di richiamo. L'abilità si imposta a parte con
`.set skill magery 100`, e una runa si segna con `.mark_rune`.

## Cambiare le regole

- Gli incantesimi sono [`data/spells.toml`](data-files/spells.md): cerchio, parole, reagenti, bersaglio, flag, suono e grafica,
  e la pergamena di ciascuno. Generato da UOX3 con `moongate-convert uox-spells`.
- Un incantesimo è `scripts/spells/<key>.lua` sopra `scripts/common/magic.lua`: `check` può rifiutare prima che si spenda
  qualcosa, `cast` è l'effetto. Il mana, il ritardo e la finestra di Magery vengono dal cerchio, nel server.
- La finestra di abilità è la Magery tra cui una prova passa da un fallimento sicuro a un successo sicuro; quella di una
  pergamena è più facile di due cerchi.
- Le pergamene sono i template con `script_id = "spell_scroll"` e il libro quello con `script_id = "spellbook"`.
- Gli script raggiungono gli incantesimi con il [modulo `spell`](https://moongate.sh/lua/spell/).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/spells.toml`,
`scripts/spells/`, `scripts/common/magic.lua`, `scripts/common/field.lua`, `scripts/common/summon.lua`,
`scripts/common/creature.lua`, `scripts/common/pet_orders.lua`, `scripts/items/spellbook.lua`, `scripts/items/spell_scroll.lua`,
`scripts/items/magic_field.lua`, `scripts/items/moongate.lua`, `scripts/items/test_kit.lua`,
`scripts/gumps/resurrect.lua`, `scripts/gumps/polymorph_forms.lua`, `templates/gumps/resurrect.xml`,
`templates/gumps/polymorph_forms.xml`, `templates/items/magic/misc_magic.toml`, `templates/items/magic/scrolls.toml`,
`templates/items/magic/fields.toml`, `templates/items/test_kits.toml` e `templates/mobiles/magicsummon.toml`, e i
nuovi messaggi di `data/messages`. Un libro già creato conserva ciò che contiene; un
nuovo `spellbook` è vuoto.

## Non ancora

Magic Lock, Unlock, Magic Trap e Magic Untrap, inscription (scrivere pergamene), le bacchette, un PNG che lancia,
liberare le mani al lancio, i capelli di Incognito.

## Vedi anche

- [Combattimento](combat.md)
- [Pozioni](potions.md)
- [Abilità](skills.md)
- [`.mark_rune`](commands/mark_rune.md)
- [spells.toml](data-files/spells.md)
