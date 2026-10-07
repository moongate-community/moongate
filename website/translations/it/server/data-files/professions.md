<!-- translation: {"sourceHash":"3b4a1005e36f9e4bb3d8da1ecbf97884a0828f0a5bbf93a82b91d92ca1964e07","title":"Professioni"} -->

# Professioni

`professions.toml` elenca le professioni che un giocatore può scegliere alla creazione del personaggio.
Il client invia l'id scelto (pacchetto 0xF8) e lascia vuote abilità e statistiche; il
personaggio riceve le statistiche e le abilità elencate qui. L'id 0 è la scelta "Avanzato", dove il giocatore sceglie
tutto, quindi non è elencato; un id non elencato viene trattato come 0.

```toml
[[profession]]
id = 1
name = "Warrior"
name_cliloc = 1061180
description_cliloc = 1061230
gump = 5577
str = 45
dex = 35
int = 10
skills = [
    { skill = "Tactics", value = 30 },
    { skill = "Healing", value = 30 },
    { skill = "Swordsmanship", value = 30 },
    { skill = "Anatomy", value = 30 },
]
```

| Campo | Significato |
| --- | --- |
| `id` | L'id della professione inviato dal client. |
| `name` | Il nome della professione. |
| `name_cliloc`, `description_cliloc` | Gli id del nome e della descrizione localizzati mostrati dal client. |
| `gump` | L'id dell'immagine gump mostrata dal client. |
| `str`, `dex`, `int` | Le statistiche iniziali. |
| `skills` | Le abilità iniziali: il nome `SkillType` senza spazi (`"SpiritSpeak"`) e il valore in punti interi. |

Le professioni distribuite assegnano 90 punti di statistiche e 120 punti di abilità, i totali di
`CharacterCreationRules` (`StatTotal = 90`, totali delle abilità di 100 o 120). Il caricatore
non verifica questi totali. La creazione del personaggio usa le statistiche
e le abilità di una professione corrispondente; le scelte personalizzate vengono validate separatamente da `CharacterCreationRules`.

## Validazione all'avvio

Il server si arresta quando:

- `professions.toml` non esiste o non contiene voci `[[profession]]`;
- un id è inferiore a 1 oppure è usato due volte;
- un'abilità iniziale non è in `skills.toml`.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Verificare le modifiche](../data-files.md#check-your-changes): valida i dati modificati prima di riavviare.
