<!-- translation: {"sourceHash":"fe520fdd7c430ff661f394fa4c3fae32414249fff2ea55c57660c19e5d340db8","title":"Archi e frecce"} -->

# Archi e frecce

Un arciere-artigiano crea archi e balestre dalle assi, aste dalle assi, e frecce e dardi da balestra da aste e
piume. Le regole sono quelle di ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità, i fallimenti, gli oggetti
eccezionali, il marchio del creatore, gli attrezzi che si consumano e Make last.

## Come creare archi e frecce

1. Porta gli attrezzi da fletcher nello zaino, o in una borsa al suo interno. Archi e frecce non hanno bisogno di un banco di lavoro.
2. Fai doppio clic su di essi. Si apre il gump di creazione di archi e frecce.
3. Scegli il legno con Change, come fa un [falegname](carpentry.md): assi comuni se non lo scegli, oppure un tipo di legno che chiede
   tanto Bowcraft/Fletching quanto chiede Carpentry a un falegname, e colora l'arco.
4. Premi il pulsante prima di una ricetta, oppure apri la sua scheda. Make last la ripete: una freccia si crea una alla volta.

## Le ricette

9 ricette in quattro gruppi, convertite da UOX3:

| Gruppo | Ricetta | Bowcraft/Fletching | Richiede |
| --- | --- | --- | --- |
| Weapons | Bow | da 30 a 70 | 7 legno |
| Weapons | Crossbow | da 60 a 100 | 7 legno |
| Weapons | Heavy crossbow | da 90 a 130 | 10 legno |
| Weapons | Composite bow | da 70 a 100 | 7 legno |
| Weapons | Repeating crossbow | da 90 a 100 | 10 legno |
| Weapons | Yumi | da 90 a 100 | 10 legno |
| Shafts | Shaft | da 0 a 40 | 1 legno |
| Arrows | Arrow | da 0 a 40 | 1 asta, 1 piuma |
| Crossbow Bolts | Bolt | da 0 a 70 | 1 asta, 1 piuma |

I lotti da cinque, venti e cinquanta di UOX3 sono esclusi: Make last li crea uno dopo l'altro. Anche la legna da ardere è esclusa:
la taglia già un'[ascia](lumberjacking.md) dai tronchi.

## Cambiare le regole

- Le ricette sono [`data/crafts/fletching.toml`](data-files/crafts.md).
- Gli attrezzi sono i template con `script_id = "fletching_tool"` (`scripts/items/fletching_tool.lua`); le regole sono
  `scripts/common/crafting.lua`.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/fletching.toml`,
`scripts/items/fletching_tool.lua` e `templates/items/skills/tools/fletching.toml`, oppure dai
`script_id = "fletching_tool"` ai tuoi attrezzi da fletcher.

## Non ancora

Creare più frecce in una volta, e gli archi delle ere successive.

## Vedi anche

- [Falegnameria](carpentry.md)
- [Taglio della legna](lumberjacking.md)
- [File dati dei mestieri](data-files/crafts.md)
