<!-- translation: {"sourceHash":"d5970ced928b0d6e984df0867ab7d57d0054e370c62fce0d7f9130d8379f908b","title":"version"} -->

# version

Mostra la versione eseguita dal server, se è una compilazione Debug o Release e quando è stata compilata.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `version` | Sì | Sì | Regular | Ogni ruolo |

```text
version
.version
```

Per ogni giocatore e dalla console. Non accetta argomenti e stampa una riga:

```text
Moongate 0.14.0 "Lilly" (Release), built 2026-10-05 14:32 UTC.
```

- `0.14.0` è la versione del rilascio e `Lilly` il suo nome in codice.
- `Release` o `Debug` è la configurazione in cui sono stati compilati i binari. Uno shard aperto ai giocatori
  esegue una compilazione Release; una Debug è ciò che producono `dotnet run` e una compilazione di sviluppo.
- L'ora indica quando è stato compilato `mgserver`, in UTC: distingue
  due compilazioni della stessa versione.

Le stesse tre informazioni aprono la console del server a ogni avvio, sotto l'intestazione:

```text
Version: 0.14.0 (Release) Codename: "Lilly"
        Built: 2026-10-05 14:32 UTC
```

Anche `mgctl init` le mostra, per `mgctl` stesso: viene compilato separatamente, quindi il suo `Built:` può differire
di un minuto da quello del server. `mgctl --version` stampa il solo numero, `0.14.0`, per gli script.

Una compilazione che non indica come o quando è stata prodotta mostra `(unknown)` e `built unknown`.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`uptime`](uptime.md)
