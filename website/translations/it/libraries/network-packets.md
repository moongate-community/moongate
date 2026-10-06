<!-- translation: {"sourceHash":"a6abacbf13f18e6d4061168dcd20b1b9eae8ff128ae3be2947ade67fd0c5516f","title":"Moongate.Network.Packets"} -->

![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network.Packets

Definizioni dei pacchetti Ultima Online, serializzazione basata su span e registrazione dei pacchetti per Moongate.

## Installazione

Richiede .NET 10. Usa la versione del package disponibile nel feed NuGet configurato.

```shell
dotnet add package Moongate.Network.Packets
```

## Funzionalità

- Un insieme iniziale di pacchetti per ClassicUO 7.x, inclusi messaggi di login, controllo, ping e funzionalità supportate.
- Un registro dei pacchetti che usa metadati per registrazione e ricerca degli opcode.
- Lettori e scrittori basati su span per la serializzazione dei pacchetti.
- API di codifica e decodifica testabili indipendentemente da un server TCP.

## Esempio

Registra il pacchetto ping, congela il registro e decodifica un frame codificato:

<!-- nuget-smoke:Program.cs -->

```csharp
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;

var registry = new PacketRegistry();
registry.RegisterIncoming<PingPacket>();
registry.Freeze();

var bytes = PacketCodec.Encode(new PingPacket(42));

if (!registry.TryDecode(bytes, out var packet, out var opCode)
    || packet is not PingPacket ping)
{
    throw new InvalidOperationException($"Could not decode opcode {opCode:X2}.");
}

Console.WriteLine($"{opCode:X2}:{ping.Sequence}");
```

Vedi [Pacchetti e handler](https://moongate.sh/server/packets/)
per la tabella completa degli opcode integrati, un pacchetto personalizzato, test a livello di byte e integrazione nell'host.

## Dipendenze e ambito

Questo package dipende da `Moongate.Core`. Non richiede `Moongate.Network` per codificare o decodificare pacchetti.

L'insieme di pacchetti è un sottoinsieme del protocollo UO. La decodifica richiede un frame di pacchetto completo; buffering e framing di un flusso TCP
appartengono all'integrazione del trasporto. Handler dei pacchetti e dispatch sul ciclo di gioco appartengono all'integrazione del server.

## Licenza e sorgenti

Distribuito con licenza AGPL-3.0-or-later. Vedi il [repository dei sorgenti e la licenza](https://github.com/moongate-community/moongate).
