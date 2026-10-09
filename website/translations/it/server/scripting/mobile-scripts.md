<!-- translation: {"sourceHash":"ce403ec99cd7abded3a2267701c2592e10193843771ce62bb2927b4311cec741","title":"Script dei mobile"} -->

# Script dei mobile

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). Le funzioni che uno script chiama sul proprio NPC sono nel
riferimento: [`npc`](https://moongate.sh/lua/npc/) e [`mobile`](https://moongate.sh/lua/mobile/).

Un template di mobile indica il proprio script con `script_id`, il nome di una tabella Lua globale
definita da `scripts/mobiles/<script_id>.lua`. Il server carica ogni `*.lua` direttamente
in quella directory all'avvio, in ordine di nome, dopo `init.lua`; uno script che fallisce
il caricamento viene segnalato come qualsiasi errore di script e il server parte con gli altri.

```toml
# templates/mobiles/animals.toml
[[mobile]]
id = "cat"
name = "a cat"
body = 201
script_id = "wander"
```

La tabella può definire queste funzioni; ciascuna è opzionale:

| Funzione | Quando |
| --- | --- |
| `on_think(serial)` | A ogni ciclo di pensiero dell'NPC: ogni `ultima.npcs.think_interval_ms` (500 ms per impostazione predefinita) mentre un giocatore si trova nei settori 5×5 attorno a lui; vedi [Tick degli NPC](../game-loop-and-timers.md#npc-tick). Un pensiero è istantaneo, come in ModernUO: non deve chiamare `wait` (il server avvisa una volta per script), quindi mantieni la temporizzazione nello script, per esempio contando i pensieri. |
| `on_speech(serial, speaker, text, keywords, type)` | Quando un giocatore pronuncia `text` a portata d'orecchio (i comandi non vengono ascoltati): 15 caselle a voce normale o come emote, 1 casella per un sussurro, 18 per un urlo. `speaker` è il seriale del giocatore; `keywords` sono le parole chiave del parlato individuate dal client, un array di numeri qualunque sia la sua lingua, come `SpeechKeywordType.Bank`; `type` è il modo in cui è stato detto, un numero da confrontare con `SpeechType.Regular`, `SpeechType.Emote`, `SpeechType.Whisper` o `SpeechType.Yell`. Può chiamare `wait`. |
| `on_spawn(serial)` | Una volta, subito dopo la generazione dell'NPC (`.spawn`), nel mondo con i suoi oggetti e già mostrato, prima di qualsiasi altra funzione del suo script. Non quando gli NPC salvati vengono caricati all'avvio. Può chiamare `wait`. |
| `on_mobile_in_range(serial, other)` | Ogni volta che un altro mobile, giocatore o NPC, entra entro `ultima.npcs.sense_range` caselle (8 per impostazione predefinita, un quadrato lungo X e Y) tramite un passo o entrando nel mondo. Una volta per arrivo: si attiva di nuovo solo dopo che il mobile ha lasciato il raggio ed è tornato. In entrambe le direzioni: anche un NPC che cammina verso un mobile lo percepisce. Gli NPC caricati insieme all'avvio non si percepiscono finché uno non esce dal raggio e torna. `other` è il suo seriale; `npc.name(other)` restituisce `nil` per un giocatore. Può chiamare `wait`. |
| `on_mobile_killed(serial, killed, killer)` | Quando un mobile, giocatore o NPC, viene ucciso entro `ultima.npcs.sense_range` celle da questo NPC (8 per impostazione predefinita, un quadrato lungo X e Y), escluso chi è morto. `serial` è l'NPC che viene avvisato, `killed` il seriale di chi è morto e `killer` il seriale di chi lo ha ucciso, oppure `nil` quando non lo ha fatto nessuno. `npc.name(killed)` restituisce `nil` per un giocatore. Un NPC che sta morendo non viene avvisato. |
| `on_death(serial, corpse, killer)` | Quando l'NPC muore ([Morte e resurrezione](../death.md)), dopo che il cadavere esiste ed è mostrato e prima che l'NPC lasci il mondo, quindi può ancora essere letto. `corpse` è il seriale del cadavere, nil quando non ne è stato creato uno; `killer` è il seriale di chi lo ha ucciso, nil quando non lo ha fatto nessuno. Se ucciso da uno script, viene eseguito al turno successivo del game loop, subito prima che l'NPC venga rimosso; non deve chiamare `wait()`. |
| `on_drag_drop(serial, giver, item)` | Quando un giocatore rilascia un oggetto sull'NPC da 2 caselle o meno, sulla stessa mappa; lo staff da qualsiasi distanza, come in ModernUO. Da più lontano un giocatore legge `That is too far away.` e l'oggetto torna indietro. `giver` è il seriale del giocatore, `item` quello dell'oggetto. Restituisci `true` quando lo script ha preso l'oggetto, dopo averlo spostato o eliminato (`item.delete`, `item.move_into`, `item.move_to`): l'oggetto non è su alcun cursore mentre la funzione viene eseguita. Qualsiasi altra cosa, una funzione assente, un errore o un `wait()` restituiscono l'oggetto al punto da cui il giocatore lo ha sollevato, come ogni rilascio su un NPC senza questa funzione. Il `can_drop` dell'oggetto viene interrogato prima, e il suo `on_drop` non viene chiamato per un oggetto preso da un NPC. |
| `on_context_menu(serial, player)` | Quando quel giocatore chiede il [menu contestuale](../context-menus.md) dell'NPC. Restituisci le voci che l'NPC aggiunge, una tabella di `{ id, cliloc, range, enabled }`, oppure nulla; risponde subito e non deve chiamare `wait`. |
| `on_context_menu_select(serial, player, id)` | Quando il giocatore ha scelto una di quelle voci: `id` è quello della voce. Chiamato solo per una voce che il giocatore ha visto e che è nella sua portata. Può chiamare `wait`. |

`on_spawn`, `on_mobile_in_range` e `on_mobile_killed` vengono eseguiti subito dopo ciò che li ha causati, al turno
successivo del game loop: un passo fatto da `npc.step` dentro un handler in esecuzione non può
avviare subito un altro script. Nessuna funzione viene eseguita prima che gli script siano caricati
all'avvio, dopo che gli NPC salvati sono entrati nel mondo.

Gli script agiscono sul proprio NPC con il modulo `npc`, passando il suo seriale. Un seriale che
non è un NPC nel mondo, come un NPC rimosso o un giocatore, restituisce `false` o
`nil`, mai un errore: un handler che ha atteso può sopravvivere al proprio NPC e uno script non può
mai far parlare o muovere un giocatore.

Un NPC che prende ciò che gli viene dato e conserva solo l'oro:

```lua
collector = {}

function collector.on_drag_drop(serial, giver, given)
    if item.template(given) ~= "0x0eed_gold_coin" then
        npc.say(serial, "I take gold only.")

        return false
    end

    npc.say(serial, "Thank you.")
    item.delete(given)

    return true
end
```

Il file `scripts/mobiles/wander.lua` della distribuzione, copiato nella directory radice da `mgctl`:

```lua
wander = {}

local thinks = {}

function wander.on_think(serial)
    thinks[serial] = (thinks[serial] or 0) + 1

    if thinks[serial] % 4 == 0 then
        npc.wander(serial)
    end
end

function wander.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        npc.look_at(serial, speaker)
        wait(1)
        npc.say(serial, "Well met, traveller.")
    end
end

function wander.on_spawn(serial)
    npc.say(serial, "*stretches*")
end

function wander.on_mobile_in_range(serial, other)
    if npc.name(other) == nil then
        npc.say(serial, "Who goes there?")
    end
end
```

Nessun template nel repository lo usa: aggiungi `script_id = "wander"` a un template di mobile
per provarlo. Un NPC generato da una regione di spawn porta la propria area di origine nelle proprietà `spawn.x1`,
`spawn.y1`, `spawn.x2` e `spawn.y2`, che `npc.home` restituisce come tabella: `npc.wander` compie passi solo
al suo interno e riporta l'NPC dentro quando è fuori. Passeggia come le creature di ModernUO, soprattutto
dritto davanti a sé, mentre lo script precedente sceglieva una nuova direzione a ogni passo.

Le tabelle `local` di uno script vivono in memoria: ripartono vuote dopo un riavvio o un
ricaricamento. Per ricordare qualcosa tra riavvii, conservalo nelle proprietà dell'NPC, anteponendo alla
chiave il nome dello script, come `vega.lua` conta i saluti che ascolta:

```lua
function vega.on_speech(serial, speaker, text)
    if text:lower():find("hello", 1, true) then
        npc.look_at(serial, speaker)

        local times = (npc.get_prop(serial, "vega.greeted") or 0) + 1
        npc.set_prop(serial, "vega.greeted", times)
        npc.say(serial, "Meow! That's " .. times .. " hellos.")
    end
end
```

Una modifica successiva all'ultimo salvataggio del mondo va persa se il server si arresta senza salvare.

Ricarica uno script con `script reload mobiles/wander.lua`. La sua tabella viene sostituita,
quindi gli NPC usano le nuove funzioni dal loro prossimo pensiero; lo stato conservato nelle tabelle
`local` del vecchio file riparte e le attese lasciate dai suoi handler vengono annullate,
perché le chiamate di uno script appartengono a `mobiles/<script_id>.lua`. Quando il server si arresta,
gli script non vengono più chiamati, prima che si arresti il motore degli script.

Lo script dei mostri, `monster.lua`, è descritto con gli altri
[script distribuiti](shipped-scripts.md#monsterlua).

## Percorrere un tragitto

`npc.walk_to` permette a un NPC di raggiungere un luogo aggirando muri, acqua e dirupi, e passando dalle porte chiuse che apre: un umano o un mostro apre una porta non chiusa a chiave, un animale o una creatura marina no, a meno che il suo template di mobile non lo dica con `opens_doors = true` o `false`. Lo script lo chiama
a ogni tick e l'NPC compie un passo ogni volta:

```lua
guard = {}

function guard.on_think(serial)
    local state = npc.walk_to(serial, 1434, 1699)

    if state == "arrived" then
        npc.say(serial, "All quiet at the bank.")
    end
end
```

| Risposta | Significato |
| --- | --- |
| `"moving"` | L'NPC ha compiuto un passo, oppure ha chiesto a una porta sulla sua strada di aprirsi: passa a una chiamata successiva, e dopo tre chiamate davanti a una porta che resta chiusa la risposta è `"blocked"` |
| `"arrived"` | Si trova entro `range` caselle dal luogo (predefinito 0), alla sua altezza; non viene controllato che non ci sia nulla tra loro |
| `"blocked"` | Il passo è stato rifiutato oppure l'NPC attende di cercare un'altra strada |
| `"no_path"` | L'ultima ricerca non ha raggiunto il luogo: nulla porta lì oppure porta solo nelle vicinanze |
| `nil` | Il seriale non è un NPC, `range` è negativo oppure `z` è fuori dall'intervallo da -128 a 127 |

Il percorso viene trovato con la [ricerca dei percorsi](../world-queries.md#pathfinding) del server e conservato per
l'NPC, quindi la maggior parte delle chiamate compie solo il passo successivo. Una ricerca viene eseguita quando l'NPC non ha più passi
oppure il luogo è cambiato, e solo:

- due secondi dopo l'ultima ricerca dell'NPC, come in ModernUO; dieci secondi quando quella ricerca non ha
  raggiunto lo stesso luogo da dove si trova l'NPC, perché quel tipo di ricerca è costoso;
- per dieci NPC al secondo nell'intero server; gli altri attendono il proprio turno.

Mentre non può cercare, un NPC prosegue lungo il percorso che ha oppure, se non ne ha, cammina dritto
verso il luogo, così uno che insegue qualcosa continua a muoversi. Si avvicina a un luogo irraggiungibile
fin dove porta un percorso. Per seguire qualcuno, passa la sua posizione a ogni
tick e un `range` di 1 per fermarsi accanto a lui:

```lua
local where = mobile.location(target)
npc.walk_to(serial, where.x, where.y, where.z, 1, true)
```

`running` cambia solo l'aspetto del passo: un NPC compie un passo per `on_think`, due al secondo.
Senza `z` il luogo è il terreno più alto della casella non sopra la testa dell'NPC, altrimenti il
più alto presente. Partenza e destinazione devono trovarsi entro `ultima.world.pathfinding_range` caselle (38);
più lontano restituisce `"no_path"`. Una porta chiusa blocca il passaggio e un NPC non la apre: la
aggira, oppure si avvicina e poi risponde `"no_path"`. Gli altri mobile non bloccano un percorso.
