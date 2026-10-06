<!-- translation: {"sourceHash":"07cfc019e48170011a790d61a518759877c6afff09e647de846c4e0ed23cf928","title":"Trasporto e proprietà dello stato di gioco"} -->

# Trasporto e proprietà dei servizi di gioco

L'host separa la durata della connessione TCP da quella della sessione di gioco. `mode =
"login"` esegue una pipeline asincrona ordinata dedicata ai pacchetti di accesso, l'accesso ad Accounts e
l'elenco dei realm. `mode = "game"` esegue il game loop, i servizi del mondo e la presenza dei
realm su Redis. `standalone` combina i servizi degli account e di gioco e pubblica il proprio
realm locale tramite Redis. Accesso e gioco possiedono registri delle
connessioni, mittenti, dispatcher dei pacchetti e listener TCP separati. I valori predefiniti
sono `network.login_port = 2593` e `network.game_port = 2595`; standalone rifiuta
la stessa porta per entrambi i ruoli. Con `network.listen_address = "0.0.0.0"`, ogni ruolo
si collega una volta per indirizzo locale rilevato, quindi standalone avvia due listener per
indirizzo. Il realm locale annuncia `network.game_port` salvo che
`realm_directory.advertised_port` sia impostato.

| Componente | Responsabilità |
| --- | --- |
| `ConnectionService` | Appartenenza locale al ruolo, ammissione e chiusura completa delle connessioni |
| `NetworkService` | Listener, pipeline di protocollo per connessione e notifiche sincrone del trasporto |
| `PacketSendService` | Snapshot codificati, code FIFO limitate e I/O in uscita di sua proprietà |
| `GameServerService` | Creazione delle sessioni, decodifica immediata dei pacchetti, inoltro e ritiro delle sessioni |
| `LoginServerService` | Connessioni di accesso indipendenti e gestione asincrona ordinata dei pacchetti |
| `RedisRealmDirectoryService` | Voci dei realm attivi basate su Redis, esposte all'accesso degli account |
| `PacketDispatchService` / `GameLoopService` | Handler tipizzati, lavoro di gioco ordinato e modifica dello stato di proprietà del loop |

Il trasporto e gli invii in uscita possono funzionare senza `ISessionService` o `IGameLoopService`.
Le implementazioni rimangono nell'eseguibile `mgserver` (il progetto `Moongate.Server`); i loro contratti pubblici
e i dati degli eventi risiedono nella libreria `Moongate.Server.Core`.

## Comporre un listener grezzo

```csharp
var connections = new ConnectionService();
var options = new NetworkListenerOptions
{
    Endpoints = [new IPEndPoint(IPAddress.Loopback, 2593)]
};
var network = new NetworkService(options, connections);

network.DataReceived += (_, args) =>
{
    // Copy inside the synchronous callback if another component needs the bytes later.
    byte[] ownedBytes = args.Data.ToArray();
    ProcessOwnedBytes(ownedBytes);
};

await connections.StartAsync();
try
{
    await network.StartAsync();
    await applicationShutdown;
}
finally
{
    try { await network.StopAsync(); }
    finally { await connections.StopAsync(); }
}
```

Il frammento usa `System.Net`, `Moongate.Server.Core.Data.Network` e
`Moongate.Server.Services.Network`. `ProcessOwnedBytes` e `applicationShutdown` sono
comportamenti forniti dall'applicazione. L'assenza di una factory di framing significa blocchi TCP grezzi, che non sono
confini di messaggio. Un listener specifico di protocollo fornisce `ConnectionPipelineFactory`.
L'host di gioco fornisce un nuovo `GameSeedFramer` per ogni connessione. Riconosce
un seed grezzo di riconnessione di quattro byte oppure un pacchetto seed versionato `0xEF`, poi delega
i pacchetti successivi a `UoPacketFramer`. Entrambi i framer usano il registro dei pacchetti che
l'host costruisce all'avvio. Il listener di gioco installa anche `UoCompressionMiddleware`,
che comprime con Huffman i dati in uscita quando la sessione abilita la compressione dopo un
`0x91` valido. Le opzioni vengono copiate alla costruzione, compresi gli oggetti endpoint.

## Regole dei callback e dell'arresto

1. La registrazione della connessione avviene prima di `ConnectionAccepted`; il coordinamento del gioco crea
   la propria sessione in quel callback. I callback di ricezione decodificano i pacchetti prima di accodarli.
2. `DataReceived.Data` è valido solo fino al ritorno del callback. Non accodare mai quella memoria.
   L'errore di un sottoscrittore chiude solo la sua connessione; i callback di chiusura vengono eseguiti indipendentemente, così
   un sottoscrittore che fallisce non può saltare la pulizia di un altro.
3. `DisconnectAsync` chiude immediatamente l'ammissione. Il suo task attende la richiesta di chiusura e
   il completamento effettivo. Non attendere mai sincronicamente dentro un callback del trasporto o un handler di gioco.
   Il registro/mittente mantiene la proprietà della pulizia anche quando il chiamante non può attenderla.
4. L'arresto del gioco chiude prima il trasporto, poi attende sia la pulizia del mittente sia il ritiro
   delle sessioni di proprietà del loop. Entrambe le operazioni iniziano anche se una genera un'eccezione. Tutti i listener vengono
   fermati dopo un errore di avvio parziale, conservando l'eccezione originale di avvio.
5. Le priorità di avvio sono registri delle connessioni **40**, mittenti **50**, dispatcher
   **60**, coordinatore di gioco **100** e coordinatore di accesso standalone **110**. Il loop
   parte prima. Ogni `NetworkService` è un semplice singleton avviato dal coordinatore del proprio
   ruolo, quindi non viene registrato per l'avvio automatico una seconda volta.
   L'arresto in ordine inverso mantiene attive le dipendenze di entrambi i ruoli durante il ritiro delle sessioni.

La capacità predefinita del mittente è 128 frame codificati in attesa per connessione, più una
scrittura attiva. Acquisisce copie dei pacchetti prima che un `TrySend` riuscito ritorni e conserva
l'ordine FIFO. Un overflow o un errore di codifica/invio chiudono l'ammissione e richiedono la chiusura.
Nessuna nuova coda di uscita può ammettere invii mentre la chiusura è in sospeso. Il mittente acquisisce atomicamente
un segnale di chiusura del proprietario per registrazione tramite l'overload a tre argomenti di `IConnectionService.TryGet`.
Quel segnale sopravvive alla rimozione dal registro e distingue una chiusura esplicita della connessione attiva
da un errore del trasporto o da un completamento remoto. Gli errori di pulizia vengono registrati e
rimangono osservabili all'arresto; le scritture interrotte da una chiusura locale richiesta sono previste.

## Aggiornare da 0.1.x

Da 0.2.0 il costruttore di `NetworkSession`, il tipo restituito da `NetworkSession.Client`
e il parametro di `ISessionService.GetOrCreate` usano `INetworkConnection` invece di
`MoongateTcpClient`. È una modifica dell'API binaria: ricompila i consumatori e i plugin.
Le chiamate esistenti che passano un client TCP concreto continuano a compilare. I servizi di sessione personalizzati
devono cambiare la firma del metodo. I servizi di rete personalizzati devono implementare i
tre eventi sincroni `ConnectionAccepted`, `DataReceived` e `ConnectionClosed`.

```csharp
// Before: session.NetworkSession.Client?.Dispose();
await connectionService.DisconnectAsync(session.SessionId);
// Or, when outgoing work must also be joined:
await packetSendService.DisconnectAsync(session.SessionId);
```

Dentro un handler sincrono di pacchetto, richiedi la pulizia di proprietà del mittente senza bloccare:
`_ = packetSendService.DisconnectAsync(session.SessionId);`.

`NetworkSession.DetachClient()` scollega solo i metadati di gioco e conserva le copie degli
endpoint; non chiude una connessione. Il suo stato disconnesso rimane terminale.
`INetworkConnection.LocalEndPoint` è opzionale e vale null per impostazione predefinita nelle implementazioni
personalizzate esistenti. Gestisci sempre l'assenza dei metadati locali.

La costruzione di `NetworkService` accetta `(NetworkListenerOptions, IConnectionService)`;
`PacketSendService` accetta `(IConnectionService, int capacity = 4096)`: i pacchetti contenuti nella coda di una sessione prima che venga chiusa perché non legge, dimensionati per la raffica di una vista piena di oggetti. Gli host personalizzati devono
aggiungere `GameServerService` se richiedono il percorso di sessione/inoltro UO. Registrare solo il servizio di
rete grezzo non esegue intenzionalmente alcuna decodifica di pacchetti né creazione di sessioni di gioco.

La separazione accesso/gioco cambia anche i contratti pubblici dei servizi per host e plugin personalizzati.
Le implementazioni personalizzate di `IPacketSendService` devono implementare
`SendAndDisconnectAsync(sessionId, expectedConnection, packet, cancellationToken)`:
inviare il pacchetto finale dopo i frame accodati, chiudere quella connessione e restituire `true`
solo quando il pacchetto finale ha raggiunto il trasporto prima della chiusura. Il vecchio
`IRealmDirectoryService` viene sostituito da `IRealmCatalog` per letture asincrone
`GetAvailableAsync`/`FindByIndexAsync` e da `IRealmPresenceService` per operazioni
asincrone sui lease `RegisterAsync`/`RenewAsync`/`UnregisterAsync`.
Standalone ora pubblica il proprio realm tramite Redis invece di `RegisterLocal`.
