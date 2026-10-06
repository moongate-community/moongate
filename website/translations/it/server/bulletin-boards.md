<!-- translation: {"sourceHash":"dcbb9b96218a4a828c301c054c6a89acffac0c71922ae805c76fe01bd40a8074","title":"Bacheche"} -->

# Bacheche

Un giocatore fa doppio clic su una bacheca, legge ciò che gli altri vi hanno scritto, pubblica un messaggio o una risposta
e rimuove i propri. Ogni bacheca ha i propri messaggi: ciò che è scritto sulla bacheca della banca di Britain
non è su quella di Minoc, né sull'altra bacheca di Britain.

Le bacheche sono quelle posizionate nelle città da [`.decorate`](commands/decorate.md) (le voci di
tipo `BulletinBoard` dei [file di decorazione](templates.md)) e qualsiasi bacheca aggiunta da un game master
con `.add bulletin_board`.

Caratteristiche proprie di Moongate: durata delle discussioni e numero di messaggi contenuti in una bacheca sono impostazioni; una bacheca piena
non rifiuta mai un messaggio; una discussione viene rimossa con le sue risposte. ModernUO elimina una discussione sei
ore dopo l'ultima risposta e lascia le risposte di un messaggio rimosso; Sphere mantiene 32
messaggi senza discussioni; UOX3 consente la rimozione solo a un game master.

## Leggere

Fai doppio clic su una bacheca da due caselle o meno. Il client mostra la bacheca e l'elenco dei
messaggi: chi ha scritto ciascuno, l'argomento e il giorno. Le risposte sono elencate sotto il primo messaggio
della discussione, nell'ordine di pubblicazione.

Apri un messaggio e vedi il suo testo e l'autore com'era alla pubblicazione: corpo,
colore e ciò che indossava.

## Pubblicare

`Post` sulla bacheca apre un messaggio vuoto: digita oggetto e testo. `Reply`, su un messaggio,
pubblica nella discussione di quel messaggio; una risposta a una risposta va sotto lo stesso primo messaggio.

- Un messaggio richiede un oggetto e almeno una riga di testo; altrimenti non viene pubblicato nulla.
- L'oggetto viene tagliato a 60 caratteri, una riga a 80, un messaggio a 32 righe. I caratteri di controllo vengono
  rimossi e le righe vuote finali eliminate.
- Attendi tra due pubblicazioni sulla stessa bacheca: [`thread_seconds`](#settings) tra due nuove discussioni,
  [`reply_seconds`](#settings) dopo qualsiasi messaggio prima di una risposta. Troppo presto, leggi `You must wait
  90 seconds before posting again.` e non viene pubblicato nulla. I game master e superiori non attendono.
  Rimuovere il proprio messaggio non riduce l'attesa.
- Una risposta a un messaggio rimosso nel frattempo diventa una nuova discussione.

## Rimuovere

`Remove`, su un messaggio, lo toglie dalla bacheca. Rimuovi i tuoi messaggi; un game master
rimuove qualsiasi messaggio. Chiunque altro legge `That message is not yours.`

Rimuovere il primo messaggio di una discussione rimuove anche le sue risposte.

## Quanto dura un messaggio

- Una discussione scompare dopo [`expire_days`](#settings) dall'ultima risposta: una discussione a cui si continua a rispondere
  resta. Con `expire_days = 0` non scade nulla.
- Una bacheca contiene [`max_messages`](#settings). Un messaggio che supera il limite fa eliminare alla bacheca
  la discussione rimasta più a lungo senza risposte, con le sue risposte. Quella a cui hai appena risposto
  non viene mai eliminata: su una bacheca con quella sola discussione vengono eliminate le risposte più vecchie.
- Il server cerca discussioni scadute quando viene aperta una bacheca e una volta ogni ora. Il controllo orario
  elimina anche i messaggi di una bacheca cancellata.

I messaggi vengono mantenuti nella tabella `world.bulletin_messages` e scritti dal salvataggio del mondo, quindi un
riavvio non ne dimentica nessuno.

## Chi può

Ogni richiesta del client viene ricontrollata dal server: l'oggetto è una bacheca, il tuo
personaggio è sulla sua mappa ed entro due caselle, e il messaggio richiesto appartiene a quella
bacheca. Una richiesta che fallisce viene scartata senza dire nulla, come fanno gli altri emulatori: allontanati da una
bacheca con la finestra aperta e i pulsanti non fanno nulla. Game master e superiori leggono e rimuovono
da qualsiasi posizione.

## Impostazioni

```toml
[ultima.bulletin_boards]
expire_days = 7        # A thread goes this many days after its last reply; 0 keeps it.
max_messages = 50      # The messages a board holds; the oldest thread goes when it is full.
thread_seconds = 120   # The wait between two new threads of one character on a board.
reply_seconds = 30     # The wait between two posts of one character on a board.
```

`expire_days` va da 0 a 3650, `max_messages` da 1 a 200, le due attese da 0 a 86400.

## L'oggetto bacheca

| File | Cosa |
| --- | --- |
| `templates/items/decorations.toml` | L'oggetto `bulletin_board`: grafica 0x1E5E, non spostabile, nessun decadimento, `script_id = "bulletin_board"` |
| `templates/items/building/decs/misc.toml` | `0x1e5e_bulletin_board` e `0x1e5f_bulletin_board`, i due orientamenti, con lo stesso script |
| `scripts/items/bulletin_board.lua` | Il suo [`on_use`](scripting/shipped-scripts.md#bulletin_boardlua) apre la bacheca |

`.decorate` assegna a una voce `BulletinBoard` il template `bulletin_board` con la grafica della
voce, e converte una bacheca posizionata in precedenza come semplice decorazione. Una `BountyBoard` resta un
oggetto da guardare: le taglie non esistono ancora.

Una bacheca mostra il nome dell'oggetto, oppure `bulletin board` se non ne ha uno.

## Per gli script

Il [modulo `board`](https://moongate.sh/lua/board/) apre una bacheca su un giocatore e consente a uno script
di scriverci, leggerla e rimuoverne messaggi:

```lua
function bulletin_board.on_use(serial, user)
    board.open(serial, user)

    return true
end
```

Qualsiasi oggetto il cui template ha `script_id = "bulletin_board"` è una bacheca. `board.open` non
controlla la distanza: `on_use` richiede già due caselle e visibilità.

```lua
local notice = board.post(notice_board, "The town crier", "Hear ye", {
    "The bank of Britain is closed today.",
    "",
    "Come back tomorrow."
})

for _, message in ipairs(board.messages(notice_board)) do
    log.info(message.name .. ": " .. message.subject)
end

board.remove(notice)
```

- `board.post(board, name, subject, lines [, thread])` pubblica con il nome dato e restituisce il
  seriale del messaggio, oppure `nil` quando non è stato pubblicato nulla: nessun nome, oggetto o riga di testo,
  un oggetto che non è una bacheca, oppure nessun seriale pronto per il messaggio (la stessa chiamata funziona un momento
  dopo). Le righe terminano al primo `nil` tra esse. Con `thread`, il seriale di un messaggio di quella bacheca, è una
  risposta. Una pubblicazione da script non attende e non ha autore: il messaggio mostra un corpo nudo accanto al
  testo, e nella finestra della bacheca solo lo staff lo rimuove. Il nome viene tagliato a 30
  caratteri; oggetto, righe e dimensione della bacheca seguono le regole di un messaggio
  del giocatore.
- `board.messages(board)` restituisce i messaggi come array di `{ serial, thread, poster, name,
  subject, lines, posted_at }`, le discussioni dalla più vecchia, ciascuna seguita dalle proprie risposte.
  `thread` è `nil` sul primo messaggio, `poster` è `nil` per un messaggio pubblicato da uno script, e
  `posted_at` è in secondi, come `world.now()`.
- `board.remove(message)` rimuove un messaggio e le sue risposte quando avvia una discussione; `false`
  quando non c'è. Non chiede a nessuno: uno script che rimuove per un giocatore controlla prima `poster`.

Il modulo non controlla chi lo chiama: uno script per lo staff controlla prima `world.is_staff`. Un
 giocatore che ha la bacheca aperta vede ciò che uno script ha pubblicato o rimosso quando la riapre.

## I pacchetti

Tutto viaggia nel pacchetto 0x71, con un sottocomando:

| Sottocomando | Da | Cosa |
| --- | --- | --- |
| 0x00 | server | La bacheca e il suo nome. I messaggi seguono come contenuto di un contenitore (0x3C), ciascuno un oggetto 0x0EB0 dentro la bacheca |
| 0x01 | server | Il riepilogo di un messaggio: autore, oggetto, data e discussione a cui risponde |
| 0x02 | server | Un messaggio completo: autore, oggetto, data, aspetto dell'autore, righe |
| 0x03 | client | Richiede il testo di un messaggio |
| 0x04 | client | Richiede il riepilogo di un messaggio |
| 0x05 | client | Un messaggio: a cosa risponde, oggetto, righe |
| 0x06 | client | Rimozione di un messaggio |

Un nuovo messaggio entra nell'elenco dell'autore come oggetto aggiunto alla bacheca (0x25) e un messaggio
rimosso lo lascia con 0x1D. La data è solo il giorno, in inglese: `Oct 05, 2026`. La
[guida di riferimento dei pacchetti](https://moongate.sh/packets/) contiene ogni campo.

## Cosa non fa ancora

- Bacheche delle taglie, bacheche delle case e messaggi di scorta UOX3.
- Moderazione oltre la rimozione: nessuna discussione bloccata, messaggio fissato o persona bannata da una bacheca.
- Un registro dei messaggi rimossi.

## Vedi anche

- [Configurazione del server](server-configuration.md)
- [Script forniti](scripting/shipped-scripts.md#bulletin_boardlua)
- [Template e decorazioni](templates.md)
