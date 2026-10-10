<!-- translation: {"sourceHash":"56130a48c034b391f8ec9369661edeb456c9b8eea8f5f21a21029e2a5c5df6b6","title":"Alchimia"} -->

# Alchimia

Un alchimista macina i reagenti con mortaio e pestello e versa ogni pozione in una bottiglia vuota. Le regole sono quelle
di ogni mestiere (vedi [Falegnameria](carpentry.md)): la probabilità, una su due all'abilità minima e certa alla massima, i fallimenti
che fanno perdere metà dei reagenti ("You fail to create a useful potion.", la bottiglia resta), un mortaio che si consuma, e
Make last. Una pozione si impila, quindi non è mai eccezionale né marchiata; un successo fa sentire la pozione versata. Cosa fa ogni
pozione è spiegato in [Pozioni](potions.md).

## Come creare una pozione

1. Porta mortaio e pestello, i reagenti e delle bottiglie vuote nello zaino.
2. Fai doppio clic sul mortaio. Si apre il gump di creazione dell'alchimia.
3. Premi il pulsante prima di una ricetta, oppure apri la sua scheda.

## Le ricette

20 pozioni in otto gruppi, convertite da UOX3 con i suoi numeri di abilità e le sue quantità di reagenti (differiscono un po' da
altri shard: Refresh da 15,1, Nightsight con 5 seta di ragno); ognuna richiede anche una bottiglia vuota.

| Gruppo | Pozioni (Alchemy, reagenti) |
| --- | --- |
| Agility | Agility 15,1-65 (1 blood moss), Greater Agility 35,1-85 (3) |
| Cure | Lesser Cure 0-50 (1 aglio), Cure 25,1-75 (3), Greater Cure 65,1-115 (6) |
| Explosion | Lesser Explosion 5,1-55 (3 cenere sulfurea), Explosion 35,1-85 (5), Greater Explosion 65,1-115 (10) |
| Healing | Lesser Heal 0-50 (1 ginseng), Heal 15,1-65 (3), Greater Heal 55,1-105 (7) |
| Poison | Lesser Poison 0-50 (1 nightshade), Poison 15,1-65 (2), Greater Poison 55,1-105 (4), Deadly Poison 90,1-140 (8) |
| Refresh | Refresh 15,1-65 (1 perla nera), Total Refresh 25,1-75 (5) |
| Strength | Strength 25,1-75 (2 radice di mandragora), Greater Strength 45,1-95 (5) |
| Nightsight | Nightsight 0-50 (5 seta di ragno) |

Un reagente conta sia venduto singolo sia a gruppi di dieci. Le pozioni create sono quelle che vendono i venditori, quindi si impilano insieme; le
pozioni semplici vendute dai venditori o trovate nei bottini funzionano come quelle con il nome (`scripts/common/potions.lua`), anche se una pozione
con il nome trovata in un forziere non si impila con loro.

## Cambiare le regole

- Le ricette sono [`data/crafts/alchemy.toml`](data-files/crafts.md); gli elenchi dei reagenti sono in
  `data/crafts/resources.toml`.
- L'attrezzo è il template con `script_id = "alchemy_tool"` (`scripts/items/alchemy_tool.lua`).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/alchemy.toml`,
`data/crafts/resources.toml`, `scripts/items/alchemy_tool.lua`, `scripts/common/potions.lua`,
`scripts/items/potion.lua`, `scripts/items/explosion_potion.lua`, `templates/items/magic/potions.toml` e
`templates/items/skills/tools/alchemy.toml`.

## Non ancora

Le pozioni delle ere successive (conflagration, confusion blast e le altre), i barili di pozioni.

## Vedi anche

- [Pozioni](potions.md)
- [Falegnameria](carpentry.md)
- [File dati dei mestieri](data-files/crafts.md)
