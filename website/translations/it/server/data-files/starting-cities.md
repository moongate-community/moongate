<!-- translation: {"sourceHash":"8576262c2679efeb59ceae8eefae69630b51c2a3f2a6b9b8d6a17bb5d28d522f","title":"Città iniziali"} -->

# Città iniziali

`starting_cities.toml` elenca le città in cui un nuovo personaggio può iniziare. Il server
di gioco le invia con l'elenco personaggi (pacchetto 0xA9) subito dopo il login di gioco.
Il client restituisce l'indice della città scelta, quindi l'ordine delle voci
conta.

```toml
[[starting_city]]
town = "New Haven"
description = "The Bountiful Harvest Inn"
location = "(3503, 2574, 14)"
map = "trammel"
cliloc = 1150168
```

| Campo | Significato |
| --- | --- |
| `town` | Nome della città mostrato dal client. |
| `description` | Luogo nella città, come una locanda. |
| `location` | Dove appare il personaggio, un `Point3D`. |
| `map` | Mappa di `location`. |
| `cliloc` | ID della descrizione localizzata mostrata dal client. |

## Validazione all'avvio

Il loader verifica i limiti del pacchetto dell'elenco personaggi, così una città non valida arresta il
server all'avvio invece di fallire a ogni login di gioco. Si arresta quando:

- il file non esiste;
- non ha voci `[[starting_city]]`, oppure ne ha più di 255;
- un `town` o `description` è vuoto o composto da soli spazi, più lungo di 32 caratteri o non ASCII.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
