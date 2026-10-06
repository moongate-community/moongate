<!-- translation: {"sourceHash":"de443777d05b8f60f6b080f8675e3e556b5adb0480d3c96ce410e42e250f331b","title":"Moongate.Network"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network

Trasporto TCP asincrono standalone con framing, middleware e pipeline per connessione per applicazioni .NET.

## Installazione

Richiede .NET 10. Usa la versione del pacchetto disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Network
```

## Funzionalità

- API asincrone per server e client TCP.
- Accesso all'endpoint associato al server, compresa una porta assegnata automaticamente.
- Framing, codec di trasporto e middleware configurabili.
- Pipeline per connessione per la gestione delle connessioni specifica dell'applicazione.

## Esempio

Avvia un listener loopback su una porta disponibile e arrestalo correttamente:

<!-- nuget-smoke:Program.cs -->

```csharp
using System.Net;
using Moongate.Network.Server;

await using var server = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0));
await server.StartAsync(CancellationToken.None);

if (server.Endpoint.Port == 0)
{
    throw new InvalidOperationException("The listener did not bind to a port.");
}

await server.StopAsync(CancellationToken.None);
Console.WriteLine("TCP listener started and stopped.");
```

Vedi il [ricettario TCP standalone](https://moongate.sh/libraries/network-cookbook/)
per uno scambio client/server eseguibile con framing, middleware per connessione e pulizia.

## Dipendenze e ambito

Questo pacchetto dipende da Serilog e non ha dipendenze da altri pacchetti Moongate.

TCP è un flusso di byte. Configura il framing per il protocollo della tua applicazione;
le letture del trasporto non definiscono i confini dei pacchetti di gioco.
Questa libreria non decodifica automaticamente i pacchetti Ultima Online né invia
lavoro a un game loop. Usa `Moongate.Network.Packets` per definizioni e serializzazione dei pacchetti UO.

## Metadati della connessione astratta

`INetworkConnection` espone identità del trasporto, completamento, operazioni di
invio/chiusura e metadati dell'endpoint remoto. La proprietà facoltativa `LocalEndPoint`
ha un'implementazione predefinita dell'interfaccia che restituisce null, quindi le
implementazioni personalizzate esistenti non richiedono un nuovo membro.
`MoongateTcpClient` fornisce il proprio endpoint locale effettivo.

```csharp
string DescribeLocalEndpoint(Moongate.Network.Interfaces.Client.INetworkConnection connection)
{
    return connection.LocalEndPoint?.ToString() ?? "Local endpoint unavailable";
}
```

I payload degli eventi TCP sono copie stabili; gli input dei middleware sono presi
in prestito fino al completamento del loro `ValueTask`. La proprietà delle connessioni
sopra il trasporto (sessioni di gioco, `IConnectionService`, `INetworkService`) vive
in `Moongate.Server.Core`, non qui.

## Punti di ingresso configurati

Dalla versione 0.4.0, `MoongateTcpServer.CreateConfigured(endpoint, options)` e
`MoongateTcpClient.ConnectConfiguredAsync(options)` preparano un flusso di trasporto
e installano i callback prima che inizi la ricezione. `ConnectionPipeline.PrepareStreamAsync`
può avvolgere il flusso del socket, per esempio per autenticare uno `SslStream`, e
`ConfigureClient` si iscrive agli eventi di ricezione prima che venga consegnato il
primo byte. Le regole:

- Il token di preparazione copre la scadenza di connessione/configurazione e
  l'annullamento del chiamante; i callback di preparazione devono rispettarlo.
- Il flusso restituito deve possedere il proprio flusso di input. Se la preparazione
  genera un'eccezione, rilascia qualsiasi wrapper creato prima di rilanciarla.
  Dopo una configurazione riuscita, il trasporto possiede flusso preparato e socket,
  e un errore di configurazione rilascia entrambi.
- `TcpServerOptions` limita connessioni ammesse e preparazioni concorrenti; i socket
  in eccesso si chiudono subito. Un handshake TLS lento non blocca l'accettazione di
  un'altra connessione. L'arresto del server annulla la preparazione e drena le
  configurazioni ammesse prima di un riavvio.

Il costruttore semplice e `ConnectAsync` non hanno una fase di preparazione e
mantengono il comportamento originale di ammissione.

## Arresto graduale

Per un arresto graduale dell'applicazione, `StopAcceptingAsync()` chiude il listener
e annulla la preparazione incompleta dei flussi mentre le connessioni stabilite
restano utilizzabili. `IsRunning` diventa false durante il drenaggio, mentre
`Endpoint` conserva uno snapshot sicuro dell'indirizzo e della porta associati.
Drena il lavoro dell'applicazione, poi chiama `StopAsync()` o `DisposeAsync()`.
Una nuova generazione del listener richiede un arresto completo prima del riavvio.

## Licenza e sorgente

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository sorgente e la licenza](https://github.com/moongate-community/moongate).
