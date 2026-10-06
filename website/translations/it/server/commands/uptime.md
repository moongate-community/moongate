<!-- translation: {"sourceHash":"050bdc36cd8a4fc90de11e4fadd3249ddb166b4be3176838ecc5cb2153604156","title":"uptime"} -->

# uptime

Mostra da quanto tempo il server è in esecuzione e da quando.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `uptime` | Sì | Sì | Regular | Ogni ruolo |

```text
uptime
.uptime
```

Per ogni giocatore e dalla console. Non accetta argomenti e stampa una riga:

```text
Up for 2d 4h 13m, since 2026-10-03 10:19 UTC.
```

Il tempo viene contato dall'avvio del processo server e mostrato nelle unità maggiori: giorni,
ore e minuti dopo il primo giorno, `4h 13m` dopo la prima ora, `13m 9s` prima di essa e `9s`
nel primo minuto. Il momento di avvio è in UTC.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`version`](version.md)
