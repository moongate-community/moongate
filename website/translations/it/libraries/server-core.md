<!-- translation: {"sourceHash":"bd4c5736ff4a127ecb74f98d7360e3cf5b0deff46abd75366a849a2111f808b1","title":"Moongate.Server.Core"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Server.Core

Contratti server rivolti ai plugin, eventi, registri ed estensioni di dependency injection per Moongate.

## Installazione

Richiede .NET 10. Usa la versione del pacchetto disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Server.Core
```

## Funzionalità

- Contratti, metadati e API di registrazione dei plugin.
- Eventi del ciclo di vita del server e bus degli eventi integrato con DryIoc.
- Registrazioni per servizi del server, gestori dei pacchetti e comandi.
- Contratti e tipi di dati per connessioni di trasporto, sessioni, game loop, timer, salvataggi del mondo e diagnostica.

## Esempio

Registra il bus degli eventi e iscriviti a un evento del ciclo di vita. Questo esempio
pubblica l'evento manualmente per mostrare il bus; non avvia un server Moongate.

<!-- nuget-smoke:Program.cs -->

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;

using var container = new Container();
container.RegisterMoongateEventBus();
var bus = container.Resolve<IMoongateEventBus>();

using var subscription = bus.Subscribe<MoongateStartedEvent>((_, cancellationToken) =>
{
    Console.WriteLine("Started");
    return Task.CompletedTask;
});

await bus.PublishAsync(new MoongateStartedEvent());
```

`SubscribeAll` registra un gestore invocato per ogni evento pubblicato, indipendentemente
dal tipo — eseguito dopo gli iscritti tipizzati dell'evento, nell'ordine di iscrizione.
Ogni iscrizione generale è indipendente e restituisce un proprio token rilasciabile,
esattamente come `Subscribe<TEvent>`; rilasciarne una non influisce mai su un'altra.

## Dipendenze e ambito

Questo pacchetto dipende da `Moongate.Core`, `Moongate.Network` e
`Moongate.Network.Packets`. DryIoc è disponibile tramite il grafo delle dipendenze.

L'host eseguibile e le implementazioni dei servizi runtime del server sono forniti
da `Moongate.Server`, che non è distribuito come parte di questo pacchetto libreria.
Referenziare questo pacchetto non avvia host, listener, timer o game loop.

## Connessioni e coordinamento del gioco

`IConnectionService` tiene traccia delle connessioni di trasporto indipendentemente
dalle sessioni di gioco. Il suo `TryGet` restituisce solo connessioni vive la cui
ammissione è ancora aperta. `DisconnectAsync` chiude immediatamente l'ammissione e
attende sia la richiesta di chiusura sia il completamento effettivo del trasporto.
Le connessioni in chiusura restano in `Count` e negli snapshot di appartenenza fino
alla fine della pulizia. L'arresto è terminale e riporta gli errori di pulizia.
L'overload `TryGet` a tre argomenti cattura anche un segnale di disconnessione richiesto
dal proprietario in modo atomico con la connessione; il mittente usa quel segnale
stabile per classificare le scritture interrotte intenzionalmente. La chiusura remota
o un errore di invio che chiude il proprio trasporto non lo completa.

`INetworkService` gestisce i listener e genera gli eventi sincroni `ConnectionAccepted`,
`DataReceived` e `ConnectionClosed`. **La memoria ricevuta è presa in prestito fino
al ritorno del callback:** decodificala o copiala prima di accodare lavoro altrove.
Una notifica di chiusura precede la pulizia completa del trasporto; non attendere mai
sincronicamente quella pulizia nel callback. `NetworkListenerOptions` fornisce endpoint
e una factory facoltativa di pipeline per connessione.

`IGameServerService` coordina sessioni di gioco e dispatch dei pacchetti sopra quel
confine. `IPacketSendService` invia tramite il registro delle connessioni senza
richiedere una sessione di gioco. L'host eseguibile fornisce queste implementazioni
e il relativo ordine di avvio.

### Compatibilità

Dalla versione 0.2.0, le sessioni contengono un `INetworkConnection` anziché un
`MoongateTcpClient`; consumer e plugin compilati per 0.1.x devono essere ricompilati.
Vedi [Proprietà di trasporto e gioco](https://moongate.sh/server/network-game-separation/)
per l'esempio di composizione, la sequenza di arresto e le note di aggiornamento.

## Guide runtime

Vedi [Pacchetti e gestori](https://moongate.sh/server/packets/),
[Game loop e timer](https://moongate.sh/server/game-loop-and-timers/)
e [Salvataggi del mondo](https://moongate.sh/server/persistence/)
per esempi di registrazione, threading, completamento e arresto.

## Licenza e sorgente

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository sorgente e la licenza](https://github.com/moongate-community/moongate).
