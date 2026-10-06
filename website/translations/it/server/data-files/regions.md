<!-- translation: {"sourceHash":"1d786ccf5516ca46b813ea1755b3c7ac476d9830706947ae342e5d57294d6f1b","title":"Regioni"} -->

# Regioni

`data/regions/` contiene un file per mappa, con il nome della mappa: `felucca.toml`,
`trammel.toml`, `ilshenar.toml`, `malas.toml`, `tokuno.toml` e `termur.toml`. Il
nome file imposta la mappa di ogni regione contenuta; una regione non ha un campo `map`.

```toml
[[region]]
name = "The Heartwood"
type = "town"
priority = 50
areas = ["(6911, 255)..(7168, 512)"]
go_location = "(6984, 337, 0)"
entrance = "(535, 995, 0)"
music = "ElfCity"
weather = "none"
guarded = true
housing = false
```

| Campo | Significato | Valore predefinito |
| --- | --- | --- |
| `name` | Nome della regione. Omettilo per le aree che applicano solo regole. | nessuno |
| `type` | `base`, `town`, `dungeon`, `nohousing`, `guarded`, `jail` o `greenacres` (`RegionType`). | `base` |
| `priority` | Dove le regioni si sovrappongono, si applica quella più alta. | 50 |
| `parent` | Nome della regione di cui questa fa parte, sulla stessa mappa. | nessuno |
| `areas` | Rettangoli della regione. | obbligatorio |
| `go_location` | Dove un comando "vai alla regione" porta un personaggio, un `Point3D`. | nessuno |
| `entrance` | Ingresso della città o del dungeon, un `Point3D`. | nessuno |
| `music` | Brano musicale, un nome `MusicType` come `Britain1`, riprodotto ai giocatori che entrano nella regione; in sua assenza suona la musica del genitore più vicino, altrimenti `music` della mappa. | nessuno |
| `season` | Stagione mostrata dal client nella regione (`spring`, `summer`, `fall`, `winter`, `desolation`), per esempio inverno in un dungeon di ghiaccio; non ruota mai. In sua assenza si applica la stagione del genitore più vicino, altrimenti quella della mappa. | nessuno |
| `weather` | Profilo di `weather.toml`. | `none` |
| `rune_name` | Nome di una runa marcata qui. | nessuno |
| `guarded` | Se le guardie proteggono la regione. Oggi il giocatore ne viene informato (vedi [Cosa legge un giocatore](#what-a-player-reads)), gli script possono leggerlo, e un giocatore che dice "guards" lì fa arrivare una guardia su un criminale vicino (vedi [`ultima.crime`](../server-configuration.md)). | `false` |
| `housing` | Se i giocatori possono posizionare case. | `true` |
| `instant_logout` | Se un personaggio senza combattimenti in corso lascia subito il mondo al logout. | `false` |
| `recall_in`, `recall_out`, `gate_in`, `gate_out`, `mark`, `teleport_in`, `teleport_out` | Se questi incantesimi di viaggio funzionano in ingresso, in uscita o nella regione. | `true` |

`MusicType` (`src/Moongate.Ultima/Types/MusicType.cs`) elenca i nomi dei brani; i suoi
valori seguono `Music/Digital/Config.txt` del client.

## Aree

Scrivi ogni rettangolo come `"(x1, y1)..(x2, y2)"`: due angoli, non una posizione
e una dimensione. Il primo angolo è incluso e il secondo è escluso. Per esempio,
`"(1330, 1991)..(1343, 2004)"` copre X da 1330 a 1342 e Y da 1991
 a 2003. Entrambi i punti usano la stessa notazione `(x, y)` di `Point2D`.

Senza limiti di altezza, usa direttamente stringhe:

```toml
areas = [
    "(1330, 1991)..(1343, 2004)",
    "(1494, 3767)..(1506, 3778)",
]
```

Per limitare l'altezza, usa una tabella inline con `bounds` e `z1` e `z2` facoltativi.
`z1` è incluso e `z2` è escluso. Ciascun limite può essere omesso indipendentemente;
quando nessuno dei due è presente, il rettangolo copre ogni altezza. Stringhe e tabelle
possono essere mescolate nello stesso array `areas`.

```toml
areas = [
    { bounds = "(1416, 1498)..(1740, 1777)", z1 = -10, z2 = 128 },
    { bounds = "(1500, 1408)..(1546, 1498)", z1 = 0, z2 = 128 },
]
```

Il loader accetta anche le vecchie tabelle `{ x1 = ..., y1 = ..., x2 = ..., y2 = ... }`.
La serializzazione scrive sempre il nuovo formato ad angoli, usando una stringa quando
non ci sono limiti di altezza e una tabella `bounds` altrimenti.

## Genitori e sovrapposizioni

Un `parent` registra che una regione fa parte di un'altra, come un edificio dentro
Britain. Solo `music` e `season` vengono ereditati quando il file viene letto: una regione
priva di uno dei due assume quello del genitore più vicino. Ogni regione scrive le altre regole,
già combinate con quelle dei genitori, così ciascuna può essere letta autonomamente.

Dove le regioni si sovrappongono, quella con `priority` maggiore fornisce nome, musica,
regole di guardie, case e logout. Il viaggio funziona diversamente: un incantesimo di viaggio viene
bloccato quando qualsiasi regione che copre il luogo lo blocca, indipendentemente dalla priorità.

A parità prevale il figlio sul genitore, poi la regione scritta per prima. Il server
conserva la regione in cui si trova ogni giocatore e registra un cambiamento a livello debug
(`"Aria" left Britain for Britain Graveyard`); `.where` stampa la regione del punto
selezionato: `Trammel (1496, 1628, 10) in Britain`.

## Cosa legge un giocatore

Entrando a piedi in un luogo nominato, o venendovi teletrasportato, un giocatore legge "Sei entrato in Britain.";
uscendone a piedi, "Hai lasciato Britain." Al login legge dove si trova una volta completato l'accesso.

Un luogo è una regione nominata che non è una semplice parte della regione circostante. Una regione il cui `parent`
ha lo stesso `type` e lo stesso `guarded` è una parte: un campo o un negozio dentro Britain è ancora
Britain, e spostarsi tra essi non dice nulla. Una regione diversa dal genitore è un luogo
proprio: New Haven, una città sorvegliata, si trova dentro Haven Island, che non ha guardie, quindi entrando a piedi dall'
isola si legge "Sei entrato in New Haven." e arrivando da altrove viene nominata prima l'isola e poi la
città. Una regione di `type = "guarded"`, come i moongate, indica solo le guardie e non viene mai nominata. Un
luogo con lo stesso nome, come la stessa città su un'altra mappa, è lo stesso luogo.

Quando cambia la protezione un giocatore legge "Ora sei sotto la protezione delle guardie di
Britain." oppure "Hai lasciato la protezione delle guardie di Britain." Le guardie prendono il nome del
luogo sorvegliato più vicino intorno al giocatore; quando non c'è, come presso un moongate o in una casa sorvegliata di
una città senza guardie, vengono usati i due testi del client (500112, 500113). I quattro testi sono
i messaggi da 30134 a 30137 di `data/messages`.

L'ingresso viene mostrato in verde, l'uscita in rosso, inclusi i due testi del client.

## Zone di viaggio

Le zone di viaggio sono regioni senza nome di priorità 0 che limitano solo il viaggio, come le
Lost Lands di Felucca:

```toml
[[region]]
type = "base"
priority = 0
areas = ["(5120, 2304)..(6144, 4096)"]
weather = "none"
recall_in = false
recall_out = false
gate_in = false
gate_out = false
mark = false
teleport_in = true
teleport_out = true
```

Il server legge tipo, musica, stagione e meteo della regione per i giocatori al suo interno.
Nessun sistema legge ancora le regole di guardie, case, logout e viaggio: vengono solo caricate e
validate.

## Validazione all'avvio

Il server si arresta quando:

- la directory `regions/` non esiste;
- un nome file non è un nome di mappa (il confronto ignora le maiuscole);
- una regione non ha aree;
- un'area ha limiti malformati, coordinate mancanti, `bounds` e campi di coordinate
  legacy mescolati, campi sconosciuti o limiti di altezza non interi;
- il secondo angolo non è superiore al primo sia su X sia su Y oppure, quando sono impostati entrambi i limiti
  di altezza, `z2` non è superiore a `z1`;
- `weather` di una regione non è un profilo di `weather.toml`;
- un nome viene usato due volte nello stesso file;
- un `parent` non è una regione dello stesso file, oppure i genitori formano un ciclo.

## Aggiungere una regione

1. Apri il file della mappa, come `data/regions/trammel.toml`.
2. Aggiungi un `[[region]]` con le sue `areas` e ogni regola esplicitata, incluse quelle
   del genitore. I campi omessi assumono i valori predefiniti della tabella sopra.
3. Assegna una `priority` superiore alle regioni che deve sostituire dove si sovrappongono.
4. Usa un profilo `weather` esistente in `weather.toml`.

## Vedi anche

- [Panoramica dei file di dati](../data-files.md): percorsi dei file, ordine di caricamento e formati condivisi dei valori.
- [Controllare le modifiche](../data-files.md#check-your-changes): validare i dati modificati prima di riavviare.
