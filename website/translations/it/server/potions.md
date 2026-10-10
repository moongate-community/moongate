<!-- translation: {"sourceHash":"63df7a23836ef3d5886141db5bc4b016ceb2684dd3eb8cae424c66d14d030d93","title":"Pozioni"} -->

# Pozioni

Un giocatore beve una pozione con un doppio clic, nello zaino o entro 1 casella ("That is too far away for you to
use"). Bere richiede una mano libera: non un'arma a due mani, né un'arma e uno scudo ("You must have a free hand to
drink a potion."). Una pozione della pila se ne va, con il suono del bere e, per un umano a piedi, il gesto; una
bottiglia vuota torna nello zaino.

## Cosa fa ogni pozione

| Pozione | Effetto |
| --- | --- |
| Cura minore, cura, cura maggiore | Da 3 a 10, da 6 a 20, da 9 a 30 punti ferita; poi 10 secondi prima di un'altra pozione di cura ("You must wait 10 seconds before using another healing potion."). Non si beve a salute piena. |
| Rinvigorimento, rinvigorimento totale | Un quarto della stamina, o tutta. Non si beve a stamina piena. |
| Forza, forza maggiore | +10 o +20 di forza per 2 minuti, e altrettanti punti ferita in più al massimo. |
| Agilità, agilità maggiore | +10 o +20 di destrezza per 2 minuti, e altrettanta stamina in più al massimo. |
| Visione notturna | Vista al buio per 15-25 minuti. |

Una seconda pozione di forza (o di agilità) mentre dura la prima viene rifiutata: "You are already under a similar effect.";
così anche una seconda visione notturna. Quando un bonus finisce, i punti ferita o la stamina sopra il nuovo massimo se ne vanno.

Le pozioni di veleno e gli antidoti, e le pozioni esplosive, non si bevono ancora: arrivano con il veleno e con il lancio.

## Bonus e visione notturna

Un bonus di statistica si somma alla statistica base: lo stato mostra la somma, il danno in combattimento e il peso che un giocatore può portare
la usano, e i punti ferita e la stamina massimi crescono con essa. I guadagni di statistica alzano la base. Bonus e visione notturna non si salvano mai:
finiscono quando scade il tempo, quando il giocatore esce, o quando il server si ferma.

Gli script li danno con `mobile.add_stat_bonus(user, "strength", 10, 120)` e
`mobile.set_night_sight(user, 13, 1200)`; `mobile.stats` dà sia i valori con i bonus sia quelli `base_`.

## Cambiare le regole

- Le pozioni sono `scripts/items/potion.lua`: la tabella `EFFECTS` associa un template al suo effetto.
- I template con `script_id = "potion"` sono i dieci qui sopra, in `templates/items/magic/potions.toml`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `scripts/items/potion.lua` e
`templates/items/magic/potions.toml`, oppure dai `script_id = "potion"` alle tue pozioni di cura, rinvigorimento, forza, agilità e
visione notturna.

## Non ancora

Veleno e antidoti (fetta 2), esplosione (fetta 3), alchimia, la barra dei buff, i barili di pozioni.

## Vedi anche

- [Script forniti](scripting/shipped-scripts.md)
- [Combattimento](combat.md)
