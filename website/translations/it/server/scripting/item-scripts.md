<!-- translation: {"sourceHash":"341b00948cec140001eb16b1f5d36378fedf9cf26a1d57fa27627fc4e7959502","title":"Script degli oggetti"} -->

# Script degli oggetti

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). Le funzioni che uno script chiama sul proprio oggetto sono nel
riferimento: [`item`](https://moongate.sh/lua/item/).

Un template di oggetto indica il proprio script con `script_id`, il nome di una tabella Lua globale
definita da `scripts/items/<script_id>.lua`. I file di `scripts/items/` vengono caricati
all'avvio come gli [script dei mobile](mobile-scripts.md), e `script reload
items/potion.lua` ne ricarica uno.

```toml
# a potion template of your own
[[item]]
id = "my_potion"
item_id = 0x0F0C
script_id = "potion"
```

| Funzione | Quando |
| --- | --- |
| `on_use(serial, user)` | Un giocatore fa doppio clic sull'oggetto, trasportato (indossato o nei propri contenitori) oppure a terra entro 2 caselle e visibile; più lontano, il giocatore legge "È troppo lontano." e non viene eseguito nulla. Gli oggetti dentro un contenitore a terra non possono ancora essere usati: il giocatore legge "È troppo lontano.". Un `on_use` assente, oppure che genera un errore, lascia proseguire l'azione predefinita. Restituisci `true` per interrompere l'azione predefinita, come l'apertura di un contenitore; non restituire nulla per lasciarla proseguire. Un handler che chiama `wait` conta come gestito; dopo l'attesa l'oggetto potrebbe essersi spostato, quindi controllalo di nuovo, per esempio `item.owner(serial) == user`. |
| `on_move_over(serial, mobile)` | Un giocatore è entrato nella casella dell'oggetto, a terra all'altezza del giocatore, fino a 14 unità sopra i piedi, oppure sotto di essi e abbastanza alto da raggiungerli (la regola di ModernUO). Viene eseguito dopo che il passo è stato confermato e mostrato ai giocatori vicini; un NPC esegue invece `on_npc_move_over`. Una volta che uno script ha spostato il giocatore fuori dalla casella, gli altri oggetti della casella non vengono eseguiti. Arrivare tramite teletrasporto non lo attiva, quindi due teletrasporti che puntano l'uno all'altro non creano un ciclo. |
| `on_npc_move_over(serial, npc)` | Un NPC è entrato nella casella dell'oggetto, con la stessa regola sull'altezza. Viene eseguito al turno del game loop successivo al passo, quindi l'NPC potrebbe essersi già spostato: controlla dove si trova |
| `on_speech(serial, speaker, text, keywords)` | Un giocatore ha pronunciato `text` entro 15 caselle dall'oggetto a terra (i comandi non vengono ascoltati). `speaker` è il seriale del giocatore; `keywords` sono le parole chiave del parlato individuate dal client, un array di numeri. Ogni oggetto a terra con script nel raggio viene interrogato, dopo gli NPC, quindi uno script controlla il proprio raggio e le parole. Può chiamare `wait`. |
| `on_equip(serial, wearer)` | L'oggetto è passato su un layer del mobile `wearer`, rilasciato sul paperdoll. Un oggetto indossato sollevato e respinto non ha mai lasciato il proprio layer, e gli oggetti caricati o generati già indossati non attivano nulla. Non può rifiutare l'oggetto: lo fa `can_equip`. |
| `on_unequip(serial, wearer)` | L'oggetto ha lasciato il layer di `wearer`: rilasciato in un contenitore o a terra, oppure unito a una pila (l'oggetto allora non esiste più, quindi `item.*` restituisce `nil`). Disconnettersi, rimuovere un NPC o eliminare un mobile con i suoi oggetti non attivano nulla. |
| `on_pickup(serial, picker)` | Il giocatore `picker` solleva l'oggetto da un contenitore, dal paperdoll o da terra; sollevare parte di una pila solleva questo oggetto, e il resto lasciato indietro non è nuovo. Mentre è tenuto, `item.consume` e `item.delete` lo rifiutano. Un oggetto tenuto termina in `on_drop`, in `on_equip` quando viene indossato da un nuovo portatore, oppure in nulla: quando viene respinto, indossato di nuovo sul layer da cui proviene o quando il suo giocatore si disconnette tenendolo. |
| `on_drop(serial, dropper)` | Il giocatore `dropper` posa l'oggetto tenuto: in un contenitore, a terra o su una pila (l'oggetto allora non esiste più, quindi `item.*` restituisce `nil`). Non quando viene respinto o indossato. Un oggetto indossato che viene posato esegue prima `on_unequip`, poi `on_drop`. |
| `can_pick_up(serial, picker)` | Il giocatore `picker` sta per sollevare l'oggetto da un contenitore, dal paperdoll o da terra, quando tutte le regole del server lo consentono e prima che una pila venga divisa. Restituisci `false` per rifiutare: l'oggetto rimane dov'è e il client non mostra alcun messaggio proprio. Un oggetto indossato viene sollevato prima di essere tolto, quindi questo è anche il punto in cui uno script mantiene un oggetto sul suo portatore |
| `can_drop(serial, dropper)` | Il giocatore `dropper` sta per posare l'oggetto tenuto, ovunque: a terra, in un contenitore o su una pila. Restituisci `false` per rifiutare: l'oggetto torna da dove è stato sollevato |
| `can_equip(serial, wearer)` | L'oggetto tenuto sta per essere indossato da `wearer`, quando il layer è libero e le regole lo consentono. Restituisci `false` per rifiutare: l'oggetto torna da dove è stato sollevato e `on_equip` non viene eseguito. Non viene interrogato per un oggetto indossato sollevato e rimesso sul suo layer, che non ha mai lasciato |
| `can_insert(serial, mobile, item)` | Interrogato su un contenitore: il giocatore `mobile` sta per inserirvi `item`, oppure per unirlo a una pila direttamente al suo interno, quando le regole consentono il rilascio. `serial` è il contenitore, trasportato o a terra; un contenitore che contiene quel contenitore non viene interrogato. Restituisci `false` per rifiutare: l'oggetto torna da dove è stato sollevato. Viene interrogato dopo il `can_drop` dell'oggetto e una volta per ogni rilascio |
| `on_context_menu(serial, player)` | Quel giocatore chiede il [menu contestuale](../context-menus.md) dell'oggetto: uno che porta, oppure a terra o in un contenitore lì, in vista. Restituisci le voci che l'oggetto aggiunge, una tabella di `{ id, cliloc, range, enabled }`, oppure nulla; risponde subito e non deve chiamare `wait`. |
| `on_context_menu_select(serial, player, id)` | Il giocatore ha scelto una di quelle voci: `id` è quello della voce. Chiamato solo per una voce che il giocatore ha visto e che è nella sua portata. |
| `on_timer(serial, name)` | È scaduto un timer dell'oggetto, avviato con `item.start_timer`. I timer vengono verificati una volta al secondo. Il timer non esiste più quando la funzione viene eseguita: avvialo di nuovo lì per qualcosa che si ripete. Un timer scaduto mentre il server era fermo, o mentre il personaggio che trasportava l'oggetto era offline, viene eseguito appena l'oggetto torna nel mondo. Può chiamare `wait` |
| `on_darkness(serial, dark)` | Ogni 30 secondi e subito dopo `.globallight`, su un lampione (template `decoration_light`, proprietà `decoration_type` da LampPost1 a LampPost3) il cui punto è diventato buio (`true`) o luminoso (`false`) |
| `on_create(serial)` | Un oggetto appena creato entra nel mondo: l'equipaggiamento, lo zaino e il bottino di un NPC generato, prima dell'`on_spawn` di quell'NPC, e una cassa creata da una regione di spawn, con tutto ciò che contiene. Gli oggetti iniziali di un nuovo personaggio, il resto di una pila divisa e gli oggetti creati da `item.give`, `item.create`, `item.add_loot` o `.decorate` non attivano nulla. |

`on_equip`, `on_unequip`, `on_pickup`, `on_drop` e `on_create` vengono eseguiti subito dopo ciò che
li ha causati, al turno successivo del game loop, quando i giocatori lo hanno visto: uno script
può allora eliminare o consumare l'oggetto. Sono notifiche: nessuna può rifiutare il movimento.

`can_pick_up`, `can_drop`, `can_equip` e `can_insert` sono domande, poste prima del
movimento e risposte subito: solo `false` rifiuta. Una funzione assente, un errore, una chiamata a
`wait` o qualsiasi altro valore lasciano proseguire il movimento, quindi uno script guasto non blocca mai un oggetto. Spiega
al giocatore il motivo con `mobile.message` prima di restituire `false`. Vengono interrogate per i movimenti
che un giocatore compie con il client, staff incluso; uno script che sposta autonomamente un oggetto
(`item.move_to`, `item.move_into`) non viene interrogato. Mentre viene posta una domanda l'oggetto conta come tenuto, quindi
`item.delete`, `item.consume`, `item.move_into` e le funzioni che lo modificano lo rifiutano:
rispondi lì alla domanda e agisci sull'oggetto in `on_pickup`, `on_drop` o `on_equip`.

```lua
-- a cursed ring: once worn, it stays on
function ring.can_pick_up(serial, picker)
    -- worn: it has an owner and lies in no container
    if item.owner(serial) == picker and item.container(serial) == nil then
        mobile.message(picker, "The ring will not come off.")
        return false
    end
end
```

Lo script agisce sul proprio oggetto con il modulo `item`, passando il suo seriale; `user` è
il seriale del giocatore. Il file `scripts/items/potion.lua` della distribuzione viene copiato nella directory radice da `mgctl`; nessun
template lo usa ancora:

```lua
potion = {}

function potion.on_use(serial, user)
    item.message(serial, user, "You drink the potion.")
    item.consume(serial)

    return true
end
```

Gli script distribuiti per porte, luci, cibo, teletrasporti, moongate, orologi e contenitori
sono descritti in [Script distribuiti](shipped-scripts.md).
