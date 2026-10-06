<!-- translation: {"sourceHash":"d8bae69e303cd0d6310a548788901d62dae305589e9fe1306cc354ef9870b2dd","title":"moongate"} -->

# moongate

Posiziona ai tuoi piedi un moongate verso un luogo della tua mappa o di un'altra.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `moongate <x>,<y>,<z> [map]` | No | Sì | GameMaster | Game |

```text
.moongate 1496,1628,10
.moongate 5690 569 25
.moongate 1496,1628,10 Felucca
```

Solo in gioco. Il luogo è scritto come per [`go`](go.md): `x`, `y` e `z`, separati
da virgole o spazi, `z` da -128 a 127, poi al massimo uno dei nomi `MapType` (`Felucca`,
`Trammel`, `Ilshenar`, `Malas`, `Tokuno`, `TerMur`), senza distinzione tra maiuscole
e minuscole. Senza una mappa, il portale porta a un luogo della mappa su cui ti trovi.

Un moongate blu (il template oggetto `moongate`) appare dove ti trovi, luminoso
(proprietà `light = "circle300"`, come ModernUO), e viene salvato con il mondo.
Mantiene il luogo nelle proprietà `teleport.x`, `teleport.y`, `teleport.z` e
`teleport.map`; il suo script, `scripts/items/moongate.lua`, vi porta chiunque ci
salga un secondo dopo (vedi [moongate.lua](../scripting/shipped-scripts.md#moongatelua)).
Ci sei già sopra, quindi scendi e risali per usarlo. L'altezza viene presa come
scritta; il comando non cerca il suolo.

Una mappa non caricata, o una posizione fuori dalla mappa, viene rifiutata con
`No moongate can lead there: tokuno is not loaded or the spot is outside it.` e
non viene creato alcun portale. Qualsiasi altra forma che non sia tre numeri e al
massimo un nome di mappa stampa l'utilizzo.

Il portale non scompare da solo, e [`remove`](remove.md) rimuove solo NPC: nessun
comando elimina ancora un portale.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`go`](go.md)
- [Moongate pubblici](../data-files/moongates.md): i portali delle città, con un elenco di destinazioni
