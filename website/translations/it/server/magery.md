<!-- translation: {"sourceHash":"78d712a6385c2764d6f496664993fb21a22554d2d96768cdef4b3c92af51743d","title":"Magery"} -->

# Magery

Un mago lancia gli incantesimi di Magery da un libro degli incantesimi o li legge da una pergamena, con le regole del gioco
classico: parole di potere, un ritardo in cui il lanciatore sta fermo, un cursore di mira, reagenti, mana e una prova di
abilità. Comprende il libro, il motore di lancio, i primi quattro cerchi e Reactive Armor; gli altri cerchi
seguiranno.

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

| Reactive Armor | aglio, seta di ragno, cenere sulfurea | Per 25 secondi e mezzo secondo a punto di Magery, una parte di ogni colpo in mischia che arriva da distanza di un braccio torna a chi lo ha dato: il 10 per cento e un quarto di per cento a punto di Magery (35 a 100) |

Un incantesimo dannoso rende il lanciatore l'aggressore del bersaglio: un criminale contro un innocente che non combatte, e
un PNG reagisce. Una maledizione su una statistica che è forte uguale o di più resta. Un incantesimo dannoso su un bersaglio
che non può essere colpito, come un venditore o un banchiere, viene rifiutato con "You cannot perform negative acts on your
target." prima che reagenti e mana siano spesi.

## Il secondo cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Agility | blood moss, radice di mandragora | Alza la destrezza di 1 più un decimo della Magery del lanciatore, per 1,2 secondi a punto |
| Cunning | radice di mandragora, nightshade | Come Agility, sull'intelligenza |
| Strength | radice di mandragora, nightshade | Come Agility, sulla forza |
| Cure | aglio, ginseng | Può far finire un veleno: la probabilità è (10000 + 75 a punto di Magery - 1750 per ogni livello del veleno, il minore essendo 1) / 100 per cento. Una cura riuscita avvisa il bersaglio e il lanciatore, una fallita il lanciatore |
| Harm | nightshade, seta di ragno | Subito, da 1 a 15 danni, tre quarti se resistito, scalati come gli altri incantesimi di danno; la metà a due caselle e un quarto oltre |
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
| Teleport | blood moss, radice di mandragora | Il lanciatore si trova nel punto scelto, in linea di vista, con uno sbuffo in entrambi i punti. Rifiutato prima che si spenda qualcosa quando il lanciatore è troppo carico per muoversi, nessuno può stare lì, o una regione vieta un teletrasporto in uscita dal suo punto o in entrata nella destinazione |
| Telekinesis | blood moss, radice di mandragora | Usa un oggetto da lontano come farebbe un doppio clic: un contenitore si apre, una porta si muove. Rifiutato per ciò che non ha un uso |
| Wall of Stone | blood moss, aglio | Tre pezzi di muro di traverso sulla via dal lanciatore al punto, che bloccano il movimento per dieci secondi; nessun pezzo dove sta un mobile |
| Magic Lock, Unlock | | Disabilitati, vedi sotto |

## Il quarto cerchio

| Incantesimo | Reagenti | Cosa fa |
| --- | --- | --- |
| Arch Cure | aglio, ginseng, radice di mandragora | Cure su chiunque sia vivo entro due caselle dal punto scelto, con una probabilità un poco più bassa, l'uno per cento in meno |
| Arch Protection | aglio, ginseng, radice di mandragora, cenere sulfurea | Protection su chiunque sia vivo entro tre caselle dal punto scelto e non l'abbia già |
| Curse | nightshade, aglio, cenere sulfurea | Abbassa le tre statistiche del bersaglio insieme, come fanno Clumsy, Feeblemind e Weaken ciascuno |
| Fire Field | perla nera, seta di ragno, cenere sulfurea | Cinque pezzi di fuoco di traverso sulla via, per 20 secondi: chi calpesta un pezzo o ci sta dentro brucia per 2 danni una volta al secondo (1 quando una prova di Resisting Spells riesce); il fuoco non blocca |
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
lo farà per i giocatori. Recall viene rifiutato, prima che si spenda qualcosa, per ciò che non è una runa, una runa non
segnata, un criminale, un lanciatore troppo carico per muoversi, una runa di un'altra mappa, un punto in cui nulla può
stare e una regione che non permette un richiamo in uscita dal suo punto o in entrata nella destinazione (i flag
`recall_out` e `recall_in` delle regioni).

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
- Recall non controlla un combattimento in corso, come non faceva il gioco classico prima dell'AOS, e gli animali di un
  mobile non lo seguono.
- Teleport e Recall non rifiutano un punto occupato da un mobile o da un oggetto, solo uno in cui nulla può stare.

## Provalo

`.add test_kit_magery` dà una sacca che, alla prima apertura, si riempie con un libro completo, 20 di ogni reagente tre
pergamene per ogni incantesimo costruito dei primi quattro cerchi e due rune di richiamo. L'abilità si imposta a parte con
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
`scripts/spells/`, `scripts/common/magic.lua`, `scripts/common/field.lua`, `scripts/items/spellbook.lua`,
`scripts/items/spell_scroll.lua`, `scripts/items/magic_field.lua`, `scripts/items/test_kit.lua`,
`templates/items/magic/misc_magic.toml`, `templates/items/magic/scrolls.toml`, `templates/items/magic/fields.toml` e
`templates/items/test_kits.toml`, e i nuovi messaggi di `data/messages`. Un libro già creato conserva ciò che contiene; un
nuovo `spellbook` è vuoto.

## Non ancora

I cerchi da 5 a 8, Magic Lock, Unlock, Magic Trap e Magic Untrap, Magic Reflection, inscription (scrivere pergamene), le
bacchette, un PNG che lancia, liberare le mani al lancio.

## Vedi anche

- [Combattimento](combat.md)
- [Pozioni](potions.md)
- [Abilità](skills.md)
- [`.mark_rune`](commands/mark_rune.md)
- [spells.toml](data-files/spells.md)
