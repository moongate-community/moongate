<!-- translation: {"sourceHash":"a85711f6ba385cbdffb15f354c6034040602be64bffacac0b3c021a376298da5","title":"Mappe"} -->

# Mappe

Un oggetto mappa si apre nel client sulla parte del mondo che mostra, con il percorso di puntine tracciato sopra. La cartografia,
le mappe del tesoro e i messaggi in bottiglia disegneranno le loro mappe su questa base.

## Aprire una mappa

Fai doppio clic su una mappa nello zaino, o a terra entro 2 caselle; più lontano dice "That is too far away.".
Il client disegna da solo il territorio: il server gli dice solo gli angoli dell'area, la dimensione del disegno e il
mondo.

Le 33 mappe pronte (il mondo piccolo e grande, Britain, Minoc, da Britain a Trinsic, i mondi di Ilshenar, Malas, Tokuno
e Ter Mur, e le altre) si aprono sull'area che dà loro UOX3. Una mappa vuota, o una mappa creata non ancora disegnata, non ha area:
"It appears to be blank.". Una mappa di Felucca aperta in Trammel viene disegnata come Trammel, la stessa terra.

## Tracciare un percorso

1. Apri la mappa e premi il lucchetto nel suo angolo: ora il percorso si può modificare.
2. Fai clic sul disegno per aggiungere una puntina, trascina una puntina per spostarla, o usa i pulsanti per togliere puntine o cancellare il percorso.
3. Premi di nuovo il lucchetto quando il percorso è pronto.

Un percorso ha al massimo 50 puntine e resta con la mappa. Si può modificare solo una mappa nello zaino o entro 2 caselle,
e mai una che uno script ha protetto, una tenuta sul cursore, o da un fantasma. I client più vecchi della 7.0.13, e un client che
non ha ancora detto la sua versione, mostrano solo mappe di Felucca e Trammel; una mappa di un altro mondo glielo dice.

## Per gli script

Il modulo Lua `map` apre le mappe e ne imposta area e percorso:

```lua
-- A map of the 400 tiles around the player, with a pin where it stands.
local here = mobile.location(user)
map.set_bounds(serial, here.x - 200, here.y - 200, here.x + 200, here.y + 200, 200, 200, here.map)
map.add_world_pin(serial, here.x, here.y)
map.display(user, serial)
```

Una mappa tiene i suoi dati nelle proprietà dell'oggetto: `map.x1`, `map.y1`, `map.x2`, `map.y2`, `map.width`, `map.height`, `map.facet`,
`map.pins` (pixel del disegno, `x,y;x,y`), `map.editable` e `map.protected`. Una mappa pronta senza di esse prende
la sua area dai tag del suo template, da `map_x1` a `map_facet`. Il percorso mantiene i suoi pixel quando l'area cambia:
cancellalo o impostalo di nuovo.

## Root esistenti

`mgctl init` non sostituisce mai un file che potresti aver modificato. Copia dalla distribuzione
`scripts/items/map_item.lua` e `templates/items/skills/misc/maps.toml`, oppure dai `script_id = "map_item"` e i
tag `map_*` ai tuoi template di mappe.

## Non ancora

Cartografia, mappe del tesoro, messaggi in bottiglia e mappe indecifrabili. Inserire e togliere puntine non è ancora stato provato con un
client.

## Vedi anche

- [Script forniti](scripting/shipped-scripts.md)
- [Pacchetti e handler](packets.md)
