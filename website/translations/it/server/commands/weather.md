<!-- translation: {"sourceHash":"5ef9650c9b1b20f998d7b75ac1e35aa7a5949be4ea4b521defd677aa52da0695","title":"weather"} -->

# weather

Mostra il meteo nel punto in cui ti trovi, oppure lo forza fino alla successiva ora di gioco.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `weather [none\|rain\|snow\|storm]` | No | Sì | GameMaster | Game |

```text
.weather
.weather storm
```

Solo in gioco. Senza un tipo stampa il meteo nel punto in cui ti trovi:
`Weather here (temperate): rain, density 40, temperature 12.` Con `none`, `rain`, `snow` o
`storm` forza quel meteo sul profilo del luogo (ogni regione che usa il profilo
lo riceve) fino alla successiva ora di gioco, che lo estrae di nuovo. Vedi i
[profili meteo](../data-files/weather.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`gmtools`](gmtools.md): gli stessi pulsanti in un gump
- [`globallight`](globallight.md)
