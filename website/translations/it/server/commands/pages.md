<!-- translation: {"sourceHash":"d952f551c981118dc45c3677e1e9754d53ae8b11a5b8940b79cb6cb2bb398522","title":"pages"} -->

# pages

Apre la coda delle richieste per i game master: quelle aperte e in carico, dalla più vecchia, dieci per
pagina. Una riga apre la richiesta, dove il game master può andare dal giocatore, prenderla in carico,
rispondere o chiuderla.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `pages` | No | Sì | GameMaster | Game |

```text
.pages
```

Solo in gioco. I giocatori mandano le richieste dal pulsante Help del paperdoll, con
*Call a game master*; vedi [Aiuto](../help.md) per cosa contiene una richiesta, come la risposta arriva al
giocatore e le impostazioni della coda.

```text
Help requests
[>] #1 Gino, Bug, 3 min, open
[>] #2 Pina, Harassment, now, taken by Gino
```

Il livello viene ricontrollato da ogni pulsante, quindi un game master retrocesso mentre il gump è aperto
non può fare nulla. Una richiesta chiusa nel frattempo da un altro game master dice
`Request 1 is already closed.`
