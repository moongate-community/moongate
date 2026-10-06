<!-- translation: {"sourceHash":"e9c72d89761af241ceeb3b2d40ac90292360ec96916551ae2f271c9b64f2355b","title":"Abilità"} -->

# Abilità

`skills.toml` elenca le 58 abilità. Ogni `id` indica un `SkillType`, il cui valore è il
numero usato dal client (da 0 a 57), quindi le voci restano in quell'ordine, da `alchemy` a
`throwing`:

```toml
[[skill]]
id = "alchemy"
name = "Alchemy"
title = "Alchemist"
profession_name = "Alchemy"
primary_stat = "int"
secondary_stat = "dex"
str_scale = 0.0
dex_scale = 5.0
int_scale = 5.0
str_gain = 0.0
dex_gain = 0.5
int_gain = 0.5
gain_factor = 1.0
```

| Campo | Significato |
| --- | --- |
| `id` | Abilità, per nome `SkillType`; il suo valore è l'ID dell'abilità del client. |
| `name` | Nome dell'abilità. |
| `title` | Titolo di un personaggio la cui migliore abilità è questa. |
| `profession_name` | Nome usato dai file delle professioni per l'abilità. |
| `primary_stat`, `secondary_stat` | Statistiche da cui dipende l'abilità: `str`, `dex` o `int`. |
| `str_scale`, `dex_scale`, `int_scale` | Probabilità percentuale che un incremento dell'abilità aumenti anche quella statistica. |
| `str_gain`, `dex_gain`, `int_gain` | Quanto un incremento dell'abilità favorisca quella statistica quando una statistica aumenta. |
| `gain_factor` | Velocità di incremento dell'abilità; 1.0 è normale. |
| `delay` | Facoltativo. Secondi di attesa del personaggio prima di un'altra abilità dopo aver usato questa, da 0 a 3600. In sua assenza, e quando lo script dell'abilità non restituisce un numero, un secondo. |

Le 23 abilità usate direttamente da un giocatore contengono le attese di ModernUO:

| `delay` | Abilità |
| --- | --- |
| 30 | `begging`, `stealing`, `detecting_hidden`, `animal_taming` |
| 10 | `hiding`, `stealth`, `poisoning`, `remove_trap`, `tracking`, `meditation` |
| 1 | `anatomy`, `animal_lore`, `arms_lore`, `item_identification`, `taste_identification`, `evaluating_intelligence`, `forensic_evaluation`, `cartography`, `inscription`, `spirit_speak`, `peacemaking`, `provocation`, `discordance` |

Dove l'attesa di ModernUO dipende dall'esito (`meditation`, `spirit_speak`, `hiding`) il file contiene quella
abituale, e lo script dell'abilità restituisce le altre. Solo `hiding` ha uno script finora: un
`delay` non fa nulla finché l'abilità non ne ha uno.

Le [abilità](../skills.md) leggono `gain_factor` quando un'abilità aumenta e `delay` quando viene usata; i
campi delle statistiche non vengono ancora letti.

## Validazione all'avvio

Il server si arresta quando:

- `skills.toml` non esiste o non ha voci `[[skill]]`;
- gli ID non sono nell'ordine `SkillType` senza lacune: la voce 0 deve essere `alchemy`
  (valore 0), la voce 1 `anatomy` (valore 1) e così via;
- un ID è un numero o non è un nome `SkillType`;
- un `delay` è inferiore a 0 o superiore a 3600.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
