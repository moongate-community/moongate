<!-- translation: {"sourceHash":"8194b32f9c387085562ff472e8194122c6bf8b2ffbb4b2f2576682e52d7413fb","title":"gmtools"} -->

# gmtools

Apre il gump degli strumenti del game master: a sinistra una barra laterale di strumenti (meteo, stagione, ora e, per gli amministratori, eventi) e
a destra i comandi dello strumento selezionato.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `gmtools` | No | Sì | GameMaster | Game |

```text
.gmtools
```

Solo in gioco. Il gump si apre sul primo strumento della barra laterale; un clic su un altro strumento ne mostra
il pannello.

## Meteo

Lo strumento del meteo mostra il meteo del punto in cui ti trovi, il profilo del luogo e che cosa fa adesso:

```text
Weather here: temperate
Now: rain, density 40, temperature 12
```

Sotto ci sono quattro pulsanti, `none`, `rain`, `snow` e `storm`. Un clic impone quel meteo al
profilo del luogo, come fa [`.weather`](weather.md): lo riceve ogni regione che usa il profilo, finché
la prossima ora di gioco non lo estrae di nuovo. Leggi `The weather of temperate is now storm until the next
hour.` e il gump mostra il nuovo stato.

Per ora non si sceglie il profilo: il meteo è quello del luogo in cui ti trovi, quindi cammina o usa
[`.go`](go.md) per raggiungere il luogo di cui vuoi cambiare il cielo.

## Stagione

Lo strumento della stagione mostra la stagione che il client mostra nel punto in cui ti trovi (quella della
regione, altrimenti quella della mappa) e la stagione della tua mappa:

```text
Season here: winter
Season of your map: summer
```

Sotto ci sono sei pulsanti, `spring`, `summer`, `fall`, `winter`, `desolation` e `auto`. Un clic imposta
la stagione della tua mappa fino al riavvio e la invia subito a ogni giocatore sulla mappa, come fa
[`.season`](season.md), tranne a quelli in una regione con una stagione propria; `auto` restituisce alla mappa
la stagione di `maps.toml`, fatta ruotare quando `[ultima.world] season_rotation` è attivo. Leggi `The
season of your map is now winter.` e il gump mostra il nuovo stato. Il client disegna la stagione da
solo, con un suono al cambio: alberi spogli e neve in inverno, colori autunnali in autunno.

La stagione è l'aspetto del terreno; la neve che cade è il meteo, impostato dal primo strumento.
Per un mondo innevato, imposta entrambi.

## Ora

Lo strumento dell'ora mostra ciò che stampa [`.time`](time.md), l'ora di gioco e le fasi delle due lune nella
tua posizione, e il livello di luce nel punto in cui ti trovi:

```text
Game time here: 07:05
Moons: Trammel last quarter, Felucca first quarter
Light here: 0, following the time of day
```

Sotto ci sono cinque pulsanti, i livelli `0 (brightest)`, `12`, `26` e `31 (darkest)`, e `auto (the
time of day)`. Un livello dà subito quella luce a ogni giocatore del mondo, come fa
[`.globallight`](globallight.md), e la riga dice `Light here: 26, the same for every player`;
`auto` torna alla luce dell'ora del giorno. Leggi `The global light is now 26.` oppure `The
global light follows the time of day again.` La forzatura non viene salvata: un riavvio torna
all'ora del giorno. Per un livello intermedio tra i pulsanti, usa `.globallight <0-31>`.

## Eventi

Lo strumento degli eventi è per gli amministratori, come [`.event`](event.md); un game master non lo vede. Elenca
gli [eventi stagionali](../schedule.md#seasonal-events) con date, modalità e stato:

```text
Seasonal events
Halloween (10-24 to 11-15): auto, on
Christmas (12-24 to 01-01): auto, off
```

Sotto ogni evento ci sono tre pulsanti, `auto`, `on` e `off`: `auto` restituisce l'evento alle sue date, `on` e `off`
lo forzano. Leggi `Halloween is now off.` e il gump si riapre; l'evento inizia o finisce come con il
comando, e la sua modalità si conserva tra i riavvii. Il pannello elenca quattro eventi; gli altri restano a `.event`.

## Aggiungere uno strumento

Il gump è [`templates/gumps/gmtools.xml`](../gumps.md) e il suo script
[`scripts/gumps/gmtools.lua`](../scripting/shipped-scripts.md#gmtoolslua), che puoi modificare.
Uno strumento è una voce della tabella `tools` nello script e una funzione che ne disegna il pannello. Chi
non è un game master vede un gump vuoto, e un pulsante del gump non fa nulla per chi ha
perso il grado dopo averlo aperto. Uno strumento con `admin = true` nella tabella è solo per gli amministratori.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`weather`](weather.md)
- [`season`](season.md)
- [`time`](time.md) e [`globallight`](globallight.md)
- [`event`](event.md)
- [`go`](go.md)
