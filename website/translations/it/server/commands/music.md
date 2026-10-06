<!-- translation: {"sourceHash":"e3bf91d375772dceb3fd57ac8d1fd6b36186b0f7c78973f13914e6aeb8761cdf","title":"music"} -->

# music

Mostra la musica nel luogo in cui ti trovi, oppure riproduce un brano per te.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `music [track]` | No | Sì | GameMaster | Game |

```text
.music
.music tavern04
```

Solo in gioco. Senza un brano stampa la musica del luogo in cui ti trovi: quella della regione, altrimenti della mappa,
altrimenti `no_music`, come `Music here: britain1.` Con un nome `MusicType` riproduce quel brano per te
finché il prossimo cambio di regione ne porta un altro: `Playing tavern04.` Un brano già in riproduzione riparte,
e `tavern04 could not play.` significa che la musica non ha potuto esserti inviata. La musica delle regioni proviene da
[`data/regions`](../data-files/regions.md) e [`maps.toml`](../data-files/maps.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`weather`](weather.md)
