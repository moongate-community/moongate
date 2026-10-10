<!-- translation: {"sourceHash":"01d97239fae026450cb756a5f86630c8d5758b1ece3f4437f4551cd74af8b7ce","title":"Inscription"} -->

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
volta sola, da un tentativo che viene fatto: lo paga un successo e lo paga anche un fallimento ("You fail to inscribe the
scroll, and the scroll is ruined."), che fa anche perdere metà dei materiali, arrotondata per difetto e almeno un'unità del
primo reagente. Un successo dice "You inscribe the spell and put the scroll in your backpack." Una pergamena non è mai
eccezionale né marchiata.

| Cerchio | Mana | Finestra di Inscription |
| --- | --- | --- |
| Primo | 4 | da -25 a 25 |
| Secondo | 6 | da -10.8 a 39.2 |
| Terzo | 9 | da 3.5 a 53.5 |
| Quarto | 11 | da 17.8 a 67.8 |
| Quinto | 14 | da 32.1 a 82.1 |
| Sesto | 20 | da 46.4 a 96.4 |
| Settimo | 40 | da 60.7 a 110.7 |
| Ottavo | 50 | da 75 a 125 |

La probabilità è una su due al minimo della finestra e certa alla sua fine, come in ogni mestiere; un minimo negativo,
come ha il primo cerchio, viene sempre tentato, quindi un principiante scrive subito le pergamene del primo cerchio. Le
ricette sono i 64 incantesimi, elencate che il libro li abbia o no, così un giocatore scopre cosa c'è da imparare; ognuna
richiede i reagenti che l'incantesimo chiede quando viene lanciato.

## Cambiare le regole

- Le ricette sono [`data/crafts/inscription.toml`](data-files/crafts.md), scritte dal convertitore a partire da
  `data/spells.toml` e dalla tabella dei cerchi; `spell` e `mana` sono i due campi che usa solo questo mestiere.
- L'attrezzo è il template con `script_id = "inscription_tool"` (`scripts/items/inscription_tool.lua`): penna e
  inchiostro, le cui grafiche sono `0x0fc0_pen_and_ink` e `0x0fbf`. La penna da cartografo resta un attrezzo di
  [cartografia](cartography.md).
- I testi di fallimento e di successo e il suono di un mestiere sono le tabelle `FAILED_TEXT`, `SUCCESS_TEXT` e
  `SUCCESS_SOUND` di `scripts/common/crafting.lua`.
- La borsa per lo staff `.add test_kit_inscription` contiene penna e inchiostro, 100 pergamene vuote, i reagenti e un
  libro completo; imposta le abilità a parte con `.set skill inscription 100` e `.set skill magery 100`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/inscription.toml`,
`data/crafts/resources.toml` (la lista `blank_scrolls`), `scripts/common/crafting.lua`, `scripts/gumps/craft_menu.lua`,
`scripts/items/inscription_tool.lua`, `scripts/items/test_kit.lua`, `templates/items/test_kits.toml` e
`templates/items/skills/tools/inscription.toml`. Una penna e inchiostro che era un attrezzo da cartografo ora apre
l'inscription; la cartografia si apre con la penna da cartografo.

## Non ancora

Pergamene delle espansioni successive, ordini di massa e scrivere un libro degli incantesimi vuoto. Le pergamene di Magic
Lock, Unlock, Magic Trap e Magic Untrap si possono scrivere, ma non si possono lanciare finché [Magery](magery.md#left-disabled) non li ha.

## Vedi anche

- [Magery](magery.md)
- [Cartografia](cartography.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
