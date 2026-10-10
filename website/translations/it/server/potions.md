<!-- translation: {"sourceHash":"eb1a2dd72e85c56665d1a5e3efe3786881f895c79b7d89f7f3f71d426c336940","title":"Pozioni"} -->

# Pozioni

Un giocatore beve una pozione con un doppio clic, nello zaino o entro 1 casella, anche in una borsa a terra ("That
is too far away for you to use"). Bere richiede una mano libera: non un'arma a due mani, né un'arma e uno scudo ("You must have a free hand to
drink a potion."). Una pozione della pila se ne va, poi il suo effetto, con il suono del bere e, per un umano a piedi,
il gesto; una bottiglia vuota torna nello zaino, o ai piedi quando è pieno. Una pozione che non si può consumare,
come una tenuta sul cursore, non fa nulla.

## Cosa fa ogni pozione

| Pozione | Effetto |
| --- | --- |
| Cura minore, cura, cura maggiore | Da 3 a 10, da 6 a 20, da 9 a 30 punti ferita; poi 10 secondi prima di un'altra pozione di cura ("You must wait 10 seconds before using another healing potion."). Non si beve a salute piena. |
| Rinvigorimento, rinvigorimento totale | Un quarto della stamina, o tutta. Non si beve a stamina piena. |
| Forza, forza maggiore | +10 o +20 di forza per 2 minuti, e altrettanti punti ferita in più al massimo. |
| Agilità, agilità maggiore | +10 o +20 di destrezza per 2 minuti, e altrettanta stamina in più al massimo. |
| Visione notturna | Vista al buio per 15-39 minuti, rimandata quando il giocatore cambia regione. |

Una seconda pozione di forza (o di agilità) mentre dura la prima viene rifiutata: "You are already under a similar effect.";
così anche una seconda visione notturna. Quando un bonus finisce, i punti ferita o la stamina sopra il nuovo massimo se ne vanno.

Le pozioni esplosive si lanciano, non si bevono: vedi [Esplosione](#explosion).

## Veleno

Una pozione di veleno avvelena chi la beve, al suo livello: lesser, regular, greater o deadly. Un mobile avvelenato perde
punti ferita a intervalli di pochi secondi, la sua barra della vita diventa verde, non recupera punti ferita, e chi è vicino legge che sembra stare male;
le pozioni di cura vengono rifiutate ("You can not heal yourself in your current state.").

| Livello | Danno a colpo | Ogni | Colpi |
| --- | --- | --- | --- |
| Lesser | 1 e il 2,5% dei punti ferita, da 4 a 26 | 3 s | 10 |
| Regular | 1 e il 3,125%, da 5 a 26 | 3 s | 10 |
| Greater | 1 e il 6,25%, da 6 a 26 | 3 s | 10 |
| Deadly | 1 e il 12,5%, da 7 a 26 | 4 s | 10 |

Il primo colpo arriva dopo 3,5 secondi, e metà dei colpi ripete il danno del precedente. Un veleno più forte sostituisce uno
più debole; uno più debole non cambia nulla. Il veleno svanisce ("The poison seems to have worn off."), viene curato, o
uccide; la morte lo ferma subito. Si salva con il personaggio insieme ai colpi già fatti: uscire dal gioco non lo ferma,
continua al rientro successivo. Il veleno di un NPC non si salva. Un livello 4, lethal, esiste per i mostri. Un giocatore avvelenato e nascosto
non viene visto stare male. I client più vecchi della 7.0 non disegnano la barra verde.

## Antidoti

Un antidoto cura il veleno con una probabilità; si consuma comunque ("That potion was not strong enough to cure your
ailment!"), e viene rifiutato quando chi beve non è avvelenato ("You are not poisoned.").

| Antidoto | Lesser | Regular | Greater | Deadly | Lethal |
| --- | --- | --- | --- | --- | --- |
| Antidoto minore | 75% | 50% | 15% | 0% | 0% |
| Antidoto | 100% | 75% | 50% | 15% | 0% |
| Antidoto maggiore | 100% | 100% | 100% | 75% | 25% |

Gli script avvelenano e curano con `mobile.poison(user, 2)`, `mobile.cure(user)` e `mobile.poison_level(user)`.

## Esplosione

Una pozione esplosiva non si beve ma si lancia. Fai doppio clic su di essa nello zaino o entro 1 casella: una pozione della pila viene
innescata nello zaino ("You should throw it now!"), si apre un cursore, e un conto alla rovescia 3, 2, 1 scorre sopra chi la tiene, visto
da chi è vicino, il primo numero dopo 0,75 secondi, poi uno al secondo. A 0 esplode dove si trova: in mano, sopra chi la
tiene; una tenuta sul cursore esplode appena viene lasciata.

Lanciala entro 10 caselle e in vista ("That is too far away.", "Target cannot be seen." la lasciano in mano, innescata;
fai di nuovo doppio clic per mirare). Vola un decimo di secondo per casella e il conto alla rovescia continua dove atterra.

L'esplosione ferisce ogni mobile vivo entro 2 caselle, anche chi la lancia, con le regole di un colpo: ferire un innocente che
non stava combattendo con chi lancia lo rende criminale, e una morte lo indica come uccisore. Le altre pozioni esplosive entro 2
caselle esplodono con essa, senza l'Alchemy di chi lancia. Chi lancia e poi esce dal gioco non viene incolpato di nulla, e i propri
animali non sono innocenti per un'esplosione né si rivoltano contro il padrone.

| Pozione | Danno |
| --- | --- |
| Esplosiva minore | da 5 a 10 |
| Esplosiva | da 10 a 20 |
| Esplosiva maggiore | da 15 a 30 |

L'Alchemy di chi lancia aggiunge un decimo dei suoi punti; un'esplosione fa al massimo 40. Gli script feriscono allo stesso modo con
`combat.harm(target, damage, attacker)`.

## Bonus e visione notturna

Un bonus di statistica si somma alla statistica base: lo stato mostra la somma, il danno in combattimento e il peso che un giocatore può portare
la usano, e i punti ferita e la stamina massimi crescono con essa. I guadagni di statistica alzano la base. Bonus e visione notturna non si salvano mai:
finiscono quando scade il tempo, quando il giocatore esce, o quando il server si ferma, e un salvataggio non scrive mai i punti ferita
o la stamina che tenevano sopra i massimi base. I massimi di un NPC non crescono con un bonus.

Gli script li danno con `mobile.add_stat_bonus(user, "strength", 10, 120)` e
`mobile.set_night_sight(user, 13, 1200)`; `mobile.stats` dà sia i valori con i bonus sia quelli `base_`.

## Cambiare le regole

- Le pozioni sono `scripts/items/potion.lua`: la tabella `EFFECTS` associa un template al suo effetto.
- I template con `script_id = "potion"` sono le pozioni qui sopra, in `templates/items/magic/potions.toml`; le
  pozioni esplosive hanno `script_id = "explosion_potion"` (`scripts/items/explosion_potion.lua`).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `scripts/items/potion.lua`,
`scripts/items/explosion_potion.lua` e `templates/items/magic/potions.toml`, oppure dai `script_id = "potion"` alle
pozioni che si bevono e `script_id = "explosion_potion"` alle pozioni esplosive.

## Non ancora

Alchimia, armi avvelenate e l'abilità Poisoning, mostri velenosi, bende che curano, la barra
dei buff, i barili di pozioni.

## Vedi anche

- [Script forniti](scripting/shipped-scripts.md)
- [Combattimento](combat.md)
