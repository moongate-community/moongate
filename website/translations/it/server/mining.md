<!-- translation: {"sourceHash":"2fac2ff914f75483ea5689c10d13b8510ed4dbd36144a1e1e1cd991ff958b45f","title":"Estrazione e fusione"} -->

# Estrazione e fusione

Un giocatore con un piccone o una pala estrae minerale di ferro dalla roccia, e lo fonde in lingotti a una forgia.
L'abilità Mining decide entrambe le cose e cresce con l'uso; il minerale di un luogo si esaurisce e ritorna con il tempo.

## Come si scava

1. Fai doppio clic su un piccone o una pala, in mano, nello zaino o a terra a portata di mano. Leggi "Where do
   you wish to dig?" e ricevi un cursore.
2. Scegli la roccia di una montagna o il pavimento di una grotta entro 2 caselle.
3. Il tuo personaggio colpisce una volta, e il risultato arriva 0,9 secondi dopo con il suono del piccone.

Devi trovarti ancora entro 2 caselle dal luogo quando arriva il risultato, e scavi in un luogo alla volta: un secondo
doppio clic nel frattempo non fa nulla.

| Leggi | Perché |
| --- | --- |
| `You can't mine while riding.` | Sei su una [cavalcatura](mounts.md): scendi prima |
| `You can't mine there.` | Il luogo non è roccia: erba, sabbia, una strada, o uno statico che non è il pavimento di una grotta |
| `You can't mine that.` | Hai scelto un oggetto o qualcuno |
| `That is too far away.` | La roccia è a più di 2 caselle |
| `You have moved too far away to continue mining.` | Ti sei allontanato prima del risultato |
| `There is no metal here to mine.` | Nel luogo non resta minerale: prova altrove, o torna più tardi |
| `Someone has gotten to the metal before you.` | L'ultimo minerale è stato preso mentre colpivi |
| `You loosen some rocks but fail to find any useable ore.` | La prova è fallita |
| `Your backpack is full, so the ore you mined is lost.` | Il tuo zaino non può prendere il mucchio: il minerale sparisce comunque dal luogo |

## Che cosa ottieni

La prova viene tirata sull'abilità Mining tra 0 e 100, quindi la probabilità di uno scavo riuscito è l'abilità stessa,
e l'abilità può crescere a ogni prova. Uno scavo riuscito dà un mucchio di minerale di ferro, che si unisce al mucchio dello
stesso tipo già nello zaino:

| Mucchio | Quanto spesso | Lingotti in cui si fonde |
| --- | --- | --- |
| Grande | 3 volte su 4 | 2 per ogni minerale |
| Medio, in due forme | 1 volta su 8 | 1 per ogni minerale |
| Piccolo | 1 volta su 8 | 1 ogni 2 minerali |

L'attrezzo non si consuma.

## Metalli

Un luogo di roccia è di un solo metallo, estratto di nuovo ogni volta che il minerale ritorna: la stessa roccia può dare oro oggi e ferro
domani. Circa metà dei luoghi è di ferro.

| Metallo | Luoghi su mille | Mining richiesto | Lo scavo viene provato tra | Fuso tra |
| --- | --- | --- | --- | --- |
| Ferro | 496 | 0 | 0 e 100 | 25 e 75 |
| Dull copper | 112 | 65 | 25 e 105 | 40 e 90 |
| Shadow iron | 98 | 70 | 30 e 110 | 45 e 95 |
| Copper | 84 | 75 | 35 e 115 | 50 e 100 |
| Bronze | 70 | 80 | 40 e 120 | 55 e 105 |
| Gold | 56 | 85 | 45 e 125 | 60 e 110 |
| Agapite | 42 | 90 | 50 e 130 | 65 e 115 |
| Verite | 28 | 95 | 55 e 135 | 70 e 120 |
| Valorite | 14 | 99 | 59 e 139 | 74 e 124 |

Chi ha il Mining del metallo ne scava il minerale uno scavo su due, e ferro l'altro; chi non ce l'ha scava sempre
ferro. Il minerale di un metallo è un mucchio grande del suo colore, che si fonde in lingotti di quel metallo, due per ogni minerale, provato
tra i limiti dell'ultima colonna. Un singolo minerale di un metallo che non si fonde viene bruciato.

## Fusione

1. Fai doppio clic su un mucchio di minerale, nello zaino o a terra entro 2 caselle. Leggi "Select the forge on which to
   smelt the ore, or another pile of ore with which to combine it."
2. Scegli una forgia entro 2 caselle: una posata come oggetto, o una che fa parte della mappa.

L'intero mucchio viene fuso in una volta. La prova viene tirata sull'abilità Mining tra 25 e 75 per il ferro (vedi [Metalli](#metals)
per gli altri): sotto 25 una fusione fallisce sempre, da 75 riesce sempre, e la prova può far crescere l'abilità.

| La fusione | Che cosa succede |
| --- | --- |
| Riesce | Il mucchio diventa lingotti di ferro nel tuo zaino, in base alla sua dimensione (la tabella sopra); un minerale piccolo dispari resta. Leggi "You smelt the ore removing the impurities and put the metal in your backpack." |
| Fallisce | Metà del mucchio brucia, arrotondata per difetto. Un mucchio di un solo minerale invece rimpicciolisce: uno grande diventa medio, uno medio piccolo. Leggi "You burn away the impurities but are left with less useable metal." |

Un singolo minerale piccolo risponde "There is not enough metal-bearing ore in this pile to make an ingot." Scegliere
qualcosa che non è una forgia risponde `That is not a forge.`, e una forgia a più di 2 caselle "That is too far
away." Il minerale viene tolto prima che i lingotti vengano dati: uno zaino senza posto per loro perde il metallo, e leggi `You have no room in your backpack for the ingots: the metal is lost.` Un mucchio che tieni sul cursore, o uno dentro un forziere a terra, risponde "The ore is too far away.": mettilo prima nello zaino o a terra.

## Il minerale di un luogo

Ogni mappa è divisa in zone di 8 caselle per 8. Una zona contiene da 10 a 34 minerali, estratti la prima volta che qualcuno vi scava. Uno
scavo riuscito ne toglie uno; una prova fallita non ne toglie. La zona torna piena, tutta in una volta, da 10 a 20 minuti dopo
il primo minerale preso.

Le zone sono tenute in memoria: dopo un riavvio ogni luogo è pieno. I numeri sono la risorsa `ore` di
[`harvest.toml`](data-files/harvest.md).

## Cambiare le regole

Le regole di uno scavo sono in `scripts/items/pickaxe.lua`: la distanza, il tempo, i mucchi e quanto spesso esce ciascuno,
i terreni che sono roccia e gli statici che sono il pavimento di una grotta. Quelle di una fusione sono in
`scripts/items/ore.lua`: la distanza, l'abilità, i lingotti di ogni mucchio e le grafiche che sono forge. Vedi
[Script forniti](scripting/shipped-scripts.md#pickaxelua-and-orelua). Un template scava con
`script_id = "pickaxe"` e viene fuso con `script_id = "ore"`.

## Root esistenti

Una root creata prima che l'estrazione esistesse ha bisogno di tre cose. Esegui `mgctl init`, che aggiunge i due script. Poi aggiungi la
risorsa `ore` al tuo `data/harvest.toml`, e gli script ai template: `script_id = "pickaxe"` ai
picconi e alle pale di `templates/items/skills/tools/mining.toml`, `script_id = "ore"` ai quattro mucchi di
minerale di ferro di `templates/items/skills/resources/mining.toml`. Oppure copia i tre file dalla distribuzione:
`mgctl init` non sostituisce mai un file che potresti aver modificato. `scripts/items/ore.lua` ora legge le forge da
`scripts/common/smithy.lua`: copialo insieme. Per i metalli, copia `scripts/common/metals.lua`, `templates/items/metals.toml` e
entrambi gli script di nuovo, e aggiungi le `[[resource.vein]]` del minerale al tuo `data/harvest.toml`: senza le vene ogni luogo è di ferro.

## Non ancora

Unire due mucchi in uno; attrezzi che si consumano; sabbia, pietra e gemme; rifondere un oggetto di metallo in lingotti.

## Vedi anche

- [Pesca](fishing.md) e [Taglio della legna](lumberjacking.md)
- [`harvest.toml`](data-files/harvest.md)
- [Abilità](skills.md)
- [Script forniti](scripting/shipped-scripts.md#pickaxelua-and-orelua)
