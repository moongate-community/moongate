<!-- translation: {"sourceHash":"73e93cd662c6f81bd03da3b427a28c5e8dfa6e349dd81c9ba23698684c7a4116","title":"Mestieri"} -->

# Mestieri

`data/crafts` contiene i mestieri con cui i giocatori creano oggetti, un file per mestiere (oggi `carpentry.toml`, `blacksmithing.toml` e `tailoring.toml`), e
`resources.toml`, gli elenchi di template di oggetti che una ricetta può richiedere. Vedi [Falegnameria](../carpentry.md) per le regole e
[Fabbro](../blacksmithing.md). Ciò a cui un mestiere deve stare vicino, come l'incudine e la forgia del fabbro, non è un dato:
è la tabella `NEEDS` di `scripts/common/crafting.lua`.

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
| `skill_min` | Il minimo dell'abilità principale per provarla: lì la probabilità è una su due. Da 0 a 150. |
| `skill_max` | L'abilità a cui non fallisce mai. Non sotto `skill_min`, al massimo 150. |
| `resources` | Cosa richiede: `resource` è un elenco di `resources.toml` o un template di oggetto, `amount` almeno 1. Almeno uno. |
| `skills` | Altre abilità richieste: `skill`, `min` (il minimo per provarla) e `max`, rispetto a cui viene provata. |

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
quantità sotto 1, una ricetta che non richiede nulla, limiti di abilità fuori da 0 a 150 o il minimo sopra il massimo, oppure un elenco
senza template o che ne nomina uno inesistente, un mestiere senza nome, senza `[[group]]` o con un gruppo senza ricette. Senza la cartella non si può creare nulla.

## Da dove vengono i file

Sono convertiti dai menu di creazione di UOX3:

```bash
uv run --project tools/convert moongate-convert uox-crafts --source <UOX3>/data/dfndata/create \
    --items moongate_root/templates/items --destination moongate_root/data/crafts
```

Il convertitore esclude i gruppi che creano deed e la ricetta delle assi, trasforma i decimi di abilità di UOX3 in
punti, e conta come legno solo le assi. UOX3 annida i suoi menu (Blacksmithing, Armor, Ringmail): ogni menu che contiene
ricette diventa un gruppo, nell'ordine in cui i menu si incontrano. Il nome di una ricetta inizia con la maiuscola.
