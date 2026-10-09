<!-- translation: {"sourceHash":"2faaab1cc94b8de1b2e62e1c55fdc94fe2b5d2e5ca8dcfd0187eec4dd1af99a2","title":"Falegnameria"} -->

# Falegnameria

Un giocatore con un attrezzo da falegname crea mobili, contenitori, bastoni e strumenti dalle assi. L'abilità Carpentry
decide se un tentativo riesce e cresce con l'uso. Le regole sono condivise da tutti i mestieri che verranno: la
falegnameria è il primo.

## Come creare qualcosa

1. Tieni un attrezzo da falegname nello zaino, o in una borsa al suo interno: una sega, una sega a coda di rondine, una
   pialla (moulding, jointing o smoothing), degli scalpelli, un coltello a petto (draw knife), un froe o un inshave. Un attrezzo a terra o nella cassetta in banca risponde
   "This item must be in your backpack to be used."
2. Fai doppio clic sull'attrezzo. Si apre il gump di creazione: i gruppi a sinistra, le ricette del gruppo a destra,
   dieci per pagina.
3. Premi il pulsante prima di una ricetta per crearla, o quello dopo per vederne la scheda: l'oggetto, cosa richiede e
   quanto ne porti di ciascuno, le abilità richieste e la tua probabilità.
4. Due colpi dopo, a 1,25 secondi l'uno dall'altro, l'oggetto è nel tuo zaino e il gump si riapre con l'esito.

La riga in basso mostra il legno che lavori e quante assi di quel tipo porti. Change elenca i tipi di legno.

## Cosa richiede un tentativo

| Controllo | Cosa leggi |
| --- | --- |
| Carpentry, e ogni altra abilità della ricetta, almeno al suo minimo | "You don't have the required skills to attempt this item." |
| Un tipo di legno che sai lavorare | "You cannot work this strange and unusual wood." |
| Ogni materiale nello zaino e nelle sue borse: non nella cassetta in banca, non una pila sul cursore | "You do not have sufficient wood to make that.", "You don't have enough cloth to make that." oppure "You don't have the components needed to make that." |
| Non stai già creando qualcosa | "You must wait to perform another action." |

Quando un tentativo viene rifiutato non si toglie nulla. Al secondo colpo si ricontrolla tutto: assi spostate nel
frattempo non creano nulla.

## La probabilità

Ogni ricetta ha un minimo e un massimo di Carpentry. Al minimo la probabilità è una su due; cresce in linea retta fino
alla certezza al massimo. Uno sgabello chiede da 11 a 36: a 23,5 la probabilità è tre su quattro. Ogni prova può far crescere le abilità della ricetta.

- **Riuscita**: toglie tutti i materiali e crea l'oggetto nel tuo zaino, "You create the item.". Uno zaino senza
  spazio li toglie comunque e mette l'oggetto ai tuoi piedi.
- **Fallimento**: toglie metà di ogni materiale, arrotondata per difetto, "You failed to create the item, and some of your materials
  are lost."

## Tipi di legno

Le assi comuni sono quelle predefinite. Un'asse di un altro tipo, tagliata da un [taglialegna](lumberjacking.md) e segata con l'ascia,
richiede tanto Carpentry quanto Lumberjacking, e dà il suo colore all'oggetto.

| Tipo | Carpentry richiesto |
| --- | --- |
| Comune | 0 |
| Quercia (oak) | 65 |
| Frassino (ash) | 80 |
| Tasso (yew) | 95 |
| Heartwood, bloodwood, frostwood | 100 |

Il tipo scelto vale solo per il legno: la stoffa di un'arpa resta stoffa. Il tipo resta fino a un riavvio.

## Le ricette

42 ricette in sei gruppi, convertite da UOX3: Chairs, Tables, Containers, Other Items, Staves & Poles e Musical
items. Alcune:

| Ricetta | Carpentry | Richiede |
| --- | --- | --- |
| Barrel Staves | da 0 a 25 | 5 legno |
| Stool | da 11 a 36 | 9 legno |
| Wooden Box | da 21 a 46 | 10 legno |
| Wooden Shield | da 52,6 a 80 | 9 legno |
| Chest | da 73,6 a 98,6 | 15 legno |
| Quarter Staff | da 73,6 a 100 | 6 legno |
| Lute | da 68,4 a 93,4, Musicianship 45 | 25 legno, 10 stoffa |
| Fishing Pole | da 68,4 a 93,4, Tailoring 68,4 | 5 legno, 5 stoffa |

I gruppi delle aggiunte (casa, fabbro, sarto e cucina) creano dei deed, che non servono finché non ci sono le case:
sono esclusi. Lo è anche la ricetta delle assi, che l'ascia sega già.

## Cambiare le regole

- Le ricette sono [`data/crafts`](data-files/crafts.md).
- Le regole di un tentativo sono `scripts/common/crafting.lua`; i tipi di legno `scripts/common/woods.lua`, condiviso con
  l'ascia.
- Il gump è `templates/gumps/craft_menu.xml` con `scripts/gumps/craft_menu.lua`.
- Gli attrezzi sono i template con `script_id = "carpentry_tool"`. Vedi [Script forniti](scripting/shipped-scripts.md#craftinglua-and-carpentry_toollua).

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione `data/crafts/`,
`scripts/common/crafting.lua`, `scripts/common/woods.lua`, `scripts/items/carpentry_tool.lua`,
`scripts/items/axe.lua` (ora legge `woods.lua`), `templates/gumps/craft_menu.xml`, `scripts/gumps/craft_menu.lua`,
e `templates/items/skills/tools/carpenty.toml`, oppure dai `script_id = "carpentry_tool"` ai tuoi attrezzi da falegname.

## Non ancora

Oggetti eccezionali e marchio del creatore, attrezzi che si consumano, ricreare l'ultimo oggetto, le aggiunte, la
riparazione e gli altri mestieri.

## Vedi anche

- [Taglio della legna](lumberjacking.md)
- [File dati dei mestieri](data-files/crafts.md)
- [Script forniti](scripting/shipped-scripts.md#craftinglua-and-carpentry_toollua)
