<!-- translation: {"sourceHash":"a2ddd2adfc9f0a9a4c1ebe8d6ec8dc4710463c6e15facb20edb294010916d17f","title":"Pozioni"} -->

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

Le pozioni esplosive non si bevono: verranno lanciate.

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
uccide; la morte lo ferma. Si salva con il personaggio: uscire dal gioco non lo ferma, riparte al rientro successivo.
Un livello 4, lethal, esiste per i mostri.

## Antidoti

Un antidoto cura il veleno con una probabilità; si consuma comunque ("That potion was not strong enough to cure your
ailment!"), e viene rifiutato quando chi beve non è avvelenato ("You are not poisoned.").

| Antidoto | Lesser | Regular | Greater | Deadly |
| --- | --- | --- | --- | --- |
| Antidoto minore | 75% | 50% | 15% | 0% |
| Antidoto | 100% | 75% | 50% | 15% |
| Antidoto maggiore | 100% | 100% | 100% | 75% |

Gli script avvelenano e curano con `mobile.poison(user, 2)`, `mobile.cure(user)` e `mobile.poison_level(user)`.

## Bonus e visione notturna

Un bonus di statistica si somma alla statistica base: lo stato mostra la somma, il danno in combattimento e il peso che un giocatore può portare
la usano, e i punti ferita e la stamina massimi crescono con essa. I guadagni di statistica alzano la base. Bonus e visione notturna non si salvano mai:
finiscono quando scade il tempo, quando il giocatore esce, o quando il server si ferma, e un salvataggio non scrive mai i punti ferita
o la stamina che tenevano sopra i massimi base. I massimi di un NPC non crescono con un bonus.

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

Esplosione (fetta 3), alchimia, armi avvelenate e l'abilità Poisoning, mostri velenosi, bende che curano, la barra
dei buff, i barili di pozioni.

## Vedi anche

- [Script forniti](scripting/shipped-scripts.md)
- [Combattimento](combat.md)
