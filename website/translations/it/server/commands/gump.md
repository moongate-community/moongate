<!-- translation: {"sourceHash":"de90d50b0d173cd951aef3572d4e377ea6b9e19785830aa65375271bfa5c1d6a","title":"gump"} -->

# gump

Apre su di te un gump di `templates/gumps`, per provarlo.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `gump <id> [name=value ...]` | No | Sì | GameMaster | Game |

```text
.gump tutorial_name
.gump tutorial_greeting name=Aria
```

Solo in gioco. Apre il gump come farebbe `gump.open` da uno script: i suoi slot vengono riempiti e il suo
script riceve le risposte. Le coppie `name=value` riempiono i segnaposto `${name}`: i nomi vengono convertiti in
minuscolo, come i segnaposto, e un valore può contenere `=` ma non spazi. Un ID sconosciuto stampa
`No gump <id> in templates/gumps.`; un gump che non può aprirsi, come uno la cui funzione slot fallisce,
stampa `Gump <id> could not open.`
Vedi [Gump](../gumps.md) e [Il tuo primo gump](../gump-tutorial.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`decorate`](decorate.md)
