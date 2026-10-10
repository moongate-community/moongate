<!-- translation: {"sourceHash":"32ab43b936ef3aa5a9750bb27966018b42c25726d3017fe833714ae74d02a22c","title":"Cartografia"} -->

# Cartografia

Un cartografo disegna mappe su mappe vuote con una penna: una mappa locale, una mappa di città o una carta nautica della terra attorno,
o una mappa di un mondo intero. Le regole sono quelle di ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità,
i fallimenti, gli oggetti eccezionali, il marchio del creatore, gli attrezzi che si consumano e Make last. Una mappa, una volta disegnata, si apre e accoglie un
percorso di puntine come ogni [mappa](maps.md).

## Come disegnare una mappa

1. Porta una penna da cartografo, o penna e inchiostro, e delle mappe vuote nello zaino.
2. Fai doppio clic sulla penna. Si apre il gump di creazione della cartografia.
3. Mettiti dove vuoi il centro della mappa, e premi il pulsante prima di una ricetta.

## Le ricette

| Ricetta | Cartography | Disegna |
| --- | --- | --- |
| Local map | da 10 a 70 | 64 caselle per lato e 2 in più per punto di abilità, su un disegno di 200 |
| City map | da 25 a 85 | 64 caselle e 4 in più per punto di abilità, almeno 200, su un disegno da 200 a 400 |
| Sea chart | da 35 a 95 | 64 caselle e 10 in più per punto di abilità, almeno 200, su un disegno da 200 a 400 |
| World map | da 39,5 a 99,5 | Tutta Britannia |
| World map of Ilshenar, Malas, Tokuno, Ter Mur | da 39,5 a 99,5 | Tutto quel mondo |

Ognuna richiede una mappa vuota (o una pergamena vuota, come in UOX3). Le mappe locali, di città e nautiche mostrano il mondo in cui ti trovi.
I numeri di abilità di UOX3 sono sbagliati di una cifra o oltre ogni abilità: il convertitore scrive invece quelli classici.

## Cambiare le regole

- Le ricette sono [`data/crafts/cartography.toml`](data-files/crafts.md).
- Le penne sono i template con `script_id = "cartography_tool"` (`scripts/items/cartography_tool.lua`); come si disegna una mappa
  è `scripts/common/cartography.lua`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/cartography.toml`,
`data/crafts/resources.toml`, `scripts/common/crafting.lua`, `scripts/common/cartography.lua`,
`scripts/items/cartography_tool.lua`, `templates/items/skills/tools/cartography.toml` e
`templates/items/skills/tools/inscription.toml`.

## Non ancora

L'iscrizione, che condividerà le penne; le mappe del tesoro.

## Vedi anche

- [Mappe](maps.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
