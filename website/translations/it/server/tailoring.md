<!-- translation: {"sourceHash":"3ab899f5f781ea7c1d2b7029fe03730c5848a918632fcfdd0c9f9f1a2fc3a9ff","title":"Sartoria"} -->

# Sartoria

Un sarto cuce vestiti, calzature e armature di cuoio da stoffa e cuoio. Le regole sono quelle di ogni mestiere:
vedi [Falegnameria](carpentry.md) per la probabilità, i fallimenti, gli oggetti eccezionali, il marchio del creatore, gli attrezzi che si consumano e
Make last.

## Come cucire

1. Porta un kit da cucito nello zaino, o in una borsa al suo interno. La sartoria non ha bisogno di un banco di lavoro.
2. Fai doppio clic su di esso. Si apre il gump di creazione della sartoria.
3. Premi il pulsante prima di una ricetta, oppure apri la sua scheda per vedere la stoffa o il cuoio che richiede, le abilità e la tua
   probabilità.

Una ricetta di cui ti mancano stoffa o cuoio risponde "You don't have enough cloth to make that." oppure "You do not have
sufficient leather to make that." e non toglie nulla.

## Le ricette

50 ricette in otto gruppi, convertite da UOX3. La prima di ogni gruppo:

| Gruppo | Ricetta | Tailoring | Richiede |
| --- | --- | --- | --- |
| Hats | Skullcap | da 0 a 52 | 2 stoffa |
| Shirts | Doublet | da 0,1 a 50 | 8 stoffa |
| Pants | Long pants | da 24,8 a 75 | 8 stoffa |
| Miscellaneous | Body sash | da 4,1 a 54 | 4 stoffa |
| Footwear | Sandals | da 12,4 a 62 | 4 cuoio |
| Leather Armor | Leather gorget | da 53,9 a 104 | 4 cuoio |
| Studded Armor | Studded gorget | da 78,8 a 129 | 6 cuoio |
| Female Armor | Leather shorts | da 62,2 a 112 | 8 cuoio |

La stoffa è qualunque stoffa piegata o tagliata; il cuoio è cuoio tagliato o mucchi di pelli. Un pezzo di armatura di cuoio eccezionale
dà 8 di armatura in più, come dice il [fabbro](blacksmithing.md#exceptional-weapons-and-armor); lo stesso vale per un cappello o
un abito eccezionale che ha un valore di armatura proprio, come il cappello da mago.

## Cambiare le regole

- Le ricette sono [`data/crafts/tailoring.toml`](data-files/crafts.md).
- I kit da cucito sono i template con `script_id = "tailoring_tool"` (`scripts/items/tailoring_tool.lua`); le regole
  sono `scripts/common/crafting.lua`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/tailoring.toml`,
`scripts/common/crafting.lua` e `scripts/items/tailoring_tool.lua`, e `templates/items/skills/tools/tailoring.toml`,
oppure dai `script_id = "tailoring_tool"` ai tuoi kit da cucito.

## Non ancora

Armature d'osso e pantaloncini, che UOX3 non mette in nessun menu; tipi di cuoio (spined, horned, barbed, che i kit runici
lavorerebbero) e stoffa colorata da scegliere; tagliare stoffa e pelli con le forbici, tingere ciò che
si crea, e filare lana e lino in stoffa.

## Vedi anche

- [Falegnameria](carpentry.md)
- [Fabbro](blacksmithing.md)
- [File dati dei mestieri](data-files/crafts.md)
