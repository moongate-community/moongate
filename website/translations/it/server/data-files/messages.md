<!-- translation: {"sourceHash":"d97e258f94b08c4f3d88fa46fcc896fd92d636cbff4cd29d8e53973400bb660e","title":"Messaggi"} -->

# Messaggi

`data/messages/<lang>.toml` contiene i testi inviati dal server in una
lingua. Ogni file `*.toml` nella directory `data/messages/<lang>/` viene unito
ad esso, così una lingua può essere suddivisa in più file. Il server distribuisce
i testi standard in `<lang>.toml` e quelli propri di Moongate (numeri da 30000) in
`<lang>/moongate.toml`. `ILocalizationService` li legge durante l'esecuzione. Vedi
[Localizzazione](../localization.md) per il formato, il fallback in inglese e le
regole di validazione.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Verificare le modifiche](../data-files.md#check-your-changes): valida i dati modificati prima di riavviare.
