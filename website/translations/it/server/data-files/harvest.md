<!-- translation: {"sourceHash":"45514080d7ac73e6531d81d98ac1e67da948a87160336c0b873ddcc5f1aa9521","title":"Raccolta"} -->

# Raccolta

`harvest.toml` elenca ciò che si raccoglie dal mondo e si esaurisce: i pesci della [pesca](../fishing.md), la legna del [taglio della legna](../lumberjacking.md) e il minerale dell'[estrazione](../mining.md).

```toml
[[resource]]
id = "fish"
area = 8
amount_min = 5
amount_max = 15
respawn_min_minutes = 10
respawn_max_minutes = 20
```

| Campo | Significato |
| --- | --- |
| `[[resource]]` | Uno per ogni cosa raccolta. |
| `id` | Il nome con cui gli script la chiedono, un identificatore in minuscolo: `harvest.take("fish", map, x, y)`. Ognuno usato una volta sola. |
| `area` | Quante caselle misura il lato di una zona, da 1 a 256. Ogni casella di una zona ne condivide la quantità. |
| `amount_min` | Il minimo che contiene una zona piena, almeno 1. |
| `amount_max` | Il massimo che contiene una zona piena. |
| `respawn_min_minutes` | I minuti minimi prima che una zona torni piena. 0 se omesso: torna piena subito. |
| `respawn_max_minutes` | I minuti massimi, fino a 10080 (una settimana). Uguale al minimo se omesso. |

Una risorsa può avere dei tipi, le sue vene: una zona è di uno solo di essi.

```toml
[[resource]]
id = "wood"
area = 4
amount_min = 2
amount_max = 4

[[resource.vein]]
id = "plain"
weight = 490

[[resource.vein]]
id = "oak"
weight = 300
```

| Campo | Significato |
| --- | --- |
| `[[resource.vein]]` | Una per tipo, sotto la sua risorsa. Omettile per una risorsa di un solo tipo. |
| `id` | Il nome con cui gli script la ottengono, un identificatore in minuscolo: `harvest.vein("wood", map, x, y)` dà `"oak"`. Ognuno usato una volta sola nella sua risorsa. |
| `weight` | Quanto spesso una zona è di questo tipo, rispetto ai pesi delle altre vene della risorsa. Da 1 a 1000000. |

Il server estrae soltanto la vena; che cosa dà una vena e che cosa richiede è compito dello script, come i tipi di legno di `scripts/items/axe.lua`.

## Come vive una zona

Ogni mappa è divisa in zone quadrate della dimensione della risorsa. Una zona viene estratta piena, tra le due quantità, la prima
volta che uno script la chiede. Una presa la abbassa di uno. Alla prima presa da una zona piena viene estratto il momento del suo
riempimento, tra i due tempi di ricomparsa; quando arriva, la zona viene estratta di nuovo piena, tutta in una volta. Una zona di una risorsa con vene estrae la sua vena ogni volta che viene estratta piena.

Le zone sono tenute in memoria e non salvate: dopo un riavvio ogni luogo è pieno.

Una risorsa con un `id` non valido o usato due volte, un'area fuori da 1 a 256, una quantità sotto 1, un minimo sopra il
massimo, un tempo negativo o superiore a una settimana, oppure una vena con un `id` non valido, usato due volte nella sua risorsa o con un peso fuori da 1 a 1000000 ferma il server all'avvio, indicando il file. Un file senza `[[resource]]` viene caricato con un avviso.

## Senza il file

Una root creata prima che questo file esistesse non ce l'ha: non si raccoglie nulla, e le canne da pesca dicono che i pesci non
abboccano. Esegui `mgctl init`, oppure copia `data/harvest.toml` dalla distribuzione, e vedi [Root esistenti](../fishing.md#existing-roots) per le canne.

## Dagli script

```lua
if harvest.take("fish", map, x, y) then
    -- one fish less in that area
end
```

`harvest.has(id)`, `harvest.amount(id, map, x, y)` e `harvest.take(id, map, x, y)`: vedi il
[riferimento Lua](https://moongate.sh/lua/harvest/).
