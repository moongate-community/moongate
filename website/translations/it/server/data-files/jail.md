<!-- translation: {"sourceHash":"de8742d26f89c0a43227bbe2db17c5ce2cedb8b7c435e930e66e806a0c387807","title":"Prigione"} -->

# Prigione

`jail.toml` descrive la [prigione](../jail.md): mappa e celle. Il gump di
[`.jail`](../commands/jail.md) elenca le celle per numero, e un prigioniero arriva alla posizione
 della sua cella.

```toml
map = "felucca"
release = "(1444, 1697, 10)"

[[cell]]
number = 1
location = "(5276, 1164, 0)"

[[cell]]
number = 2
location = "(5286, 1164, 0)"
```

| Campo | Significato |
| --- | --- |
| `map` | Mappa delle celle: `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` o `termur`. |
| `release` | Dove va un prigioniero liberato quando la mappa su cui è stato arrestato non è più caricata, un `Point3D` sulla mappa della prigione. |
| `[[cell]]` | Uno per cella, nell'ordine in cui il gump le elenca. |
| `number` | Numero mostrato dal gump; da 1, ciascuno usato una volta. |
| `location` | Dove arriva il prigioniero, un `Point3D`. |

Il file fornito contiene le dieci celle della prigione di Felucca, le stesse usate da ModernUO e UOX3, e
rilascia alla banca di Britain come ModernUO. Otto celle sono stanze chiuse di nove caselle per nove,
le ultime due sono larghe il doppio.

Una cella aggiunta qui richiede la sua cassa di razioni: una regione in più in
`templates/spawns/felucca/jail.toml`, come spiega [Prigione](../jail.md#the-chest-of-rations).

Il file può mancare: la prigione è allora disattivata e `.jail` lo segnala.

Gli script leggono le celle con `jail.cells()`; vedi il [modulo `jail`](https://moongate.sh/lua/jail/).

## Validazione all'avvio

Il server si arresta all'avvio quando il file esiste e:

- non ha `map` o ne ha una sconosciuta;
- non ha `release`, oppure uno con `x` o `y` fuori da 0 a 65535 o `z` fuori da -128 a 127;
- non ha `[[cell]]`;
- una cella ha un `number` inferiore a 1 o uguale a quello di un'altra cella;
- una cella non ha `location`, oppure ne ha una fuori dagli stessi limiti.

## Vedi anche

- [Prigione](../jail.md): cosa fa la prigione.
- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
