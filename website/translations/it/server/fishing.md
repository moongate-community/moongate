<!-- translation: {"sourceHash":"03c07852b823cdcddd9f84fc775d22dca1cb9a35dd80dfd9f1123dd0cee357ce","title":"Pesca"} -->

# Pesca

Un giocatore con una canna da pesca tira fuori pesci dall'acqua. L'abilità Fishing decide che cosa esce e cresce
con l'uso; i pesci di un luogo si esauriscono e ritornano con il tempo.

## Come si pesca

1. Fai doppio clic su una canna da pesca che porti con te. Leggi "What water do you want to fish in?" e ricevi un cursore.
2. Scegli dell'acqua entro 4 caselle, in vista.
3. Il tuo personaggio lancia, l'acqua schizza un momento dopo, e 8 secondi dopo il lancio arriva il risultato.

Devi trovarti ancora entro 4 caselle dall'acqua quando gli 8 secondi finiscono, e peschi in un'acqua alla volta: un
secondo doppio clic nel frattempo risponde "You are already fishing."

| Leggi | Perché |
| --- | --- |
| `You need water to fish in!` | Ciò che hai scelto non è acqua: terra asciutta, un oggetto o qualcuno |
| `You need to be closer to the water to fish!` | L'acqua è a più di 4 caselle o non è in vista, oppure ti sei allontanato prima del risultato |
| `The fish don't seem to be biting here.` | Nel luogo non restano pesci: prova altrove, o torna più tardi |
| `You fish a while, but fail to catch anything.` | La prova è fallita, oppure non è uscito nulla |
| `You do not have room in your backpack for a fish.` | Il tuo zaino non può prendere la presa, che resta in acqua |

## Che cosa esce

La prova viene tirata sull'abilità Fishing tra 0 e 100, quindi la probabilità di tirare fuori qualcosa è l'abilità stessa,
e l'abilità può crescere a ogni prova. Quando la prova riesce:

| Cosa | Probabilità | Ad abilità 0 | Ad abilità 100 |
| --- | --- | --- | --- |
| Una calzatura: stivali, sandali, scarpe o stivali alti | (105 − abilità) / 525 | 20% | circa 1% |
| Niente | (200 − abilità) / 400 | 50% | 25% |
| Un pesce, uno dei quattro | il resto | | |

La presa finisce nello zaino. La canna non si consuma.

## I pesci di un luogo

Ogni mappa è divisa in zone di 8 caselle per 8. Una zona contiene da 5 a 15 pesci, estratti la prima volta che qualcuno vi pesca.
Una presa ne toglie uno; una prova fallita e "niente" non ne tolgono. La zona torna piena, tutta in una volta, da 10 a 20 minuti
dopo la prima presa.

Le zone sono tenute in memoria: dopo un riavvio ogni luogo è pieno. I numeri sono in
[`harvest.toml`](data-files/harvest.md).

## Cambiare le regole

Le regole sono in `scripts/items/fishing_pole.lua`: la distanza, i secondi, che cosa esce e quanto spesso. Vedi
[Script forniti](scripting/shipped-scripts.md#fishing_polelua). Un template pesca con
`script_id = "fishing_pole"`.

## Non ancora

Pesci magici e pesci grossi, l'acqua profonda e ciò che ne viene (reti speciali, messaggi in bottiglia, serpenti marini),
tagliare un pesce in tranci, le esche, e la regola contro la pesca a cavallo, che aspetta le cavalcature.

## Vedi anche

- [`harvest.toml`](data-files/harvest.md)
- [Abilità](skills.md)
- [Script forniti](scripting/shipped-scripts.md#fishing_polelua)
