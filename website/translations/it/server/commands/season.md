<!-- translation: {"sourceHash":"41cffa3250f6323e0642d45beb77f6dc382c23baf9b9727aa340bc5f9e7e36c7","title":"season"} -->

# season

Mostra la stagione nel luogo in cui ti trovi, oppure imposta la stagione della tua mappa.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `season [spring\|summer\|fall\|winter\|desolation\|auto]` | No | Sì | GameMaster | Game |

```text
.season
.season winter
.season auto
```

Solo in gioco. Senza argomento stampa la stagione mostrata dal client nel luogo in cui ti trovi (quella della
regione, altrimenti della mappa) e la stagione della tua mappa: `Season here: fall; trammel: summer.`

Con una stagione imposta la stagione della tua mappa fino al riavvio e la invia subito a ogni giocatore
sulla mappa, tranne quelli in una regione con una propria stagione: `Season of trammel: winter.` `auto`
restituisce alla mappa la stagione di `maps.toml`, ruotata quando `[ultima.world] season_rotation` è attivo. Vedi
[Stagioni](../data-files/maps.md#seasons).

Il client disegna autonomamente la stagione: alberi spogli e neve in inverno, colori autunnali in autunno,
alberi morti in desolazione. Riproduce un suono al cambiamento.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`time`](time.md)
- [`weather`](weather.md)
- [`gmtools`](gmtools.md): gli stessi pulsanti in un gump
