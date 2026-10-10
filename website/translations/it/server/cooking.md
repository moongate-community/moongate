<!-- translation: {"sourceHash":"dd540d3e46c897dca20bf99d20836076f41d58e5803b617098985eac33211bd9","title":"Cucina"} -->

# Cucina

Un cuoco trasforma farina e acqua in impasto, l'impasto in pane, torte e pizze, e carne e pesce crudi
in pasti. Le regole sono quelle di ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità, i fallimenti, gli oggetti
eccezionali, il marchio del creatore, gli attrezzi che si consumano e Make last.

## Come cucinare

1. Porta una padella, un setaccio per la farina o un mattarello nello zaino, o in una borsa al suo interno.
2. Fai doppio clic su di esso. Si apre il gump di creazione della cucina.
3. Premi il pulsante prima di una ricetta, oppure apri la sua scheda.

Ingredients e Preparation si fanno ovunque. Baking richiede un forno entro 2 caselle: "You must be near an oven to bake
that." Barbecue richiede una fonte di calore entro 2 caselle (un forno, un camino, un fuoco da campo, un focolare, un fornelletto, un
braciere o una forgia): "You must be near a fire source to cook." Entrambi si controllano all'inizio e di nuovo al secondo
colpo.

## Le ricette

31 ricette in quattro gruppi, convertite da UOX3, ognuna da 0 a 100 di Cooking:

| Gruppo | Ricette | Per esempio |
| --- | --- | --- |
| Ingredients | 5 | Dough: 1 farina, 1 acqua |
| Preparation | 8 | Unbaked apple pie: 1 impasto, 1 mela |
| Baking | 12 | Bread loaf: 1 impasto |
| Barbecue | 6 | Fish steak: 1 trancio di pesce crudo |

UOX3 distingue l'impasto dolce dall'impasto, e le torte crude l'una dall'altra, per colore e per un numero nascosto; il
convertitore usa invece i loro template. Inoltre cuoce ogni pizza dalla sua pizza cruda, e ogni taglio crudo
(coscia di pollo, cosciotto d'agnello, costine) dal suo taglio, dove UOX3 accetta qualsiasi carne cruda. Un sacco di farina chiuso, comprato da un
fornaio o da un mugnaio o tra gli oggetti iniziali, conta come farina così com'è: UOX3 prima lo apre.

## Cambiare le regole

- Le ricette sono [`data/crafts/cooking.toml`](data-files/crafts.md).
- Gli attrezzi sono i template con `script_id = "cooking_tool"` (`scripts/items/cooking_tool.lua`); le regole sono
  `scripts/common/crafting.lua`, e i forni e le fonti di calore `scripts/common/heat.lua`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/cooking.toml`,
`scripts/common/crafting.lua`, `scripts/common/heat.lua`, `scripts/items/cooking_tool.lua` e
`templates/items/skills/tools/cooking.toml`, oppure dai `script_id = "cooking_tool"` alle tue padelle, setacci e
mattarelli.

## Non ancora

Una brocca d'acqua si consuma, non resta vuota, e un sacco o una ciotola di farina finisce intero in un impasto, dove UOX3 dà a un
sacco venti usi. Il grano non si può ancora ottenere, quindi la ricetta Sack of flour lo aspetta. I mulini per la farina, e le ricette delle
ere successive.

## Vedi anche

- [Falegnameria](carpentry.md)
- [Pesca](fishing.md)
- [File dati dei mestieri](data-files/crafts.md)
