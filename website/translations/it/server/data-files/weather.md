<!-- translation: {"sourceHash":"8e7a7a5df6d95f71d3202da7544dcef264c9dbd110685433284a0bfd77ba1c9a","title":"Meteo"} -->

# Meteo

`weather.toml` contiene i profili meteo, denominati in base al clima. Mappe e regioni ne scelgono
uno per nome:

```toml
[[weather]]
name = "desert"
rain_chance = 1
snow_chance = 0
storm_chance = 0
snow_threshold = 0
min_temperature = 10
max_temperature = 30
cold_chance = 0
cold_temperature = 0
heat_chance = 80
heat_temperature = 35
rain_temperature_drop = 5
storm_temperature_drop = 10
```

| Campo | Significato |
| --- | --- |
| `name` | Il nome usato da mappe e regioni. |
| `rain_chance`, `snow_chance`, `storm_chance` | Probabilità (%) di quel meteo nell'ora successiva, verificata prima per il temporale, poi per la neve, poi per la pioggia. |
| `snow_threshold` | Nessuna neve a questa temperatura o superiore. |
| `min_temperature`, `max_temperature` | L'intervallo di una giornata normale. |
| `cold_chance`, `cold_temperature` | Probabilità (%) di una giornata fredda, tra `min_temperature` e `cold_temperature`. |
| `heat_chance`, `heat_temperature` | Probabilità (%) di una giornata calda, tra `max_temperature` e `heat_temperature`. |
| `rain_temperature_drop`, `storm_temperature_drop` | Di quanto la pioggia o un temporale abbassano la temperatura. |

I profili distribuiti sono `none`, `desert`, `tropical`, `temperate`, `highland`,
`stormy`, `mild`, `snowy`, `cool` e `rainy`. `none` non cambia mai il meteo.
`none` copre anche le regioni senza un campo `weather`, come i dungeon.

## Come li usa il server

Ogni profilo ha un solo meteo alla volta, condiviso da tutte le regioni e mappe che lo usano. Ogni ora di gioco
(60 minuti di gioco: 5 minuti reali con il valore predefinito di 5 per `ultima.world.seconds_per_uo_minute`)
il server lo estrae di nuovo: temporale, altrimenti neve quando la giornata è più fredda di `snow_threshold`,
altrimenti pioggia, altrimenti asciutto; le precipitazioni hanno una densità da 10 a 70 particelle. Ogni 24 ore di gioco
estrae la temperatura del giorno: giornata calda, fredda o normale, abbassata dalla pioggia o da un
temporale. Il meteo non viene salvato: un riavvio lo estrae di nuovo.

Un giocatore vede il meteo del profilo della propria regione, o del profilo della propria mappa quando si trova fuori da ogni
regione. Dentro un edificio (uno statico più di 10 unità sopra la testa) rimane asciutto. Il server
invia il pacchetto `0x65` quando il meteo del giocatore cambia: all'accesso, al cambio di regione e durante un
controllo ogni 5 secondi che rileva l'ingresso e l'uscita dagli edifici; viene inoltre reinviato ogni
minuto e ogni ora di gioco, perché il client smette di mostrare il meteo pochi minuti dopo
l'ultimo pacchetto, e dopo ogni pacchetto di stagione (`0xBC`), che interrompe il meteo del client. Durante un temporale il giocatore
all'aperto sente tuoni di tanto in tanto. I game master lo leggono o lo forzano con
[`.weather`](../commands/weather.md).

## Validazione all'avvio

Il server si arresta quando:

- `weather.toml` non esiste o non contiene voci `[[weather]]`;
- un profilo non ha nome oppure un nome è usato due volte (i nomi distinguono maiuscole e minuscole);
- una probabilità è fuori dall'intervallo da 0 a 100;
- `min_temperature` è superiore a `max_temperature`.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): posizioni dei file, ordine di caricamento e formati dei valori condivisi.
- [Verificare le modifiche](../data-files.md#check-your-changes): valida i dati modificati prima di riavviare.
