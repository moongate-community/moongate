<!-- translation: {"sourceHash":"a9b3767f7fb9cb68b9cd9b55c5e8dcc3a244fde75d66bed60e242bf1c90e05c6","title":"Sicurezza delle dipendenze"} -->

# Audit di sicurezza delle dipendenze

Il workflow `Security Audit` verifica tutte le dipendenze NuGet in `Moongate.slnx`,
incluse quelle transitive e di test, usando il database delle vulnerabilità NuGet.

Viene eseguito solo su `main`: dopo ogni push, ogni giorno alle 03:17 UTC e tramite
`workflow_dispatch` quando viene selezionato `main`. Pull request e `develop` non
eseguono questo workflow. La pianificazione diventa attiva quando il workflow raggiunge `main`,
il branch predefinito del repository.

## Politica degli errori

- Le vulnerabilità alte e critiche fanno fallire il job (`NU1903`, `NU1904`).
- Le vulnerabilità basse e moderate vengono segnalate come avvisi (`NU1901`, `NU1902`).
- Gli errori delle sorgenti di audit fanno fallire il job (`NU1900`, `NU1905`), così come errori di ripristino o
  generazione del rapporto.

Ogni esecuzione forza la risoluzione delle dipendenze e aggira la cache HTTP per aggiornare
i dati degli avvisi. Il workflow usa lo stesso container SDK .NET 10 e le stesse etichette dei runner
della CI, con permessi di sola lettura sul repository.

Il riepilogo dell'esecuzione include log di ripristino/audit e stato della generazione del rapporto.
L'artefatto `security-audit` conserva il log di ripristino e il rapporto JSON delle vulnerabilità
per 14 giorni, anche quando rilevamenti alti o critici fanno fallire il job. Se
il ripristino non può terminare, il log resta disponibile anche quando non può essere generato un rapporto JSON
completo.

## Eseguire localmente

Dalla root del repository con l'SDK .NET 10 installato:

```bash
dotnet restore Moongate.slnx --force --no-http-cache \
  -p:NuGetAudit=true \
  -p:NuGetAuditMode=all \
  -p:NuGetAuditLevel=low \
  -warnaserror:NU1900,NU1903,NU1904,NU1905

dotnet package list --project Moongate.slnx \
  --vulnerable --include-transitive --no-restore \
  --format json --output-version 1
```

Il comando di ripristino impone la politica di gravità. Il comando di elenco dei package
genera il rapporto; il solo codice di uscita non è un controllo bloccante delle vulnerabilità.

Per un rilevamento transitivo, usa `dotnet nuget why Moongate.slnx <package-id>` per
identificare la dipendenza che porta il package interessato nella soluzione.

Vedi la [documentazione di auditing NuGet](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
per codici di avviso e configurazione della gravità.
