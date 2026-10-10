<!-- translation: {"sourceHash":"edfb39bd81eb1349c6cbab4317468748146a52099b39ba839e0efc1c4f39a3d7","title":"Mestieri"} -->

# Mestieri

`data/crafts` contiene i mestieri con cui i giocatori creano oggetti, un file per mestiere (oggi `carpentry.toml`, `blacksmithing.toml`, `tailoring.toml`, `tinkering.toml`, `fletching.toml`, `cooking.toml`, `cartography.toml`, `alchemy.toml` e `inscription.toml`), e
`resources.toml`, gli elenchi di template di oggetti che una ricetta può richiedere. Vedi [Falegnameria](../carpentry.md) per le regole e
[Fabbro](../blacksmithing.md). Ciò a cui un mestiere deve stare vicino, come l'incudine e la forgia del fabbro, non è un dato:
è la tabella `NEEDS` di `scripts/common/crafting.lua`, che può indicarlo solo per alcuni gruppi (il forno di Baking, il
fuoco di Barbecue).

## Un mestiere

```toml
id = "carpentry"
name = "Carpentry"
skill = "carpentry"
sound = 0x023D

[[group]]
name = "Chairs"

[[group.recipe]]
name = "Stool"
item = "0x0a2b_a_stool"
skill_min = 11.0
skill_max = 36.0
resources = [{ resource = "wood", amount = 9 }]
skills = []
```

| Campo | Significato |
| --- | --- |
| `id` | Il nome con cui gli script lo aprono, un identificatore in minuscolo. Ogni mestiere una volta sola. |
| `name` | Ciò che mostra il suo gump. |
| `skill` | L'abilità principale di ogni ricetta, con il nome di `data/skills.toml`. |
| `sound` | Suonato a ciascuno dei due colpi. |
| `[[group]]` | Un gruppo del gump, in ordine: `name`. |
| `[[group.recipe]]` | Una ricetta del gruppo, in ordine. |
| `name` | Ciò che mostra il gump. |
| `item` | Il template dell'oggetto creato. |
| `skill_min` | Il minimo dell'abilità principale per provarla: lì la probabilità è una su due. Da -50 a 150; sotto 0 viene sempre tentata (il primo cerchio di [Inscription](../inscription.md) parte da -25). |
| `skill_max` | L'abilità a cui non fallisce mai. Non sotto `skill_min`, al massimo 150. UOX3 ne mette alcune sopra 100 (la tunica borchiata, il teschio con candela): quelle non diventano mai certe, né eccezionali sotto `skill_max - 60`. |
| `resources` | Cosa richiede: `resource` è un elenco di `resources.toml` o un template di oggetto, `amount` almeno 1. Almeno uno. |
| `skills` | Altre abilità richieste: `skill`, `min` (il minimo per provarla) e `max`, rispetto a cui viene provata. |
| `spell` | Facoltativo. La chiave di un incantesimo di `data/spells.toml` che chi crea deve avere in un libro che indossa o porta nello zaino, altrimenti "You don't have that spell!". Un identificatore in minuscolo; omesso per nessuno. |
| `mana` | Facoltativo, 0 o più. Il mana che costa un tentativo, controllato all'inizio e al secondo colpo, pagato una volta da un tentativo che viene fatto (anche un fallimento). |

## resources.toml

```toml
[[resource]]
id = "wood"
templates = ["0x1bd7_board", "0x1bda_board"]
```

| Campo | Significato |
| --- | --- |
| `[[resource]]` | Un elenco. |
| `id` | Il nome che le ricette gli danno, un identificatore in minuscolo. Ogni elenco una volta sola. |
| `templates` | I template di oggetti che contano per esso, almeno uno. |

`wood` sono le assi comuni e `metal` i lingotti di ferro: un tipo di legno o di metallo scelto nel gump prende invece le assi o i
lingotti di quel tipo, come dicono `scripts/common/woods.lua` e `scripts/common/metals.lua`.

## Caricamento

Il server si ferma all'avvio, indicando il file, per: un id che non è un identificatore in minuscolo o è usato due volte, un'abilità
sconosciuta, un gruppo o una ricetta senza nome, un oggetto o materiale che non è né un template di oggetto né un elenco, una
quantità sotto 1, una ricetta che non richiede nulla, limiti di abilità fuori da -50 a 150 (il massimo da 0) o il minimo sopra il massimo, uno `spell` che non è un identificatore in minuscolo, un `mana` sotto 0, oppure un elenco
senza template o che ne nomina uno inesistente, un mestiere senza nome, senza `[[group]]` o con un gruppo senza ricette. Senza la cartella non si può creare nulla.

## Da dove vengono i file

Sono convertiti dai menu di creazione di UOX3:

```bash
uv run --project tools/convert moongate-convert uox-crafts --source <UOX3>/data/dfndata/create \
    --items moongate_root/templates/items --destination moongate_root/data/crafts
```

Il convertitore esclude i gruppi che creano deed e la ricetta delle assi, trasforma i decimi di abilità di UOX3 in
punti, e conta come legno solo le assi. UOX3 annida i suoi menu (Blacksmithing, Armor, Ringmail): ogni menu che contiene
ricette diventa un gruppo, nell'ordine in cui i menu si incontrano. Il nome di una ricetta inizia con la maiuscola, e una seconda ricetta
con lo stesso nome (un cucchiaio girato dall'altra parte) si distingue con un numero: "Spoon 2". La ricetta Tinker's tools crea
gli attrezzi da tinker, non la cassetta degli attrezzi da 10 pietre di UOX3. Le ricette proprie di un menu radice (gli archi) formano un
primo gruppo, una seconda radice (le frecce e i dardi dell'attrezzo da fletcher) si visita dopo, e i lotti di UOX3 (cinque, venti, cinquanta) sono esclusi. Un oggetto venduto singolo e a
pile sotto una sola grafica, come un reagente (0x0f85_ginseng e 0x0f85_10_ginseng), diventa un elenco a sé (`ginseng`);
una ricetta di alchimia richiede anche una bottiglia vuota, che UOX3 non mette.

L'inscription non è nei menu di UOX3 come dati che il server possa usare: `uox-crafts --spells moongate_root/data/spells.toml`
la costruisce da `spells.toml` (una pergamena per ogni incantesimo abilitato, nel gruppo del suo cerchio, i reagenti
dell'incantesimo, una pergamena vuota dell'elenco `blank_scrolls`, il mana e la finestra classici del cerchio), e scrive `spell` e `mana`.
