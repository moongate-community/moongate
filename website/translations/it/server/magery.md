<!-- translation: {"sourceHash":"522e25e0d56865910a7e700a7202471f4edaa53434204e238139ace5eb0a7650","title":"Magery"} -->

# Magery

Un mago lancia gli incantesimi di Magery da un libro degli incantesimi o li legge da una pergamena, con le regole del gioco
classico: parole di potere, un ritardo in cui il lanciatore sta fermo, un cursore di mira, reagenti, mana e una prova di
abilità. Questa prima parte comprende il libro, il motore di lancio e i sette incantesimi del primo cerchio; gli altri
cerchi seguiranno.

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

Reactive Armor non ha ancora uno script: lanciarlo dice che l'incantesimo è disabilitato. Un incantesimo dannoso rende il
lanciatore l'aggressore del bersaglio: un criminale contro un innocente che non combatte, e un PNG reagisce. Una
maledizione su una statistica che è forte uguale o di più resta. Un incantesimo dannoso su un bersaglio che non può essere
colpito, come un venditore o un banchiere, viene rifiutato con "You cannot perform negative acts on your target." prima
che reagenti e mana siano spesi.

## Provalo

`.add test_kit_magery` dà una sacca che, alla prima apertura, si riempie con un libro completo, 20 di ogni reagente e tre
pergamene per ogni incantesimo del primo cerchio. L'abilità si imposta a parte con `.set skill magery 100`.

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
`scripts/spells/`, `scripts/common/magic.lua`, `scripts/items/spellbook.lua`, `scripts/items/spell_scroll.lua`,
`scripts/items/test_kit.lua`, `templates/items/magic/misc_magic.toml`, `templates/items/magic/scrolls.toml` e
`templates/items/test_kits.toml`. Un libro già creato conserva ciò che contiene; un nuovo `spellbook` è vuoto.

## Non ancora

I cerchi da 2 a 8, Reactive Armor, inscription (scrivere pergamene), le bacchette, un PNG che lancia, i luoghi in cui la
magia è vietata, liberare le mani al lancio, Magic Reflection.

## Vedi anche

- [Combattimento](combat.md)
- [Pozioni](potions.md)
- [Abilità](skills.md)
- [spells.toml](data-files/spells.md)
