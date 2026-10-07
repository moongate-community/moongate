<!-- translation: {"sourceHash":"6d790191117ff954b0b414e94f7ff9a0df837772448f4ea9fc74b229c44db999","title":"Messaggio del giorno"} -->

# Messaggio del giorno

Modifica `<root>/data/motd.toml` per mostrare un messaggio privato in chat ogni volta che un personaggio entra nel mondo. Il repository distribuisce un esempio in `moongate_root/data/motd.toml`. Riavvia il server di gioco per applicare le modifiche.

```toml
lines = [
    "Welcome ${player_name} to ${server_name} (${codename} ${version})!",
    "Realm: ${realm_name} — players online: ${users_online}",
]
```

Ogni voce non vuota diventa un messaggio Unicode di sistema per il personaggio che entra, nell'ordine del file. Il messaggio viene inviato dopo che il client riceve la sequenza di ingresso nel mondo. Gli altri giocatori non possono vederlo.

## Variabili

I nomi delle variabili sono in inglese `snake_case` e usano la sintassi `${name}`.

| Variabile | Valore |
| --- | --- |
| `${version}` | Versione del server, senza metadati di compilazione |
| `${codename}` | Nome in codice della versione del server |
| `${server_name}` | `shard.shard_name` da `config/moongate.toml` |
| `${realm_name}` | Nome del realm di gioco attuale mostrato al client |
| `${player_name}` | Nome del personaggio che entra nel mondo |
| `${users_online}` | Personaggi connessi nel mondo su questo processo di gioco, incluso chi entra |

`users_online` non conta sessioni solo di accesso, sessioni disconnesse, personaggi non ancora nel mondo o giocatori su un altro processo di gioco.

## Validazione ed errori

Il ruolo game carica e valida il file all'avvio. Un file mancante registra un avviso e non invia alcun MOTD. TOML malformato, un array `lines` mancante, nomi di variabile non validi e variabili sconosciute interrompono l'avvio; gli errori includono il percorso del file e l'indice della riga quando pertinente. Le voci vuote o composte solo da spazi vengono saltate. Il testo fuori da un token `${...}` completo è letterale.

Un resolver di plugin che fallisce, oppure una riga generata che non entra in un pacchetto di parlato Unicode, fa saltare quella riga e registra un avviso senza memorizzare il testo generato. Le righe successive vengono comunque inviate. Se la sessione originale del personaggio si chiude o viene sostituita, la consegna si interrompe.

## Variabili dei plugin

Un plugin C# può registrare variabili in `IMoongatePlugin.Register`. Dichiara una dipendenza da `com.github.moongate-community.moongate.plugins.ultima` affinché il plugin Ultima abbia registrato prima il registro delle variabili. Aggiungi un riferimento a `Moongate.Server.Ultima` e importa `Moongate.Server.Ultima.Extensions`.

```csharp
container.RegisterMotdVariable(
    "season_name",
    (context, cancellationToken) => ValueTask.FromResult("Summer")
);
```

Poi usa `${season_name}` in `motd.toml`. I nomi devono corrispondere a `[a-z][a-z0-9_]*`; i nomi duplicati, compresi quelli predefiniti, fanno fallire la registrazione del plugin. La registrazione si chiude prima della validazione del file, quindi non è possibile aggiungere variabili a runtime. L'output del resolver viene inserito come testo letterale e non viene mai espanso di nuovo.
