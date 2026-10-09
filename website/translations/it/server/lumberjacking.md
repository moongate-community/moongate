<!-- translation: {"sourceHash":"117d865c26aadf7f63a5fd4329a7b86eb8327389e7d7fa4145d0563ef7650730","title":"Taglio della legna"} -->

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
stessa, e l'abilità può crescere a ogni prova. Questo vale per il legno comune: gli altri [tipi di legno](#kinds-of-wood) sono più difficili. Un taglio riuscito dà 10 tronchi, che si uniscono a quelli già nello
zaino. L'ascia non si consuma.

Queste asce tagliano: l'accetta, l'ascia, l'ascia da battaglia, l'ascia doppia, l'ascia del boia, la grande ascia da
battaglia, l'ascia a due mani, l'ascia ornata e l'ascia da battaglia gargish. L'ascia gargish, le doppie asce corte e le asce da allenamento no, e un'ascia da guerra è una mazza.

## Tipi di legno

Un luogo è di un solo tipo di legno, estratto di nuovo ogni volta che la legna ritorna: gli stessi alberi possono dare quercia oggi e
legno comune domani. Circa metà dei luoghi è di legno comune.

| Tipo | Luoghi su mille | Lumberjacking richiesto | Il taglio viene provato tra |
| --- | --- | --- | --- |
| Comune | 490 | 0 | 0 e 100 |
| Quercia (oak) | 300 | 65 | 25 e 105 |
| Frassino (ash) | 100 | 80 | 40 e 120 |
| Tasso (yew) | 50 | 95 | 55 e 135 |
| Heartwood | 30 | 100 | 60 e 140 |
| Bloodwood | 20 | 100 | 60 e 140 |
| Frostwood | 10 | 100 | 60 e 140 |

Chi ha l'abilità del tipo ne ottiene i tronchi un taglio su due, e tronchi comuni l'altro; chi non ce l'ha ottiene sempre
tronchi comuni. Un taglio per i tronchi di un tipo è più difficile di uno comune: la quercia con 65 di abilità riesce una volta su due.
I tronchi di un tipo ne hanno il colore e si impilano separati dagli altri.

## Ritrovamenti rari

Con 100 di Lumberjacking un taglio riuscito può dare una cosa in più insieme ai tronchi, circa un taglio su sei:

| Ritrovamento | Tagli su cento |
| --- | --- |
| Bark fragment | 10 |
| Luminescent fungi | 3 |
| Switch | 2 |
| Parasitic plant | 1 |
| Brilliant amber | 0.1 |

Per ora non ci si costruisce niente.

## Assi

Fai doppio clic sull'ascia che hai in mano e scegli i tronchi nel tuo zaino invece di un albero: l'intera pila viene segata in assi dello stesso tipo, una per ogni tronco, subito e senza prova di abilità. I tronchi di un tipo richiedono il Lumberjacking del tipo, come tagliarli: chi non ce l'ha legge "You cannot work this strange and unusual wood." Anche i tronchi in una borsa dello zaino vengono segati. Tronchi a terra, in un forziere, nella tua cassetta in banca o sul tuo cursore rispondono "This item must be in your backpack to be used."

## Legnetti

Fai doppio clic su un coltello, un pugnale o una spada che porti con te e scegli un albero entro 2 caselle: ne stacchi un legnetto, subito e senza prova di abilità. Nel luogo deve restare della legna, e ogni legnetto ne toglie un taglio, come un taglio d'ascia: un luogo ne dà qualcuno, poi nessuno finché la legna non ritorna. Scegliere qualsiasi altra cosa risponde "You can't use a bladed item on that!"

## Le asce in combattimento

Chi abbatte alberi colpisce più forte con un'ascia: 1% di danno in più ogni 5 punti di Lumberjacking, e un altro 10% a 100, quindi 30% in tutto per un maestro. Vale per ogni arma con `weapon_type = "axe"`, le asce che tagliano e quelle che non tagliano. L'abilità non viene provata da un colpo: cresce sugli alberi. Vedi [Combattimento](combat.md).

## La legna di un luogo

Ogni mappa è divisa in zone di 4 caselle per 4. Una zona contiene da 2 a 4 tagli, estratti la prima volta che qualcuno vi taglia:
gli alberi di una zona li condividono. Un taglio riuscito ne toglie uno; una prova fallita non ne toglie. La zona torna piena,
tutta in una volta, da 20 a 30 minuti dopo il primo taglio.

Le zone sono tenute in memoria: dopo un riavvio ogni luogo è pieno. I numeri sono la risorsa `wood` di
[`harvest.toml`](data-files/harvest.md), e i tipi di legno le sue vene.

## Cambiare le regole

Le regole sono in `scripts/items/axe.lua` (la distanza, i colpi, i tronchi di un taglio, le assi, la tabella dei tipi di legno e quella dei ritrovamenti rari), `scripts/items/blade.lua` (i legnetti) e `scripts/common/trees.lua` (le grafiche che contano
come alberi). Vedi [Script forniti](scripting/shipped-scripts.md#axelua). Un template taglia con `script_id = "axe"`.

## Root esistenti

Una root creata prima che il taglio della legna esistesse ha bisogno di due cose. Esegui `mgctl init`, che aggiunge lo script. Poi aggiungi la
risorsa `wood` al tuo `data/harvest.toml` e `script_id = "axe"` alle asce base di
`templates/items/gear/weapons/axes.toml`, oppure copia entrambi i file dalla distribuzione: `mgctl init` non sostituisce mai
un file che potresti aver modificato. Per assi e legnetti, copia di nuovo `scripts/items/axe.lua` se lo avevi già, e dai `script_id = "blade"` ai tuoi coltelli, pugnali e spade, oppure copia i file di `templates/items/gear/weapons` dalla distribuzione. Per i tipi di legno e i ritrovamenti rari, copia `templates/items/woods.toml`, `scripts/items/axe.lua` e `scripts/common/trees.lua`, e aggiungi le `[[resource.vein]]` della legna al tuo `data/harvest.toml`: senza le vene ogni luogo è di legno comune, e senza i template un taglio dà tronchi comuni.

## Non ancora

Ciò che si costruisce con i tipi di legno e con i ritrovamenti rari: la falegnameria e gli altri mestieri.

## Vedi anche

- [Pesca](fishing.md)
- [`harvest.toml`](data-files/harvest.md)
- [Abilità](skills.md)
- [Script forniti](scripting/shipped-scripts.md#axelua)
