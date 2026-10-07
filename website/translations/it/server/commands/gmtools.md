<!-- translation: {"sourceHash":"5fa4864952cbf1a4fd2ae2c0edbe95b7bf6d05b0811e76b1187abd442d9fdf4b","title":"gmtools"} -->

# gmtools

Apre il gump degli strumenti del game master: a sinistra una barra laterale di strumenti (meteo, stagione e ora) e
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

## Aggiungere uno strumento

Il gump è [`templates/gumps/gmtools.xml`](../gumps.md) e il suo script
[`scripts/gumps/gmtools.lua`](../scripting/shipped-scripts.md#gmtoolslua), che puoi modificare.
Uno strumento è una voce della tabella `tools` nello script e una funzione che ne disegna il pannello. Chi
non è un game master vede un gump vuoto, e un pulsante del gump non fa nulla per chi ha
perso il grado dopo averlo aperto.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`weather`](weather.md)
- [`season`](season.md)
- [`time`](time.md) e [`globallight`](globallight.md)
- [`go`](go.md)
