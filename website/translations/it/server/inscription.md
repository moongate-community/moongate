<!-- translation: {"sourceHash":"ed0a5c5834892b688ceeafa78047449f61ccb19dbc37d19e46814c8ab304032f","title":"Inscription"} -->

# Inscription

Uno scriba scrive la pergamena di un incantesimo con penna e inchiostro, su una pergamena vuota, partendo dai reagenti
dell'incantesimo. È l'ultimo mestiere: le regole sono quelle di ogni mestiere (vedi [Falegnameria](carpentry.md)): la
probabilità, i fallimenti che fanno perdere materiali, una penna che si consuma e Make last. La novità è che una ricetta
chiede un incantesimo e del mana, come un lancio. Le pergamene create sono quelle che un mago
[lancia o scrive in un libro](magery.md).

## Come scrivere una pergamena

1. Porta penna e inchiostro, delle pergamene vuote e i reagenti nello zaino, e un libro degli incantesimi che contenga
   l'incantesimo (indossato, o nello zaino ma non in una borsa al suo interno).
2. Doppio clic su penna e inchiostro. Si apre il gump di creazione dell'inscription, con un gruppo per ogni cerchio, dal Primo
   all'Ottavo.
3. Premi il pulsante davanti a una ricetta, oppure apri la sua pagina, che dice i reagenti, la finestra di Inscription, il
   mana e la tua probabilità.

Una ricetta viene rifiutata, prima di spendere qualsiasi cosa, con il testo classico:

| Testo | Quando |
| --- | --- |
| You don't have that spell! | nessun libro che porti contiene l'incantesimo |
| You don't have the components needed to make that. | manca un reagente o la pergamena vuota |
| Insufficient mana for this spell. | hai meno mana di quanto costi il cerchio |
| You don't have the required skills to attempt this item. | Inscription è sotto il minimo della ricetta |

Tutto viene controllato all'inizio e di nuovo al secondo colpo, quindi un libro dato via o del mana speso nel frattempo
fanno rifiutare il secondo colpo senza prendere nulla.

## Cosa serve

Ogni pergamena richiede i reagenti del suo incantesimo, una pergamena vuota e il mana del suo cerchio. Il mana si paga una
volta sola, da un successo soltanto: la pergamena viene creata, i reagenti e la pergamena vuota se ne vanno con lei, e con
loro il mana. Un fallimento ("You fail to inscribe the scroll, and the scroll is ruined.") non costa mana e rovina
un'unità di ogni risorsa, anche della pergamena vuota, che non è la metà dei materiali che perdono gli altri mestieri. Un
successo dice "You inscribe the spell and put the scroll in your backpack"; quando lo zaino è pieno la pergamena resta ai tuoi
piedi e viene detto solo questo. Una pergamena non è mai eccezionale né marchiata.

| Cerchio | Mana | Finestra di Inscription |
| --- | --- | --- |
| Primo | 4 | da 0 a 40.1 |
| Secondo | 6 | da 6.1 a 50.1 |
| Terzo | 9 | da 16.1 a 60.1 |
| Quarto | 11 | da 26.1 a 70.1 |
| Quinto | 14 | da 36.1 a 80.1 |
| Sesto | 20 | da 46.1 a 90.1 |
| Settimo | 40 | da 66.1 a 110.1 |
| Ottavo | 50 | da 76.1 a 120.1 |

Le finestre sono quelle del file dati classico per cui è stata costruita la regola del motore: la probabilità è una su due
al minimo della finestra e certa alla sua fine, come in ogni mestiere. Il primo cerchio parte da 0 invece che da 1.1, così
un principiante senza alcuna abilità può comunque provare, e crescere. Le ricette sono i 64 incantesimi, elencate che il
libro li abbia o no, così un giocatore scopre cosa c'è da imparare; ognuna richiede i reagenti che l'incantesimo chiede
quando viene lanciato.

## Cambiare le regole

- Le ricette sono [`data/crafts/inscription.toml`](data-files/crafts.md), scritte dal convertitore a partire da
  `data/spells.toml` e dalla tabella dei cerchi; `spell` e `mana` sono i due campi che usa solo questo mestiere.
- L'attrezzo è il template con `script_id = "inscription_tool"` (`scripts/items/inscription_tool.lua`): penna e
  inchiostro, le cui grafiche sono `0x0fc0_pen_and_ink` e `0x0fbf`. La penna da cartografo resta un attrezzo di
  [cartografia](cartography.md).
- I testi di fallimento e di successo e il suono di un mestiere sono le tabelle `FAILED_TEXT`, `SUCCESS_TEXT` e
  `SUCCESS_SOUND` di `scripts/common/crafting.lua`; i mestieri il cui fallimento rovina un'unità di ogni risorsa sono la
  tabella `FAIL_LOSES_ALL`.
- La borsa per lo staff `.add test_kit_inscription` contiene penna e inchiostro, 100 pergamene vuote, i reagenti e un
  libro completo; imposta le abilità a parte con `.set skill inscription 100` e `.set skill magery 100`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/inscription.toml`,
`data/crafts/resources.toml` (la lista `blank_scrolls`), `scripts/common/crafting.lua`, `scripts/gumps/craft_menu.lua`,
`scripts/items/inscription_tool.lua`, `scripts/items/test_kit.lua`, `templates/items/test_kits.toml` e
`templates/items/skills/tools/inscription.toml` e `templates/shops/mapmaker.toml` (il cartografo vende la penna da
cartografo). Una penna e inchiostro che era un attrezzo da cartografo ora apre l'inscription; la cartografia si apre con
la penna da cartografo.

## Non ancora

Pergamene delle espansioni successive, ordini di massa e scrivere un libro degli incantesimi vuoto.

Le pergamene di **Magic Lock, Unlock, Magic Trap e Magic Untrap si possono scrivere ma non lanciare**: le ricette ci sono, e
uno scriba può farle e venderle, ma lanciarle dice che l'incantesimo è disabilitato finché i contenitori non hanno uno
stato di lucchetto e di trappola (vedi [Magery](magery.md#left-disabled)).

## Vedi anche

- [Magery](magery.md)
- [Cartografia](cartography.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
