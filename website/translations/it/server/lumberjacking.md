<!-- translation: {"sourceHash":"ac075f43338b4176ef25fdcfdf22d512c402196d3a68d110989d7266c48816a0","title":"Taglio della legna"} -->

# Taglio della legna

Un giocatore con un'ascia abbatte alberi per ricavarne tronchi. L'abilità Lumberjacking decide se un taglio riesce e cresce
con l'uso; la legna di un luogo si esaurisce e ritorna con il tempo.

## Come si taglia

1. Impugna un'ascia: una nello zaino risponde "The axe must be equipped for any serious wood
   chopping."
2. Fai doppio clic sull'ascia. Leggi "What do you want to use this item on?" e ricevi un cursore.
3. Scegli un albero entro 2 caselle.
4. Il tuo personaggio colpisce da una a tre volte, più spesso due, un colpo ogni 1,6 secondi. Il risultato arriva con
   l'ultimo colpo: 0,9, 2,5 o 4,1 secondi dopo il primo.

Devi trovarti ancora entro 2 caselle dall'albero, con l'ascia in mano, quando arriva l'ultimo colpo, e
tagli un albero alla volta: un secondo doppio clic nel frattempo non fa nulla.

| Leggi | Perché |
| --- | --- |
| `You can't use an axe on that.` | Ciò che hai scelto non è un albero: il terreno, una roccia, un oggetto o qualcuno |
| `That is too far away.` | L'albero è a più di 2 caselle, oppure ti sei allontanato prima dell'ultimo colpo |
| `There's not enough wood here to harvest.` | Nel luogo non resta legna: prova altrove, o torna più tardi |
| `You hack at the tree for a while, but fail to produce any useable wood.` | La prova è fallita |
| `You can't place any wood into your backpack!` | Il tuo zaino non può prendere i tronchi, che vanno persi: la legna sparisce comunque dal luogo |

## Che cosa ottieni

La prova viene tirata sull'abilità Lumberjacking tra 0 e 100, quindi la probabilità di un taglio riuscito è l'abilità
stessa, e l'abilità può crescere a ogni prova. Un taglio riuscito dà 10 tronchi, che si uniscono a quelli già nello
zaino. L'ascia non si consuma.

Queste asce tagliano: l'accetta, l'ascia, l'ascia da battaglia, l'ascia doppia, l'ascia del boia, la grande ascia da
battaglia, l'ascia a due mani, l'ascia ornata e l'ascia da battaglia gargish. L'ascia gargish, le doppie asce corte e le asce da allenamento no, e un'ascia da guerra è una mazza.

## Assi

Fai doppio clic sull'ascia che hai in mano e scegli i tronchi nel tuo zaino invece di un albero: l'intera pila viene segata in assi, una per ogni tronco, subito e senza prova di abilità. Tronchi a terra, in un forziere o sul tuo cursore rispondono "This item must be in your backpack to be used."

## Legnetti

Fai doppio clic su un coltello, un pugnale o una spada che porti con te e scegli un albero entro 2 caselle: ne stacchi un legnetto, subito e senza prova di abilità. Nel luogo deve restare della legna, ma i legnetti non ne tolgono. Scegliere qualsiasi altra cosa risponde "You can't use a bladed item on that!"

## La legna di un luogo

Ogni mappa è divisa in zone di 4 caselle per 4. Una zona contiene da 2 a 4 tagli, estratti la prima volta che qualcuno vi taglia:
gli alberi di una zona li condividono. Un taglio riuscito ne toglie uno; una prova fallita non ne toglie. La zona torna piena,
tutta in una volta, da 20 a 30 minuti dopo il primo taglio.

Le zone sono tenute in memoria: dopo un riavvio ogni luogo è pieno. I numeri sono la risorsa `wood` di
[`harvest.toml`](data-files/harvest.md).

## Cambiare le regole

Le regole sono in `scripts/items/axe.lua` (la distanza, i colpi, i tronchi di un taglio, le assi), `scripts/items/blade.lua` (i legnetti) e `scripts/common/trees.lua` (le grafiche che contano
come alberi). Vedi [Script forniti](scripting/shipped-scripts.md#axelua). Un template taglia con `script_id = "axe"`.

## Root esistenti

Una root creata prima che il taglio della legna esistesse ha bisogno di due cose. Esegui `mgctl init`, che aggiunge lo script. Poi aggiungi la
risorsa `wood` al tuo `data/harvest.toml` e `script_id = "axe"` alle asce base di
`templates/items/gear/weapons/axes.toml`, oppure copia entrambi i file dalla distribuzione: `mgctl init` non sostituisce mai
un file che potresti aver modificato.

## Non ancora

I tipi di legno (quercia, frassino, tasso e i più rari), i ritrovamenti rari, e il bonus delle asce in combattimento dato dall'abilità.

## Vedi anche

- [Pesca](fishing.md)
- [`harvest.toml`](data-files/harvest.md)
- [Abilità](skills.md)
- [Script forniti](scripting/shipped-scripts.md#axelua)
