<!-- translation: {"sourceHash":"5f2dcf9dd814f2f816b925d2ff17c055a130bcf5938148fd58361935e34d257e","title":"Enhanced Client"} -->

# Enhanced Client

Moongate accetta l'Enhanced Client (EC) insieme al client classico. Il supporto è parziale: questa
pagina spiega come connetterlo, cosa è stato visto funzionare con un client reale e cosa non è
ancora stato provato. L'ultimo test ha usato il client 4.0.117 (versione di protocollo `67.0.117.0`) il 2026-10-02.

## Connettere un Enhanced Client

L'EC cifra sempre la connessione, quindi il server deve accettare accessi cifrati. Imposta la
politica di cifratura e la versione di protocollo del client in `config/moongate.toml`:

```toml
[network.encryption]
mode = "Optional"
client_version = "67.0.117.0"
```

- `Optional` accetta l'EC cifrato e i client classici in chiaro sugli stessi listener.
  `Required` accetta solo client cifrati. Non esiste una modalità automatica.
- `client_version` è la **versione di protocollo**: la versione dell'eseguibile del client con 60 aggiunto
  al primo numero. Un eseguibile con versione del file `4.0.117.x` è `67.0.117.0`. L'ultimo
  numero non cambia le chiavi.
- Il server usa una sola versione per la decifratura. Un client classico cifrato di un'altra versione
  viene rifiutato mentre questo profilo è impostato.

Riavvia il server dopo la modifica. Il log di avvio indica il profilo:

```text
Client encryption: Optional; client 67.0.117.0; login XOR; game Twofish / MD5-XOR
```

e un accesso dall'EC mostra:

```text
Login client connected with version 67.0.117.0 (Enhanced)
```

Vedi [Cifratura del client UO](server-configuration.md#uo-client-encryption) per tutte le modalità e
le regole di validazione.

## Cosa funziona

Osservato con un client reale:

| Passaggio | Note |
| --- | --- |
| Accesso cifrato ed elenco dei server | Il server di accesso accetta le informazioni hardware (`0xD9`) che l'EC invia subito dopo l'accesso all'account. |
| Creazione del personaggio | `0x8D`. Il volto e lo stile della camicia inviati dall'EC non vengono conservati e non c'è scelta del colore dei pantaloni. |
| Ingresso nel mondo | Un personaggio creato entra subito, come dopo averne scelto uno dall'elenco. |
| Movimento | |

Realizzato per l'EC e coperto da test automatici, ma non ancora osservato con un client reale:

| Funzionalità | Note |
| --- | --- |
| Parlato | `0xAD` viene letto in modo tollerante: un codice lingua insolito, un terminatore mancante o un carattere codificato male non disconnettono più il client. |
| Contenitori | L'EC mostra un contenitore come una griglia. Ogni oggetto ha uno slot proprio (da 0 a 124), conservato nel database; un rilascio prende lo slot richiesto dal client oppure il successivo libero. Vedi [Pacchetti](packets.md). |
| Richiesta del profilo | `0xB8`, inviato dall'EC dopo l'ingresso nel mondo, viene accettato e ignorato: non viene mostrato alcun profilo. |
| Raffica di accesso | I pacchetti inviati dall'EC mentre il server sta ancora caricando il personaggio attendono, fino a 1024 per sessione, invece di disconnettere il client. |
| Effetti particellari | Un effetto con un id di particella viene inviato all'EC come `0xC7`; ogni altro client riceve il semplice `0xC0`. Vedi [Pacchetti](packets.md). |

## Cosa manca

- Tutto ciò che manca anche al client classico: vedi l'[elenco delle funzionalità](feature-checklist.md).
- La finestra del profilo del personaggio.
- La vecchia negoziazione AES/E3 di Kingdom Reborn.
- Tutto ciò che non è elencato sopra non è stato provato con l'EC.

## Quando il client viene disconnesso

L'EC invia pacchetti che il client classico non invia mai, e alcuni pacchetti noti in una
forma diversa. Il server chiude una connessione che invia qualcosa che non accetta, e il log
indica cosa fosse:

```text
Rejected packet from session 2, opcode 0xAD, name UnicodeSpeechRequestPacket, 20 bytes: AD0014...
Opcode 0xB8 is not registered as an incoming packet; 11 bytes buffered after opcode.
Rejected login packet from session 1, opcode 0xD9
Session 2 sent more than 1024 packets while a handler was running
```

- `Rejected packet` sul server di gioco: il pacchetto è noto ma il suo contenuto è stato rifiutato, oppure
  non ha un handler. La riga mostra i primi 64 byte.
- `sent more than 1024 packets while a handler was running`, seguito da `Rejected packet`:
  il contenuto era corretto, ma troppi pacchetti erano in attesa dietro un handler che legge il
  database.
- `is not registered as an incoming packet`: l'opcode è sconosciuto al server.
- `Rejected login packet`: il server di accesso non ha un handler per quell'opcode, oppure ha rifiutato il
  contenuto del pacchetto.

Apri una issue con la riga: i byte sono ciò che serve per aggiungere il pacchetto.
