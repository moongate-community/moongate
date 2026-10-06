<!-- translation: {"sourceHash":"109cacf75c07573e105450efe11d22b4a3c7eb6dfee531e2405e6a6b2f75cbf9","title":"time"} -->

# time

Stampa l'ora di gioco nel luogo in cui ti trovi e le fasi delle due lune.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `time` | No | Sì | Regular | Game |

```text
.time
```

Solo in gioco, per ogni giocatore. Non accetta argomenti e stampa ora e minuto dell'
orologio di gioco alla tua posizione, poi le fasi di Trammel e Felucca, come le mostra il cannocchiale
 di ModernUO:

```text
Game time here: 07:05.
Moons: Trammel last quarter, Felucca first quarter.
```

L'orologio di gioco è quello che controlla giorno e notte: un minuto di gioco dura
`[ultima.world] seconds_per_uo_minute` secondi reali (5 per impostazione predefinita, quindi un giorno di gioco è 2 ore reali),
ogni mappa è avanti di 320 minuti di gioco rispetto alla precedente, e l'ora avanza di un minuto ogni 16
caselle verso est. Due giocatori sulla stessa mappa possono quindi vedere ore diverse.

Ogni luna attraversa 8 fasi (luna nuova, falce crescente, primo quarto, gibbosa crescente, luna
piena, gibbosa calante, ultimo quarto, falce calante) sull'orologio della propria mappa: quella di Felucca cambia ogni 10
minuti di gioco, quella di Trammel ogni 30, ed entrambe si spostano con la longitudine come l'ora.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`globallight`](globallight.md)
- [`gmtools`](gmtools.md): ora, lune e luce in un gump
