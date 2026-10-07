<!-- translation: {"sourceHash":"522fc003a59a74fb7be90ffed691b0b7414b2126c7d3738e925ac1c6bdf7ebd9","title":"Ciclo di gioco e timer"} -->

# Ciclo di gioco e timer

Il ciclo di gioco gestisce mutazioni sincrone del mondo, handler sincroni dei pacchetti e callback dei
 timer su un thread dedicato. Gli handler asincroni dei pacchetti vengono eseguiti fuori dal ciclo e reinviano le
modifiche di stato tramite `PacketContext.RunOnGameLoopAsync`. I/O dei socket e scritture
su disco appartengono all'esterno di quel thread. Inietta `IGameLoopService` e
`ITimerService` nei componenti host/plugin;
i loro contratti sono in `Moongate.Server.Core` e le implementazioni nel
progetto eseguibile `Moongate.Server`.

## Pianificazione e limiti

Non c'è **una frequenza fissa dei tick del ciclo di gioco**. Il ciclo svuota un batch limitato di
comandi, elabora i timer scaduti, poi attende nuovo lavoro o la scadenza del prossimo timer
quando inattivo. La **risoluzione predefinita di 8 ms** della ruota dei timer arrotonda le scadenze
verso l'alto; non garantisce un aggiornamento della simulazione ogni 8 ms.

| Oggetto di opzioni | Proprietà | Valore predefinito |
| --- | --- | --- |
| `GameLoopOptions` | `QueueCapacity` | 4096 elementi in coda, escluso l'elemento attivo |
| `GameLoopOptions` | `MaxWorkItemsPerBatch` | 256 elementi tentati |
| `GameLoopOptions` | `WorkItemBudget` | 5 ms per batch |
| `TimerWheelOptions` | `TickDuration` | 8 ms |
| `TimerWheelOptions` | `WheelSize` | 512 bucket |
| `TimerWheelOptions` | `MaxPendingTimers` | 65.536 registrazioni |
| `TimerWheelOptions` | `MaxCallbacksPerBatch` | 256 callback tentate |
| `TimerWheelOptions` | `CallbackBudget` | 5 ms per batch |

Questi valori positivi sono opzioni C# registrate in `Program.cs`, non campi TOML.
I budget di tempo trascorso sono cooperativi: un elemento/callback in esecuzione non viene mai interrotto.
Un handler costoso può superare il budget e ritardare ogni giocatore e timer.
I timer ripetuti usano scadenze a frequenza fissa, accorpano le occorrenze perse dopo una
pausa e non eseguono mai callback sovrapposte sul ciclo.

## Ammissione e completamento sono diversi

`TryPost(item)` restituisce false se il ciclo è pieno o non attivo; non esegue mai
in linea. `PostAsync(item, token)` attende spazio in coda e termina all'ammissione,
non all'esecuzione. Attendi ogni chiamata del produttore affinché i produttori in attesa non crescano
senza limite. L'annullamento prima dell'accettazione rifiuta l'elemento; dopo l'accettazione non
lo ritira. Le chiamate a `PostAsync` dal thread del ciclo sono rifiutate.

Porta un task di completamento quando un chiamante esterno richiede il risultato. Questo
`ReadValueWorkItem.cs` mantiene le continuazioni fuori dal ciclo e lascia fatali le eccezioni
inattese, informando anche il chiamante:

```csharp
using Moongate.Server.Core.Interfaces.GameLoop;

public sealed class ReadValueWorkItem : IGameLoopWorkItem
{
    private readonly Func<int> _read;
    private readonly TaskCompletionSource<int> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Completion => _completion.Task;

    public ReadValueWorkItem(Func<int> read)
    {
        _read = read;
    }

    public void Execute()
    {
        try
        {
            _completion.TrySetResult(_read());
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
            throw;
        }
    }
}
```

Il seguente `Program.cs` è eseguibile in un progetto console .NET 10 che fa riferimento al
sorgente `src/Moongate.Server/Moongate.Server.csproj`; l'implementazione non è un
package NuGet. I plugin usano i servizi iniettati già avviati invece di
costruire un altro ciclo:

```csharp
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;

var timers = new TimerWheelService(new TimerWheelOptions(), TimeProvider.System);
using var loop = new GameLoopService(new GameLoopOptions(), timers, TimeProvider.System);
await timers.StartAsync();
await loop.StartAsync();
try
{
    var item = new ReadValueWorkItem(() => loop.IsOnLoopThread ? 42 : -1);
    await loop.PostAsync(item);
    var winner = await Task.WhenAny(item.Completion, loop.Completion)
        .WaitAsync(TimeSpan.FromSeconds(5));
    if (winner == loop.Completion)
    {
        await loop.Completion; // Propagate the original fatal loop error, if any.
        throw new InvalidOperationException("Loop stopped before the result");
    }
    if (await item.Completion != 42)
    {
        throw new InvalidOperationException("Wrong execution thread");
    }

    var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    timers.RegisterTimer("once", TimeSpan.FromMilliseconds(16), () => fired.TrySetResult());
    await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var repeated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var repeatId = timers.RegisterTimer("pulse", TimeSpan.FromMilliseconds(16),
        () => repeated.TrySetResult(), repeat: true);
    await repeated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    timers.UnregisterTimer(repeatId);
    Console.WriteLine("Loop completion and timer callbacks verified");
}
finally
{
    await loop.StopAsync();
    await timers.StopAsync();
}
await loop.Completion;
```

Non chiamare `.Wait()`/`.Result` su un task che richiede questo ciclo dall'interno di un handler.
Usa `IsOnLoopThread` quando un'API supporta un percorso sincrono diretto sul thread proprietario.
Copia i dati di rete presi in prestito prima di accodarli; vedi [Pacchetti e handler](packets.md).

## Ciclo di vita dei timer

`RegisterTimer(name, interval, callback, delay: ..., repeat: ...)` restituisce un
ID stringa opaco. `interval` e l'eventuale primo `delay` devono essere positivi; senza
ritardo la prima callback scade dopo l'intervallo. La registrazione è consentita prima dell'
avvio, ma fallisce dopo l'arresto o quando la capacità di registrazione è esaurita.
Sono consentiti nomi logici duplicati.

Usa `UnregisterTimer(id)` per una registrazione, `UnregisterTimersByName(name)` per
le registrazioni nominate di un componente oppure `UnregisterAllTimers()` quando possiedi
l'intero servizio. L'annullamento impedisce una callback non ancora acquisita; una già in esecuzione
può terminare. I servizi devono conservare i propri ID e annullarne la registrazione quando
si arrestano. Evita callback `async void`: il lavoro asincrono richiede un worker
esplicitamente gestito e trattamento di completamento/errori.

## Tick degli NPC

Il plugin Ultima assegna a ogni NPC vicino a un giocatore un timer ripetuto `npc_think`
(`INpcTickService`), come l'`AITimer` di ModernUO. Un NPC è vicino a un giocatore quando si trova
in uno dei 5×5 settori di 16×16 caselle intorno al settore di un giocatore. Il primo ciclo di pensiero
arriva dopo un intervallo casuale di 1–256 ms, così gli NPC risvegliati insieme non pensano nello stesso
 tick, poi uno ogni `ultima.npcs.think_interval_ms` (valore predefinito 500 ms).

I settori attivano e disattivano i timer: quando un settore riceve il primo giocatore vicino, gli NPC
si svegliano, e quando l'ultimo se ne va restano inattivi e i timer vengono rimossi. Un
NPC inattivo non ha timer e non costa nulla sul ciclo. Un giocatore che attraversa il confine di un
settore non riavvia gli NPC coperti da entrambe le posizioni, e un NPC che attraversa un confine
all'interno dell'area attiva conserva il timer.

Ogni ciclo di pensiero chiama l'`INpcThinker` registrato, se presente. Un'eccezione generata viene
registrata con il seriale dell'NPC e l'NPC resta attivo: non raggiunge mai la ruota dei
 timer. Le metriche `npcs` contano gli NPC attivi e i cicli di pensiero.

## Errori, arresto e osservazione

Un'eccezione che esce da un normale `IGameLoopWorkItem.Execute` è fatale al ciclo
e porta in errore il suo task stabile `Completion`. Gli elementi accettati dopo quell'errore possono
non essere mai eseguiti, per questo i chiamanti osservano sia il proprio risultato sia il completamento del ciclo.
Le eccezioni delle callback dei timer sono isolate e conteggiate; non mandano in errore il ciclo.
Gestisci gli errori di dominio attesi nell'handler e riporta deliberatamente un risultato.

Il normale `StopAsync()` chiude ammissione e timer, completa il lavoro accettato e attende
il thread del ciclo. Esegue la pulizia anche dopo un errore; esamina `Completion` per
l'errore originale. Stop non può interrompere forzatamente un handler bloccante e non ha un
 timeout forzato. L'overload terminale `StopAsync(finalWorkItem)` aggiunge un'ultima acquisizione
sincrona dopo il completamento e propaga l'errore di acquisizione/ciclo; viene usato
dal salvataggio del mondo. Non chiamare nessuna operazione di arresto dal thread del ciclo.

`GetMetricsSnapshot()` sul ciclo e sui timer espone pressione della coda, tempi,
rifiuti, ritardi ed errori. L'host predefinito include entrambi i provider di metriche.
Vedi [Diagnostica](diagnostics.md), [provider di metriche personalizzati](metric-providers.md)
e [salvataggi del mondo](persistence.md) per l'integrazione.
