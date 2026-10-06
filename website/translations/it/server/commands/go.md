<!-- translation: {"sourceHash":"7e62567ded746ec7a3acae1b653e2ce7f841265542dcbd09365c47cd0bb615d4","title":"go"} -->

# go

Ti porta in un luogo: scelto da un elenco, indicato per nome o tramite coordinate numeriche.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `go [<x>,<y>,<z> [map] \| <place>]` | No | Sì | GameMaster | Game |

```text
.go
.go britain
.go covetous entrance
.go 1496,1628,10
.go 5690 569 25
.go 1496,1628,10 Felucca
```

Solo in gioco.

## Scegliere dall'elenco

`.go` da solo apre un gump con i luoghi nominati in [`locations.toml`](../data-files/locations.md),
al livello della tua mappa: prima le sue categorie (`Dungeons`, `Towns`, ...), poi
i luoghi elencati sotto la mappa stessa, dodici per pagina. Una categoria apre il
livello inferiore, `Back` quello superiore, fino all'elenco delle mappe. Un luogo ti
porta lì subito e il gump rimane aperto, così puoi passare da uno al successivo;
un luogo rifiutato dal mondo, come uno fuori dalla propria mappa, lo segnala con
`You cannot go to Arena: its map is not loaded or the spot is outside it.` Percorsi
e nomi lunghi vengono tagliati al bordo della cornice.

Il gump è [`templates/gumps/go.xml`](../gumps.md) e le sue righe provengono da
`scripts/gumps/go.lua`; puoi modificarli entrambi. Senza `locations.toml`, o quando
nessuno dei suoi luoghi si trova su una mappa caricata, `.go` da solo stampa l'utilizzo.

## Indicare il nome del luogo

`.go <place>` va al luogo indicato dalle parole, senza distinguere maiuscole e minuscole:

- il suo nome: `.go minoc`;
- categorie e nome, usando tante categorie finali quante servono quando più luoghi
  condividono un nome: `.go covetous entrance`, `.go dungeons covetous level 1`;
- una categoria, che indica il proprio luogo chiamato `Center` se presente,
  altrimenti il primo luogo: `.go britain`, `.go covetous`.

La tua mappa ha la precedenza: prima un suo luogo, poi una sua categoria, e solo dopo
un luogo o una categoria di un'altra mappa, che ti porta su quella mappa. Quando
nulla ha esattamente quel nome, bastano le parole finali di un nome: `.go haven`
trova `Old Haven` su una mappa senza Haven. Contano solo parole intere, e `/` viene
letto come spazio, quindi funziona un percorso copiato dal titolo del gump:
`.go dungeons/covetous`. Quando più luoghi corrispondono ancora, non ne viene scelto
nessuno e vengono elencati i primi dieci, così puoi aggiungere una parola:

```text
2 places are named entrance; add words of the category, such as go covetous entrance:
Felucca: Dungeons/Covetous/Entrance
Felucca: Dungeons/Shame/Entrance
```

`No place is named atlantis; go alone lists them.` risponde a un nome che nessun luogo possiede.

## Indicare la posizione

Un argomento che inizia con una cifra o un segno è una posizione. I tre numeri sono
`x`, `y` e `z`, separati da virgole o spazi; `z` va da -128 a 127. Senza una mappa
resti sulla tua; una mappa è uno dei nomi `MapType` (`Felucca`, `Trammel`, `Ilshenar`,
`Malas`, `Tokuno`, `TerMur`), senza distinzione tra maiuscole e minuscole. Qualsiasi
altra forma che non sia tre numeri e al massimo un nome di mappa stampa l'utilizzo.

## Cosa succede

Ti trovi subito sulla posizione: il client riceve prima la nuova mappa quando cambia,
e i giocatori attorno alla vecchia posizione non ti vedono più mentre quelli attorno
alla nuova ti vedono. L'altezza viene presa così come scritta; il comando non cerca il suolo.

Una mappa non caricata, o una posizione fuori dalla mappa, viene rifiutata con
`You cannot go there: tokuno is not loaded or the spot is outside it.`

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`locations.toml`](../data-files/locations.md)
- [`where`](where.md)
- [`moongate`](moongate.md)
