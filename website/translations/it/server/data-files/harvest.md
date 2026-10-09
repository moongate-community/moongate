<!-- translation: {"sourceHash":"51fc17e58037e767de691315d4f3674421fbbbc516c6b3e6d2e02bd7da04ae7e","title":"Raccolta"} -->

# Raccolta

`harvest.toml` elenca ciò che si raccoglie dal mondo e si esaurisce: oggi i pesci della [pesca](../fishing.md),
più avanti minerale e legna.

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
| `respawn_max_minutes` | I minuti massimi. Uguale al minimo se omesso. |

## Come vive una zona

Ogni mappa è divisa in zone quadrate della dimensione della risorsa. Una zona viene estratta piena, tra le due quantità, la prima
volta che uno script la chiede. Una presa la abbassa di uno. Alla prima presa da una zona piena viene estratto il momento del suo
riempimento, tra i due tempi di ricomparsa; quando arriva, la zona viene estratta di nuovo piena, tutta in una volta.

Le zone sono tenute in memoria e non salvate: dopo un riavvio ogni luogo è pieno.

Una risorsa con un `id` non valido o usato due volte, un'area fuori da 1 a 256, una quantità sotto 1, un minimo sopra il
massimo o un tempo negativo ferma il server all'avvio, indicando il file.

## Senza il file

Una root creata prima che questo file esistesse non ce l'ha: non si raccoglie nulla, e le canne da pesca dicono che i pesci non
abboccano. Copia `data/harvest.toml` dalla distribuzione per attivare la pesca.

## Dagli script

```lua
if harvest.take("fish", map, x, y) then
    -- one fish less in that area
end
```

`harvest.has(id)`, `harvest.amount(id, map, x, y)` e `harvest.take(id, map, x, y)`: vedi il
[riferimento Lua](https://moongate.sh/lua/harvest/).
