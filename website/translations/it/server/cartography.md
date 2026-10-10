<!-- translation: {"sourceHash":"44d404645b90c3ead6b5ccd5ac7711ca1939c1b6bc60493f5a63a652e5f64acf","title":"Cartografia"} -->

# Cartografia

Un cartografo disegna mappe su mappe vuote con una penna: una mappa locale, una mappa di città o una carta nautica della terra attorno,
o una mappa di un mondo intero. Le regole sono quelle di ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità,
i fallimenti, gli attrezzi che si consumano e Make last; una mappa non è mai eccezionale né marchiata. Una mappa, una volta disegnata, si apre e accoglie un
percorso di puntine come ogni [mappa](maps.md).

## Come disegnare una mappa

1. Porta una penna da cartografo e delle mappe vuote nello zaino.
2. Fai doppio clic sulla penna. Si apre il gump di creazione della cartografia.
3. Premi il pulsante prima di una ricetta, e resta dove vuoi il centro della mappa: viene disegnata dove ti trovi quando è
   finita, 1,25 secondi dopo.

## Le ricette

| Ricetta | Cartography | Disegna |
| --- | --- | --- |
| Local map | da 10 a 70 | 64 caselle per lato e 2 in più per punto di abilità, su un disegno di 200 |
| City map | da 25 a 85 | 64 caselle e 4 in più per punto di abilità, almeno 200, su un disegno da 200 a 400 |
| Sea chart | da 35 a 95 | 64 caselle e 10 in più per punto di abilità, almeno 200, su un disegno da 200 a 400 |
| World map | da 39,5 a 99,5 | 20 caselle per punto di abilità attorno a Britain, ovunque tu sia, su un disegno da 200 a 400 |
| World map of Ilshenar, Malas, Tokuno, Ter Mur | da 39,5 a 99,5 | Tutto quel mondo |

Ognuna richiede una mappa vuota. Le mappe locali, di città e nautiche mostrano il mondo in cui ti trovi, fino al suo bordo. UOX3 accetta anche una
pergamena vuota; qui la si lascia agli scribi.
I numeri di abilità di UOX3 sono sbagliati di una cifra o oltre ogni abilità: il convertitore scrive invece quelli classici.

## Cambiare le regole

- Le ricette sono [`data/crafts/cartography.toml`](data-files/crafts.md).
- La penna è il template con `script_id = "cartography_tool"` (`scripts/items/cartography_tool.lua`); come si disegna una mappa
  è `scripts/common/cartography.lua`. Penna e inchiostro è di uno scriba: apre [Inscription](inscription.md).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/cartography.toml`,
`data/crafts/resources.toml`, `scripts/common/crafting.lua`, `scripts/common/cartography.lua`,
`scripts/items/cartography_tool.lua`, `templates/items/skills/tools/cartography.toml` e
`templates/items/skills/tools/inscription.toml`. Se disegnare una mappa fallisce, il giocatore legge "You could not finish what
you made." e il server registra il motivo.

## Non ancora

Le mappe del tesoro.

## Vedi anche

- [Mappe](maps.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
