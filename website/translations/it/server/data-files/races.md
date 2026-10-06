<!-- translation: {"sourceHash":"156b43704e25724cc36c344703a6de2d80887e5cbf238c33a073507d73d2fa24","title":"Razze"} -->

# Razze

`races.toml` elenca le razze selezionabili da un giocatore e, per ogni genere, il corpo e
 gli stili consentiti di capelli e barba:

```toml
[[race]]
race = "human"
name = "Human"
skin_hues = ["0x03EA-0x0422"]
hair_hues = ["0x044E-0x047D"]

[race.male]
body = 400
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2047, 0x2048, 0x2049, 0x204A]
beard = [0x203E, 0x203F, 0x2040, 0x2041, 0x204B, 0x204C, 0x204D]

[race.female]
body = 401
hair = [0x203B, 0x203C, 0x203D, 0x2044, 0x2045, 0x2046, 0x2047, 0x2049, 0x204A]
beard = []
```

| Campo | Significato |
| --- | --- |
| `race` | `human`, `elf` o `gargoyle` (`RaceType`). |
| `name` | Nome della razza. |
| `skin_hues` | Colori della pelle consentiti, come valori o intervalli `HueSpec`. Vuoto consente qualsiasi colore. |
| `hair_hues` | Colori di capelli e barba consentiti, nella stessa forma. |
| `[race.male]`, `[race.female]` | Una sezione per genere. |
| `body` | ID del corpo di un personaggio vivo di questa razza e genere. |
| `hair`, `beard` | ID degli oggetti degli stili consentiti. Nessun capello o barba (0) è sempre consentito e non viene elencato. |

Alla creazione del personaggio un colore fuori da quelli consentiti diventa il colore consentito più vicino,
e uno stile non elencato viene eliminato (`CharacterCreationRules`). Una razza inviata dal client
che non è caricata diventa umana.

## Validazione all'avvio

Il server si arresta quando:

- `races.toml` non esiste o non ha voci `[[race]]`;
- una razza è elencata due volte;
- una razza non ha una sezione `[race.male]` o `[race.female]`;
- un `body` è inferiore a 1;
- uno stile di capelli o barba è fuori dall'intervallo da 1 a 0xFFFF.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
