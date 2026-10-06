<!-- translation: {"sourceHash":"e46b68eaa6ccce41e179b4ae4a43b9792e8222652f3e2dc9bbd0568a2d33997a","title":"Moongate"} -->

# Moongate

`moongates.toml` elenca i moongate pubblici: i portali tra le città di ogni mappa.
[`.decorate`](../commands/decorate.md) posiziona un portale su ogni destinazione,
e un giocatore che vi sale, o fa doppio clic dalla cella accanto, sceglie dove andare
da un gump con una pagina per mappa.

```toml
[[facet]]
map = "malas"
cliloc = 1060643
selected_cliloc = 1062039

[[facet.destination]]
name = "Luna"
cliloc = 1060641
location = "(1015, 527, -65)"

[[facet.destination]]
name = "Umbra"
cliloc = 1060642
location = "(1997, 1386, -85)"
hue = 0x0497
```

| Campo | Significato |
| --- | --- |
| `[[facet]]` | Uno per mappa; l'ordine è quello delle schede nel gump. |
| `map` | `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` o `termur`. |
| `cliloc` | Testo client del nome della mappa nel gump. |
| `selected_cliloc` | Lo stesso nome come mostrato dal gump per la pagina aperta. |
| `[[facet.destination]]` | Città della mappa, nell'ordine in cui il gump le elenca. |
| `name` | Città, per i log; i giocatori vedono il testo client. |
| `cliloc` | Testo client del nome della città. |
| `location` | Dove si trova il portale e arrivano i viaggiatori, un `Point3D`. |
| `hue` | Tonalità del portale; nessuna se omessa. |
| `average_z` | `true` prende l'altezza dalla mappa anziché dalla `z` di `location`, per una posizione la cui altezza differisce tra versioni client, come Magincia. |

Il file distribuito contiene le 34 destinazioni ModernUO: 9 a Trammel, 9 a Felucca,
9 a Ilshenar, 2 a Malas, 3 a Tokuno e 2 a Ter Mur. I nomi sono testi client, quindi
ogni giocatore li legge nella lingua del client.

Una mappa non caricata dal server viene esclusa con i suoi portali: la pagina non
compare nel gump e `.decorate` non vi posiziona nulla. Anche una destinazione fuori
dalla propria mappa viene esclusa, con un avviso nel log. Un file vuoto significa
uno shard senza moongate pubblici.

Gli script leggono l'elenco con `moongates.facets()`; vedi il
[modulo `moongates`](https://moongate.sh/lua/moongates/).

## Validazione all'avvio

Il server si arresta all'avvio quando:

- il file non esiste;
- un `[[facet]]` non ha `map`, ne ha una sconosciuta o già usata da un altro `[[facet]]`;
- a un facet manca `cliloc` o `selected_cliloc`, o non ha `[[facet.destination]]`;
- a una destinazione manca `name`, `cliloc` o `location`, la sua `location` ha `x`
  o `y` fuori da 0 a 65535 o `z` fuori da -128 a 127, oppure `hue` è fuori da 0 a 65535.

## Vedi anche

- [`moongate`](../commands/moongate.md): il comando che crea un portale con una sola destinazione.
- [Panoramica dei file dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
