<!-- translation: {"sourceHash":"5e7e0ba1be7bfe859cff44acbddbf43ceba817dcf5f52fff858783044de53d74","title":"Registrare un provider di metriche"} -->

# Registrare un provider di metriche

Un provider di metriche è una classe che consegna al servizio diagnostico un gruppo
nominato di campioni a ogni ciclo di raccolta; l'autore di un plugin ne scrive uno
per far comparire contatori e gauge del plugin nello snapshot diagnostico accanto
alle metriche incorporate. Questa pagina tratta il contratto che deve soddisfare,
il funzionamento di errori e threading e i test. Per configurazione del raccoglitore
e metriche incorporate, vedi [Diagnostica](diagnostics.md); per dove collocare la
chiamata di registrazione in un plugin, vedi
[Registrare provider di metriche](plugins.md#what-register-may-do).

## Il provider di esempio

Il plugin di esempio conta i saluti in un `GreetingCounter` condiviso basato su
`Interlocked`, perché game loop e thread console lo scrivono e il thread diagnostico
lo legge. Il provider che lo riporta:

```csharp
using Moongate.Sample.Plugin.Internal;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Sample.Plugin.Diagnostics;

public sealed class GreetingMetricProvider : IMetricProvider
{
    private readonly GreetingCounter _counter;

    public string ProviderName => "greeter";

    public GreetingMetricProvider(GreetingCounter counter)
    {
        _counter = counter;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetricSample> samples = [new MetricSample("hello_calls", _counter.Count, "calls", DiagnosticMetricType.Counter)];

        return new ValueTask<IReadOnlyList<MetricSample>>(samples);
    }
}
```

Il plugin registra prima il contatore come istanza, così modulo Lua e provider lo
condividono, poi chiama `container.AddMetricProvider<GreetingMetricProvider>()`.
Ogni raccolta produce un campione, `hello_calls`, pubblicato dal servizio come
`greeter.hello_calls`.

## Il contratto

`IMetricProvider` ha due membri:

```csharp
public interface IMetricProvider
{
    string ProviderName { get; }

    ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default);
}
```

**`ProviderName`** deve essere stabile e univoco. Il servizio legge una volta il
nome di ogni provider quando viene costruito e lo valida lì; modificare la proprietà
su un'istanza viva non ha effetto. Due provider con lo stesso nome fanno fallire il
servizio alla costruzione con `Duplicate diagnostic provider name '{name}'.`,
un errore di avvio, non per ciclo.

**`CollectAsync`** deve restituire una lista nuova a ogni chiamata, rispettare il
token di annullamento prima di accedere alla sorgente e può generare
`OperationCanceledException`. Il servizio non chiama mai due provider insieme
né chiama un'istanza in concorrenza con sé stessa.

**`MetricSample`** contiene `Name`, `Value` (un `double`), `Unit` e `Type`.
`DiagnosticMetricType` ha due valori. Usa `Gauge` per un valore che può salire o
scendere tra campioni, come `game_loop.queue_depth`. Usa `Counter` per un valore
che si accumula soltanto dall'avvio del processo, come
`game_loop.accepted_work_items_total`. Il servizio rifiuta un `Counter` negativo,
ma non può rilevare un contatore che si azzera a un numero non negativo più piccolo;
questo rimane un contratto tra provider e consumer.

**Nomi.** La chiave dello snapshot è `ProviderName + "." + sample.Name`. Entrambe
le parti devono corrispondere a `[a-z][a-z0-9_]*`, quindi il nome locale è solo la
foglia, in snake_case, senza punto; il servizio aggiunge il prefisso. I nomi locali
devono essere univoci nel risultato di un provider. Le unità sono nomi brevi nello
stile della tabella incorporata: `calls`, `bytes`, `seconds`, `count`.

## Raccolta ed errori

All'intervallo `[diagnostics] interval_seconds`, il servizio attende ogni provider
registrato a turno sul proprio thread worker, fuori dal game loop. Valida ogni
campione (nome, unità non vuota, valore finito, tipo noto, contatore non negativo)
e rifiuta duplicati nello stesso provider. Tutti i campioni di un ciclo confluiscono
in un nuovo `DiagnosticSnapshot`, che sostituisce il precedente, viene restituito
da `IDiagnosticService.GetSnapshot()` e pubblicato sul bus degli eventi come
`DiagnosticSnapshotCollectedEvent`. Con `log_metrics = true` il ciclo scrive anche
una riga di log strutturata.

Un provider che genera un'eccezione, o il cui risultato non supera la validazione,
non interrompe il ciclo. Il suo nome viene aggiunto a `DiagnosticSnapshot.FailedProviders`,
ogni campione prodotto in quel ciclo viene scartato, viene registrato l'avviso
`Diagnostic provider {ProviderName} failed` e la raccolta continua col provider
successivo. Lo snapshot viene comunque costruito e pubblicato senza le metriche
di quel provider. Poiché l'insieme delle metriche è ricostruito a ogni ciclo,
un campione precedente riuscito non rimane in un ciclo in cui il provider fallisce.

L'unica eccezione è l'annullamento dal token di arresto del servizio stesso:
interrompe il ciclo senza costruire o pubblicare uno snapshot, perché è il servizio
che si ferma, non un provider che fallisce.

## Threading

La raccolta avviene fuori dal game loop. Un provider la cui metrica dipende da stato
posseduto dal loop ha due opzioni sicure:

- **Un campo atomico** che il loop e ogni altro thread possono scrivere senza
  passaggi, come `GreetingCounter` fa con `Interlocked`.
- **Un elemento di lavoro accodato al loop.** `IGameLoopService.PostAsync` si completa
  all'accettazione, non all'esecuzione, quindi l'elemento deve avere il proprio
  completamento (un `TaskCompletionSource` impostato da `Execute()`). Il provider
  attende quel completamento, separatamente dal `ValueTask` restituito da `PostAsync`,
  prima di restituire i campioni. Vedi [Game loop e timer](game-loop-and-timers.md).

Non bloccare mai sincronicamente sul lavoro del loop dentro `CollectAsync`: i
provider vengono eseguiti uno per volta, quindi uno bloccato ritarda ogni provider
accodato dopo di lui per quel ciclo.

## Test

`tests/Moongate.Tests/TestSupport/Diagnostics/` contiene un provider basato su
delegati e uno sospendibile. Collega uno dei due a un vero `DiagnosticService`
con un `TimeProvider` simulato, avanza il timer, poi verifica `GetSnapshot()` e l'evento.

Il provider del plugin di esempio è esercitato da un capo all'altro in
[Testare un plugin](plugins.md#testing-a-plugin): il test carica il plugin tramite
il vero loader, chiama greeter una volta da Lua e una dal comando console,
raccoglie tramite il servizio e verifica che `greeter.hello_calls` sia `2` con
`FailedProviders` vuoto.

## Errori comuni

- **Restituire la stessa lista mutabile a ogni chiamata.** Il codice che conserva
  uno snapshot precedente costruito da quella lista può vederla cambiare sotto
  un oggetto che dovrebbe essere immutabile. Alloca una nuova lista per chiamata.
- **Bloccare sul loop da `CollectAsync`.** I tick arrivati mentre una raccolta è
  bloccata si accorpano in un ciclo successivo; non eseguono prima il provider
  bloccato. Attendi invece il completamento proprio di un elemento di lavoro.
- **Nomi dei provider che collidono.** Il servizio fallisce alla costruzione,
  prima di qualsiasi raccolta.
- **Riportare un contatore che si azzera.** Un contatore che scende a un numero
  non negativo inferiore supera la validazione ma viola il significato di
  "accumula dall'avvio". Se un valore può legittimamente scendere, è un `Gauge`.
