<!-- translation: {"sourceHash":"68d86f4b713b8f5dcd6a5c07d0f03cd2ca89a2d193e02d4647d2d98de541ba03","title":"sql_backup"} -->

# sql_backup

Salva il mondo, poi scrive un backup SQL dei database gestiti da questo server.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `sql_backup` | Sì | Sì | Administrator | Ogni ruolo |

```text
sql_backup
```

In gioco, gli amministratori usano `.sql_backup`. Il comando funziona anche quando
`sql_backup.enabled` è false: quell'impostazione controlla solo la pianificazione.

Risponde con una riga per ogni file scritto, con la sua dimensione:

```text
SQL backup started.
auth_20261002_113000.sql (1.2 MB)
world_20261002_113000.sql (48.3 MB)
```

Un database che non può essere scritto produce una riga di errore,
`SQL backup of world failed: <reason>.`; i dettagli sono nel log del server.
Mentre è in corso un altro backup, pianificato o richiesto, il comando risponde
`A SQL backup is already running.` e non fa nulla. Argomenti aggiuntivi stampano l'utilizzo.

Un server solo login scrive `auth`, uno solo game scrive `world`. Vedi
[Backup dei database](../persistence-operations.md#database-backups) per file,
rotazione e procedura di ripristino.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`save`](save.md)
