<!-- translation: {"sourceHash":"fddbb6df787ed5540da2da0ddd4125d2b42a9bd2b1505fe552b55fa84b8e838c","title":"Nomi vietati"} -->

# Nomi vietati

`banned_names.toml` contiene le parole che il nome di un personaggio giocante non può usare. Il confronto
ignora maiuscole e minuscole.

```toml
starts_with = [
    "admin",
    "counselor",
    "gm",
]

words = [
    "adept",
    "apprentice",
]
```

| Campo | Significato |
| --- | --- |
| `starts_with` | Parole con cui un nome non può iniziare: `gm` vieta anche `GMaria`. |
| `words` | Parole che un nome non può contenere come parola intera: `mage` vieta `Aria the Mage` ma non `Magenta`. |

Il caricatore rimuove gli spazi ai bordi di ogni parola e restituisce un `BannedNamesContent`. Alla creazione
del personaggio, un nome vietato o malformato diventa `Generic Player`
(`CharacterCreationRules.ValidateName`). I nomi non sono univoci.

## Validazione all'avvio

Il server si arresta quando:

- `banned_names.toml` non esiste;
- una parola è vuota dopo la rimozione degli spazi, cosa che vieterebbe ogni nome.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Verificare le modifiche](../data-files.md#check-your-changes): valida i dati modificati prima di riavviare.
