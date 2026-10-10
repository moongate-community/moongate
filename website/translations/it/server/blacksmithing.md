<!-- translation: {"sourceHash":"6050456ad4708e7c100be4e05890bb1de2e7553fa8dc188aacc6592e02eab04c","title":"Fabbro"} -->

# Fabbro

Un fabbro vicino a un'incudine e a una forgia forgia armi, armature e scudi dai lingotti di ferro. Le regole sono quelle di
ogni mestiere: vedi [Falegnameria](carpentry.md) per la probabilità, i fallimenti, gli oggetti eccezionali, il marchio del creatore, gli attrezzi che
si consumano e Make last.

## Come forgiare

1. Stai entro 2 caselle da un'incudine e da una forgia: oggetti a terra, oppure le incudini e le forge che la mappa stessa ha nelle sue
   fucine.
2. Porta lingotti di ferro nel tuo zaino o in una sua borsa.
3. Fai doppio clic su un martello da fabbro, una mazza o delle tenaglie, nello zaino, in una sua borsa o in mano. Si apre il gump
   di creazione del fabbro.
4. Premi il pulsante prima di una ricetta, oppure apri la sua scheda per vedere i lingotti, le abilità e la tua probabilità.

Lontano da un'incudine o da una forgia il gump si apre comunque, ma forgiare risponde "You must be near an anvil and a forge to
smith items." e non toglie nulla. Incudine e forgia vengono cercate di nuovo al secondo colpo.

## Le ricette

66 ricette in undici gruppi, convertite da UOX3, di tutte le ere: Ringmail, Chainmail, Platemail, Helmets, Shields, Bladed,
AOS Weapons, Axes, Polearms, Bashing e SE Weapons. Alcune:

| Ricetta | Blacksmithy | Richiede |
| --- | --- | --- |
| Buckler | da 0 a 10 | 10 metallo |
| Ringmail gloves | da 12 a 62 | 10 metallo |
| Platemail | da 75 a 125 | 25 metallo |
| Dagger | da 0 a 50 | 3 metallo |
| Longsword | da 28 a 78 | 12 metallo |
| War hammer | da 34,2 a 84 | 16 metallo |

La scheda di ogni ricetta nel gump ne mostra i numeri. Il metallo sono i lingotti di ferro; il tessen chiede anche stoffa e Tailoring.

## Armi e armature eccezionali

Un'arma eccezionale fa il 20% di danno in più; un pezzo d'armatura eccezionale dà 8 di armatura in più (uno scudo per ora
non conta nulla in combattimento). Vedi
[Combattimento](combat.md#weapons-and-armor). Un oggetto eccezionale è non comune, e raro quando porta il marchio del creatore: il suo
tooltip mostra la rarità nel suo colore.

## Cambiare le regole

- Le ricette sono [`data/crafts/blacksmithing.toml`](data-files/crafts.md).
- Le incudini e le forge sono `scripts/common/smithy.lua`, condiviso con la fusione; ciò a cui un mestiere deve stare vicino è la
  tabella `NEEDS` di `scripts/common/crafting.lua`.
- Gli attrezzi sono i template con `script_id = "smithing_tool"` (`scripts/items/smithing_tool.lua`).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/blacksmithing.toml`,
`scripts/common/crafting.lua`, `scripts/common/smithy.lua`, `scripts/items/smithing_tool.lua` e
`scripts/items/ore.lua` (ora legge `smithy.lua`), e i file degli attrezzi: `templates/items/skills/tools/blacksmithy.toml`,
`templates/items/gear/weapons/maces_hammers.toml` e `templates/items/misc/bod_rewards_blacksmith.toml`, oppure dai
`script_id = "smithing_tool"` ai tuoi martelli, mazze e tenaglie. Armi e armature rese eccezionali prima di
questa versione diventano subito più forti e mantengono la rarità che avevano.

## Non ancora

Metalli colorati (da dull copper a valorite), riparazione, rifondere gli oggetti in lingotti, e le mosse speciali delle armi AOS e
SE.

## Vedi anche

- [Falegnameria](carpentry.md)
- [Estrazione e fusione](mining.md)
- [File dati dei mestieri](data-files/crafts.md)
