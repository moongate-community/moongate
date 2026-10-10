<!-- translation: {"sourceHash":"88e05d38ab9ac3c01d65b24a1af22f8dde6b0c3edf3ee325f69e006e4e4bafc5","title":"Meccanica"} -->

# Meccanica

Un armaiolo (tinker) crea attrezzi, parti, utensili, gioielli e candele dai lingotti, con gemme, cera d'api o un teschio per alcuni. Le regole
sono quelle di ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità, i fallimenti, gli oggetti eccezionali, il marchio del
creatore, gli attrezzi che si consumano e Make last.

## Come lavorare

1. Porta gli attrezzi da tinker o una cassetta degli attrezzi nello zaino, o in una borsa al suo interno. La meccanica non ha bisogno di un banco di lavoro.
2. Fai doppio clic su di essi. Si apre il gump di creazione della meccanica.
3. Scegli il metallo da lavorare con Change, come fa un [fabbro](blacksmithing.md#metals): ferro se non lo scegli, oppure un metallo colorato
   che chiede tanto Tinkering quanto chiede Blacksmithy a un fabbro, e colora ciò che si crea.
4. Premi il pulsante prima di una ricetta, oppure apri la sua scheda.

## Le ricette

59 ricette in sette gruppi, convertite da UOX3. La prima di ogni gruppo:

| Gruppo | Ricetta | Tinkering | Richiede |
| --- | --- | --- | --- |
| Tools | Scissors | da 14,5 a 55 | 4 metallo |
| Parts | Gears | da 14,7 a 65 | 2 metallo |
| Utensils | Butcher knife | da 26,1 a 76 | 2 metallo |
| Jewelry | Weddingband | da 41,8 a 92 | 1 metallo, 1 diamante |
| Miscellaneous | Keyring | da 21,8 a 72 | 2 metallo |
| More Tools | Froe | da 33,2 a 83 | 2 metallo |
| Candles | Candelabra | da 67,1 a 117 | 4 metallo, 3 cera d'api |

Le trappole di UOX3 sono escluse: un contenitore non si può ancora intrappolare. Il kit da tassidermia ha la grafica di una
cassetta degli attrezzi ma non è un attrezzo da tinker. Gli utensili girati dall'altra parte hanno una seconda ricetta numerata (Spoon, Spoon 2). Due ricette di cui UOX3 scrive male l'abilità (la bilancia
e il fornelletto, da 63,8 e 64,3 a 114) vengono corrette dal convertitore.

## Cambiare le regole

- Le ricette sono [`data/crafts/tinkering.toml`](data-files/crafts.md).
- Gli attrezzi sono i template con `script_id = "tinkering_tool"` (`scripts/items/tinkering_tool.lua`); le regole sono
  `scripts/common/crafting.lua`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/tinkering.toml`,
`scripts/common/crafting.lua` e `scripts/items/tinkering_tool.lua`, e `templates/items/skills/tools/tinkering.toml`,
oppure dai `script_id = "tinkering_tool"` ai tuoi attrezzi da tinker e cassette degli attrezzi.

## Non ancora

Trappole sui contenitori, montare le parti in orologi e sestanti nel modo in cui si usano, e i gioielli speciali delle ere
successive.

## Vedi anche

- [Fabbro](blacksmithing.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
