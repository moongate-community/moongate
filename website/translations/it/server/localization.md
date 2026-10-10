<!-- translation: {"sourceHash":"bd96b9e65f0d0f4bc2392ecb03597fb3043cd7a50e41ba75ec2703b81ae1c603","title":"Localizzazione"} -->

# Localizzazione

I testi inviati dal server ai giocatori provengono da un'unica lingua per server, scelta in
`moongate.toml`. I testi risiedono in file TOML sotto `data/messages/` nella root
del server; il codice C# li legge tramite `ILocalizationService` in `Moongate.Server.Ultima`,
e gli script Lua tramite il modulo `localization`.

I testi già presenti nei file localizzati del client (numeri cliloc) non
richiedono questo servizio: il server invia il numero e il client li mostra nella
lingua del giocatore. Usa `ILocalizationService` per i testi scritti dal server stesso.

## Scegliere la lingua

Imposta il codice lingua nella sezione `[ultima.localization]`:

```toml
[ultima.localization]
language = "ita"
```

Il codice identifica il file `data/messages/<language>.toml` e la directory
`data/messages/<language>/`; il valore predefinito è `eng`. Il
server fornisce queste lingue:

| Codice | Lingua |
| --- | --- |
| `eng` | Inglese |
| `ita` | Italiano |
| `ger` | Tedesco |
| `fre` | Francese |
| `spa` | Spagnolo |
| `por` | Portoghese |
| `pol` | Polacco |
| `cze` | Ceco |

La lingua si applica all'intero server, nelle modalità game e standalone. Una modifica
ha effetto all'avvio successivo. Vedi la
[guida di riferimento della configurazione](server-configuration.md#settings-and-validation).

## File dei messaggi

Ogni file è una tabella `[messages]` di `number = "text"`:

```toml
[messages]
0 = "Questo oggetto non ha più cariche."
691 = "{0} è stato ucciso da {1}! [Terremoto]\n"
1737 = "[{0:x} {1:x} {2:x} {3:x}]"
```

- **Numero:** ID del messaggio. Lo stesso numero indica lo stesso messaggio in ogni lingua.
- **Testo:** formato composito .NET. `{0}`, `{1}`, ... sono i valori inseriti dal codice,
  in ordine. `{0,6}` completa un valore fino a 6 caratteri e `{0:x}` scrive un numero in esadecimale.
  Scrivi `{{` e `}}` per una parentesi graffa letterale.

I file provengono dai dizionari UOX3 (`data/dictionaries/dictionary.*`), con
le variabili printf UOX3 (`%s`, `%i`, `%d`) convertite in `{0}`, `{1}`, ...

### Ripiego sull'inglese

L'inglese è il riferimento e deve sempre esistere. Il server lo carica prima, poi
sostituisce ogni testo con quello della lingua scelta. Un messaggio mancante nella
lingua scelta resta in inglese, quindi una traduzione parziale funziona comunque. Il log
indica quanti messaggi sono tornati all'inglese:

```text
Found 5648 messages in ita, 3 of them in English
```

### Validazione all'avvio

`MessagesLoader` arresta il server all'avvio quando:

- l'inglese o la lingua scelta non hanno né il proprio file né un file toml nella propria directory;
- l'inglese non contiene messaggi;
- una chiave non è un numero;
- un testo è vuoto o non è un formato composito valido, come una `{` isolata;
- un testo richiede più di 16 valori;
- una traduzione richiede più valori del testo inglese, causando un errore quando il
  codice passa il numero di valori inglese;
- una traduzione contiene un numero assente in inglese;
- lo stesso numero si trova in due file di una lingua, o due volte in un file (`1` e `01`).

### Dividere una lingua in più file

Oltre a `data/messages/<language>.toml`, il server legge ogni file `*.toml` nella
directory `data/messages/<language>/` e li unisce in un unico insieme di messaggi:

```text
data/messages/
  eng.toml            # shipped: the standard texts, from UOX3
  eng/
    moongate.toml     # shipped: Moongate's own texts, numbers from 30000
    shard.toml        # your own texts
    quests.toml
  ita.toml
  ita/
    moongate.toml
    shard.toml
```

Il server fornisce due file per lingua: `<language>.toml` con i testi standard e
`<language>/moongate.toml` con i [messaggi propri di Moongate](#moongates-own-messages).

- Possono esistere sia il file sia la directory, oppure uno solo dei due.
- Ogni file ha lo stesso formato: una tabella `[messages]` di `number = "text"`.
- I file della directory vengono letti in ordine di nome. Sottodirectory e file che
  non terminano con `.toml` vengono ignorati.
- Scrivi il nome della directory in minuscolo: su Linux `ENG/shard.toml` non viene letto. Le
  maiuscole del nome file e dell'estensione `.toml` non contano.
- Un numero può comparire in un solo file. Lo stesso numero in due file di una lingua arresta
  il server e l'errore nomina entrambi i file.

Conserva i testi dello shard in file tuoi in `data/messages/eng/` affinché un aggiornamento
dei file forniti `eng.toml` e `eng/moongate.toml` non li sovrascriva.

## Leggere un messaggio dal codice

Risolvi `ILocalizationService` dal container, per esempio tramite un
costruttore:

```csharp
public class BoatHandler
{
    private readonly ILocalizationService _localization;

    public BoatHandler(ILocalizationService localization)
    {
        _localization = localization;
    }

    public string BoardMessage()
    {
        return _localization.Get(1); // "Si sale a bordo della barca."
    }
}
```

| Membro | Cosa fa |
| --- | --- |
| `Language` | Il codice configurato in minuscolo, come `ita`. |
| `Get(id, values...)` | Il messaggio con `{0}`, `{1}`, ... sostituiti da `values`, e `{{`, `}}` convertiti in parentesi graffe singole. Genera `KeyNotFoundException` per un ID sconosciuto e `FormatException` quando vengono forniti meno valori di quelli richiesti dal testo. |
| `TryGetText(id, out text)` | Il testo come scritto nel file, senza inserire valori; false per un ID sconosciuto. |

```csharp
_localization.Get(691, "Bob", "un drago"); // "Bob è stato ucciso da un drago! [Terremoto]\n"

if (_localization.TryGetText(691, out var text))
{
    // text is "{0} è stato ucciso da {1}! [Terremoto]\n"
}
```

I valori sono formattati con la cultura invariante, quindi i numeri non cambiano con
le impostazioni regionali dell'host.

Il servizio viene registrato dal plugin Ultima nelle modalità game e standalone. Legge
i messaggi la prima volta che viene richiesto un testo, dopo che `IDataLoaderService` ha eseguito
i loader all'avvio; richiederli prima fallisce perché i messaggi non sono ancora caricati.

## Leggere un messaggio da Lua

Gli script usano il modulo `localization`, che chiama `ILocalizationService`:

```lua
local text = localization.get(691, 'Bob', 'un drago')
-- "Bob è stato ucciso da un drago! [Terremoto]\n"

local raw = localization.text(691)    -- "{0} è stato ucciso da {1}! [Terremoto]\n"
local missing = localization.text(99999) -- nil

log.info('Server language: {Language}', localization.language()) -- "ita"
```

| Funzione | Cosa fa |
| --- | --- |
| `localization.get(id, ...)` | Il messaggio con `{0}`, `{1}`, ... sostituiti dagli argomenti aggiuntivi. Un numero Lua intero viene passato come intero, quindi `{0:x}` funziona; `nil` viene scritto come `nil`. Un ID sconosciuto o troppo pochi argomenti generano un errore Lua, che uno script può intercettare con `pcall`. |
| `localization.text(id)` | Il testo come scritto nel file, oppure `nil` per un ID sconosciuto. Usalo per verificare se un messaggio esiste. |
| `localization.language()` | Il codice lingua del server, come `ita`. |

```lua
local ok, err = pcall(localization.get, 99999)
-- ok is false, err contains "No message has id 99999."
```

Il plugin Ultima registra il modulo nelle modalità game e standalone, insieme agli
altri servizi dati. `definitions.lua` lo dichiara per il completamento nell'editor, con
`localization.text` che restituisce `string?`.

## Messaggi propri di Moongate

I numeri da 30000 appartengono a Moongate, non a UOX3, e sono tutti tradotti in ogni lingua fornita.
Risiedono in `data/messages/<language>/moongate.toml`, separati dai testi standard:

| ID | Testo | Usato da |
| --- | --- | --- |
| 30000–30004 | Comune, Non comune, Raro, Epico, Leggendario | Rarità nei tooltip; un oggetto la mostra solo sopra Comune (30000 resta per script e strumenti) |
| 30005 | [Maledetto] | Tipo di bottino nei tooltip |
| 30006, 30007 | Peso: 1 stone, Peso: {0} stone | Peso nei tooltip |
| 30008–30038, 30050–30052, gran parte di 30055–30112, 30115–30120, 30122 e 30126 | Bersaglio annullato., Comando sconosciuto: {0}, Uso: {0}, Il mondo è stato salvato in {0} secondi., {0} ora ha {1} fama., ... | Risposte dei comandi e messaggi globali (`CommandMessages`) |
| 30039–30049, 30053–30054, i restanti fino a 30113, 30121 e 30127 | Uno per comando integrato | Descrizioni dei comandi in `help` |
| 30114 | Questo moongate non sembra portare da nessuna parte. | Testi degli script degli oggetti, letti con `localization.get` |
| 30123, 30124 | Hai fame., Stai morendo di fame: le tue ferite non guariranno finché non mangi. | Ciò che un giocatore legge quando aumenta la fame |
| 30128, 30129 | Hai sete., Sei disidratato: la tua stamina non tornerà finché non bevi. | Ciò che un giocatore legge quando aumenta la sete |
| 30130–30132 | Sei semplicemente troppo pieno per bere ancora!, È vuoto., Bevi e hai meno sete. | I testi di `scripts/items/drink.lua` |
| 30133 | Sei sovraccarico: trasporti {0} stone su {1}. | Ciò che un giocatore legge quando posa qualcosa mentre trasporta più del consentito |
| 30134–30137 | Sei entrato in {0}., Hai lasciato {0}., Ora sei sotto la protezione delle guardie di {0}., Hai lasciato la protezione delle guardie di {0}. | Ciò che un giocatore legge quando entra o esce da un luogo nominato e quando cambia la protezione delle sue guardie |
| 30138 | Rimpiangerai le tue azioni, porco! | Ciò che una guardia dice quando arriva per un criminale |
| 30148, 30149 | Sei stato incarcerato per {0} giorni: {1}, Motivo: {0} | Ciò che un prigioniero legge quando è stato fornito un motivo, e il motivo sulla nota di rilascio |
| 30151 | i resti di {0} | Il nome del cadavere lasciato da un NPC |
| 30152 a 30154 | Uccide l'NPC che selezioni…, {0} è morto., {0} non può morire. | Il comando `kill` |
| 30164 a 30167 | Resuscita l'NPC di cui selezioni il cadavere…, {0} è tornato., Quello non è un cadavere., Quel cadavere non può essere resuscitato. | Il comando `resurrect` |
| 30155 a 30157 | Devi attendere {0} secondi prima di pubblicare di nuovo., Quel messaggio non è tuo., La bacheca è occupata: pubblica di nuovo tra un momento. | Ciò che un giocatore legge su una [bacheca](bulletin-boards.md) |
| 30168 | Non puoi usare abilità in prigione. | Ciò che un prigioniero legge quando usa un'[abilità](skills.md) |
| 30150 | Nessun personaggio si chiama {0}. | La risposta di `jail <name>` quando nessun giocatore ha quel nome |
| 30160 a 30163 | Mostra la versione eseguita dal server…, Moongate {0} "{1}" ({2}), compilato {3}., Mostra da quanto tempo il server è in esecuzione…, Attivo da {0}, dal {1}. | I comandi `version` e `uptime` |
| 30181 a 30184 | Nessun documento {0} in templates/books., Il documento {0} è nel tuo zaino., ... | Risposte e descrizione di aiuto del [comando `book`](commands/book.md) |
| 30192 a 30204 | You cannot ask to be moved while you are in jail., You cannot ask to be moved while you are fighting., You already asked to be moved: stand still., You can ask to be moved again in {0} minutes., Stand still for {0} seconds and you will be taken to {1}., You moved: you stay where you are., You have been taken to {0}., There is no city to take you to., le regole del server e le quattro etichette del gump (Help, I am stuck, Useful commands, Server rules) | Cosa legge un giocatore dal gump di [aiuto](help.md): perché «Sono bloccato» viene rifiutato, l'attesa, lo spostamento, il testo delle regole e le etichette del menu |
| 30205 a 30220 | Call a game master, What is it about?, Question, Bug, Suggestion, Harassment, il prompt di scrittura, le risposte a una richiesta, l'avviso allo staff, la riga della risposta, il conteggio delle richieste in attesa e la descrizione del comando `pages` | Cosa leggono un giocatore e un game master dalla coda di [aiuto](help.md) |
| 30224 a 30229 | The server will shut down in {0} minutes., la descrizione del comando `event` e le sue risposte | Cosa leggono un giocatore e un amministratore dal [calendario](schedule.md) |
| 30230 a 30237 | Oooooh, aren't you cute!, TRICK!, You receive some candy., gli annunci della stagione | Cosa dice un negoziante e cosa legge un giocatore in [dolcetto o scherzetto](holidays.md#halloween-trick-or-treat) |
| 30238 a 30240 | Merry Christmas!, The Christmas season is over., Happy Holidays! Gift items have been placed in your backpack. | Gli annunci e il regalo del [Natale](holidays.md#christmas-snowballs-and-gifts) |
| 30241 a 30244 | La descrizione del comando `lastonline` e le sue risposte | Cosa legge un game master da [`lastonline`](commands/lastonline.md) |
| 30185, 30186 | Apre il gump degli strumenti del game master…, Il gump gmtools manca: templates/gumps/gmtools.xml. | Il comando `gmtools` |

L'intestazione dei testi dei comandi in `eng/moongate.toml` elenca gli ID di entrambi gli insiemi.

I tooltip usano anche il 9055 "[Benedetto]" di UOX3, e `.account` il suo 555 "Un account con quel
nome esiste già!". Polacco e ceco scrivono il peso plurale
abbreviato ("kam."), perché un solo testo con `{0}` non può seguirne le forme plurali.

## Aggiungere o modificare un testo

1. Aggiungi il messaggio con un numero non ancora usato: un testo del codice Moongate in
   `data/messages/eng/moongate.toml`, un testo del tuo shard in un file tuo in
   `data/messages/eng/`.
2. Aggiungi la traduzione con lo stesso numero agli altri file. Una lingua che ne è priva
   mostra il testo inglese.
3. Usa gli stessi valori, nello stesso ordine, in ogni lingua.
4. Esegui `dotnet test --filter RepositoryDataFiles`: carica ogni lingua fornita
   con il loader reale.

Per aggiungere una lingua, copia `eng.toml` in `data/messages/<code>.toml` e
`eng/moongate.toml` in `data/messages/<code>/moongate.toml`, traduci i testi e imposta
`language = "<code>"`. Il codice può contenere solo lettere ASCII.
