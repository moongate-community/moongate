<!-- translation: {"sourceHash":"69699551aecce2c67b01bf44520153a358091f84f1b7c15dedfe42f0ff23b542","title":"Ricette TCP autonome"} -->

# Ricette TCP autonome

`Moongate.Network` è una libreria di trasporto autonoma per .NET 10. Non dipende
da sessioni di gioco, pacchetti UO o game loop. Questa guida usa un piccolo protocollo
i cui frame sono esattamente di due byte. Aggiungi un riferimento al pacchetto `Moongate.Network`.

## Confini dei frame

TCP può dividere un messaggio o combinare più messaggi in una lettura. Inserisci questo framer
in `PairFramer.cs`; il trasporto conserva i dati incompleti e lo chiama di nuovo:

```csharp
using Moongate.Network.Interfaces.Framing;

public sealed class PairFramer : INetFramer
{
    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 2;
        return buffer.Length >= frameLength;
    }
}
```

Un vero framer a lunghezza variabile deve validare l'intestazione e la dimensione dichiarata. Restituisci
false per un input incompleto; genera un'eccezione per uno malformato. La lunghezza di frame riportata
deve essere positiva e non superiore ai byte disponibili e a `MaxFrameLength`.
Un framer può trasformare i dati sul posto, ma deve trasformare ogni byte al massimo una volta
tra chiamate ripetute. Fornisci framer con stato nuovi per ogni connessione.

## Middleware

`INetMiddleware` vede blocchi grezzi **prima** del framing in ingresso, quindi non deve presumere
che una chiamata equivalga a un messaggio. Questo esempio di semplice passaggio va in `PassThroughMiddleware.cs`:

```csharp
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

public sealed class PassThroughMiddleware : INetMiddleware
{
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(MoongateTcpClient? client,
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(data);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(MoongateTcpClient? client,
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(data);
    }
}
```

Restituire memoria vuota scarta il contenuto e interrompe il resto della pipeline.
L'input del middleware è in prestito fino al completamento del `ValueTask` restituito. Non
conservarlo né restituire memoria il cui proprietario è stato rilasciato. Le chiamate di ricezione sono seriali
per connessione, così come gli invii, ma le due direzioni possono funzionare contemporaneamente; mantieni
separato lo stato mutabile in ingresso e in uscita. Non chiamare mai il
`SendAsync` dello stesso client dentro `ProcessSendAsync`: il suo lock di invio non è rientrante.

## Connettersi, scambiare un frame e arrestare

Questo `Program.cs` installa i callback prima che inizi la ricezione, copia i dati per la
continuazione esterna ed esegue gli invii fuori dai callback degli eventi. Ogni attesa di
operazione ha una scadenza; il rilascio rimane responsabile dello svuotamento del trasporto.

```csharp
using System.Net;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Data.Config;
using Moongate.Network.Server;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var token = timeout.Token;
var received = new TaskCompletionSource<(MoongateTcpClient Client, byte[] Bytes)>(
    TaskCreationOptions.RunContinuationsAsynchronously);
var reply = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

await using var server = MoongateTcpServer.CreateConfigured(
    new IPEndPoint(IPAddress.Loopback, 0),
    new TcpServerOptions
    {
        MaxFrameLength = 2,
        ConnectionPipelineFactory = () => new ConnectionPipeline
        {
            Framer = new PairFramer(),
            Middlewares = [new PassThroughMiddleware()],
            ConfigureClient = connection =>
            {
                connection.OnDataReceived += (_, e) =>
                    received.TrySetResult((e.Client, e.Data.ToArray()));
                connection.OnException += (_, e) => received.TrySetException(e.Exception);
            }
        }
    });
await server.StartAsync(token);

await using var client = await MoongateTcpClient.ConnectConfiguredAsync(
    server.Endpoint,
    new TcpClientOptions
    {
        MaxFrameLength = 2,
        Pipeline = new ConnectionPipeline
        {
            Framer = new PairFramer(),
            ConfigureClient = connection =>
            {
                connection.OnDataReceived += (_, e) => reply.TrySetResult(e.Data.ToArray());
                connection.OnException += (_, e) => reply.TrySetException(e.Exception);
            }
        }
    }, token);

// Two separate writes deliberately exercise stream reassembly.
await client.SendAsync(new byte[] { 0x12 }, token);
await client.SendAsync(new byte[] { 0x34 }, token);
var request = await received.Task.WaitAsync(token);
await request.Client.SendAsync(request.Bytes, token);
var response = await reply.Task.WaitAsync(token);
if (!response.AsSpan().SequenceEqual(new byte[] { 0x12, 0x34 }))
{
    throw new InvalidOperationException("TCP round trip failed");
}
await client.CloseAsync(token);
await server.StopAsync(token);
Console.WriteLine("Framed TCP exchange verified");
```

`Endpoint` espone l'indirizzo di ascolto e la porta assegnata dopo l'avvio, anche quando
la porta zero richiede una porta effimera. L'esempio apre solo loopback.
`ConnectConfiguredAsync` installa `ConfigureClient` dopo la preparazione del flusso ma
prima della ricezione. `ConnectAsync` avvia la ricezione prima di ritornare, quindi sottoscrivere
dopo quella chiamata può perdere una risposta precoce.

Esistono due diversi contratti di memoria: il
`TcpDataReceivedEventArgs.Data` della libreria TCP è una copia stabile e può essere conservato. I buffer
 dei middleware sono in prestito. Anche il contratto `INetworkService.DataReceived` dell'host è
in prestito fino al ritorno del callback, anche quando un trasporto specifico usa attualmente
memoria copiata. Decodifica o copia a quel confine dell'host prima di accodare lavoro.
Le copie esplicite sopra sono sicure per entrambi i modelli di proprietà.

## Configurazione e proprietà

I listener configurati hanno valori predefiniti di 32 connessioni ammesse, otto preparazioni di flusso
contemporanee e una scadenza di preparazione di cinque secondi. I valori predefiniti di client/server usano
un buffer di ricezione di 8192 byte, un frame massimo di 1 MiB e `NoDelay = true`. Le ammissioni
in eccesso vengono chiuse immediatamente. Una `ConnectionPipelineFactory` per connessione evita di
condividere stato di cifratura, codec, middleware o framing tra connessioni.

`PrepareStreamAsync` può avvolgere il flusso (per esempio, un flusso TLS autenticato).
Il flusso leggibile/scrivibile restituito deve possedere l'input. Osserva il token di
annullamento fornito e rilascia qualsiasi wrapper creato se la preparazione fallisce. Dopo
una configurazione riuscita, il trasporto possiede il flusso preparato e il socket. La preparazione viene eseguita
solo tramite i punti di ingresso configurati, `MoongateTcpServer.CreateConfigured` e
`MoongateTcpClient.ConnectConfiguredAsync` (da 0.4.0); il costruttore semplice e
`ConnectAsync` non hanno un passo di preparazione. Il server usa il trasporto per i listener dei client
UO; il coordinamento dei realm è basato su Redis.

Per un arresto ordinato, ferma i produttori e chiama `StopAcceptingAsync()` per chiudere il
listener/annullare la configurazione incompleta mentre le connessioni stabilite rimangono disponibili.
Svuota il lavoro dell'applicazione, poi chiama `StopAsync()` oppure rilascia il server per attendere
la pulizia delle connessioni. Un riavvio richiede che l'arresto completo termini. Non attendere mai sincronicamente
la pulizia delle connessioni dentro eventi di ricezione/disconnessione; i callback fanno parte
del lavoro atteso dalla pulizia. `Completion` rappresenta la pulizia completa della
connessione; `IsConnected == false` o un solo evento di chiusura non la rappresentano.

Questo esempio di framing non interpreta pacchetti UO. Vedi [Pacchetti e handler](packets.md)
e [Trasporto e proprietà dello stato di gioco](network-game-separation.md) per l'integrazione con l'host.


## Middleware di cifratura dell'host UO

L'host UO installa un nuovo `UoEncryptionMiddleware` tramite
`UoNetworkOptionsFactory` per ogni connessione di accesso/gioco accettata quando
`network.encryption.mode` è `Optional` o `Required`. Vedi
[configurazione della cifratura del client](server-configuration.md#uo-client-encryption).

I byte in ingresso passano attraverso la decifratura prima di `UoPacketFramer` o `GameSeedFramer`.
Il middleware conserva al massimo 86 byte di handshake: un seed grezzo di quattro byte oppure un seed
`0xEF` di 21 byte, seguito dall'accesso completo all'account di 62 byte o dall'accesso di gioco di 65 byte.
Prima verifica la presenza di testo in chiaro usando il contratto canonico `AccountLoginPacket.TryParse`
o `GameLoginPacket.TryParse`, compresa la validazione delle credenziali. `Optional`
accetta un accesso valido in chiaro; `Required` lo rifiuta. Altrimenti decifra con
il profilo configurato e richiede la stessa validazione completa del pacchetto prima di
rilasciare qualsiasi frame di seed o accesso. Le credenziali ASCII a larghezza completa e il padding dopo
un NUL seguono le normali regole del parser dei pacchetti.

Gli handshake frammentati attendono altro input; i byte uniti dopo l'accesso continuano
attraverso il cifrario stabilito senza decifrare un byte due volte. Sul listener di
accesso, i seed legacy grezzi diventano frame `0xEF` usando i componenti di versione da
`network.encryption.client_version`. I byte del seed di gioco e i seed versionati esistenti
rimangono intatti; i loro campi di versione non selezionano un altro profilo di cifratura.
I seed zero e l'handshake del seed legacy KR `0xFFFFFFFF` vengono rifiutati.

I dati di gioco in uscita passano prima attraverso `UoCompressionMiddleware` e poi attraverso il middleware di
cifratura; la compressione inizia solo quando abilitata per la sessione.
I profili POL coprono le varianti XOR di accesso vecchia, 1.25.36 e standard, i flussi di gioco
Blowfish, Blowfish/Twofish combinati e Twofish con MD5-XOR dal server al client.
Solo il profilo Twofish trasforma i byte di gioco in uscita; i profili Blowfish e combinato
li lasciano non cifrati, come fa POL. Le risposte del server di accesso rimangono in chiaro.
Lo stato del cifrario appartiene alla connessione, con posizioni indipendenti in ingresso e in uscita.
Il trasporto serializza le trasformazioni degli invii insieme alle scritture sul socket,
conservando l'ordine del cifrario anche quando i chiamanti inviano contemporaneamente.

Un seed o accesso rifiutato genera un'eccezione prima che qualsiasi frame di handshake venga inoltrato e
il trasporto chiude la connessione. Questa è validazione strutturale, non
cifratura autenticata: un profilo errato può teoricamente produrre un pacchetto valido.
Il listener usa un solo profilo configurato, senza rilevamento automatico della versione del client
né fallback ad altri profili. I test di vettori POL e loopback coprono i
flussi implementati, la frammentazione, l'unione e l'isolamento delle connessioni. Un Enhanced
Client reale 4.0.117 accede con il profilo `67.0.117.0`; vedi
[Enhanced Client](enhanced-client.md) per ciò che funziona oltre l'accesso.

I vettori di riferimento in `tests/Moongate.Network.Packets.Tests/TestSupport/Encryption`
provengono dagli algoritmi C++ originali di POL. Per rigenerarli da un checkout di POL:

```sh
python3 tests/Moongate.Network.Packets.Tests/TestSupport/Encryption/generate-pol-vectors.py /path/to/polserver
```

Il generatore richiede Python 3, Git e un compilatore `g++` C++20. Riscrive
`pol-vectors.json` e registra il commit del checkout sorgente. La produzione usa implementazioni dei cifrari in
C# e l'API MD5 di .NET, senza dipendenza da un binario POL.
