<!-- translation: {"sourceHash":"8f52cf298950fd86e83819cf79dff1569ebcabee3d225bbc567562d3fe669cb4","title":"Morte e resurrezione"} -->

# Morte e resurrezione

Un NPC può morire: cade dove si trova, lascia il suo cadavere con ciò che portava e sparisce dal
mondo. Niente combatte ancora, quindi un NPC muore quando un game master lo uccide con
[`.kill`](commands/kill.md), uno script chiama `mobile.kill`, oppure una [guardia
cittadina](scripting/shipped-scripts.md#guardlua) lo raggiunge mentre è un criminale, o qualcosa lo combatte
fino a zero punti ferita. Anche un [giocatore muore](#death-of-a-player), e resta come fantasma.

## Che cosa succede

1. I giocatori intorno sentono il suo suono di morte.
2. Il cadavere viene creato dove si trovava l'NPC e ne prende le cose.
3. I giocatori intorno vedono il cadavere, poi l'NPC che muore (pacchetto `0xAF`: il client riproduce da solo la
   morte di quel corpo).
4. Lo script dell'NPC esegue `on_death`.
5. L'NPC lascia il mondo. Uno arrivato da una regione di spawn libera il suo posto, e la regione
   ne porta un altro alla sua prossima scadenza.

Un corpo umano, elfico o di gargoyle muore in due momenti, a 1,5 secondi di distanza: prima riproduce la sua caduta
(azione 21, come la mostra [`animate 21`](commands/animate.md)) e non può muoversi; quando la caduta è finita
arrivano il suo cadavere, `on_death` e la sua rimozione, senza `0xAF`. Vedi [Il cadavere
vestito](#the-dressed-corpse) per il motivo. Un NPC rimosso mentre cade non lascia alcun cadavere.

La console e il log lo dicono: `an orc (0x00000384) died at (1700, 1700, 5) of Felucca, killed by
Giachi`.

## Il cadavere

Il cadavere è un oggetto del template `corpse` (`templates/items/corpse.toml`, grafica `0x2006`): un
contenitore a terra che non può essere raccolto. Fai doppio clic per aprirlo e prendere ciò che c'è
dentro, come con un forziere.

| | |
| --- | --- |
| Nome | `the remains of an orc` (messaggio 30151, nella lingua del server) |
| Aspetto | Il corpo, l'orientamento e la tinta di chi è morto |
| Dentro | Ciò che si trovava nello zaino dell'NPC, come il suo oro e il bottino, e ciò che indossava |
| Escluso | Lo zaino stesso, capelli e barba, ciò che non può essere spostato e gli oggetti newbie o benedetti: se ne vanno con l'NPC |
| Durata | 7 minuti (`decay_minutes` del template), poi sparisce con ciò che nessuno ha preso |

Il bottino di un NPC viene estratto nel suo zaino quando viene generato, quindi il cadavere contiene ciò che l'NPC
aveva già.

Un cadavere è un solo oggetto: la sua quantità è 1. Il corpo che mostra è conservato nelle sue prop e inviato al client
al posto della quantità, come il client si aspetta dalla grafica del cadavere.

| Prop | Significato |
| --- | --- |
| `corpse.body` | Il corpo di chi è morto |
| `corpse.direction` | Il verso in cui guardava, un numero di direzione |
| `corpse.template` | Il template di mobile di chi è morto, quando ne aveva uno |
| `corpse.name` | Il nome di chi è morto |
| `corpse.spawn.region`, `corpse.spawn.x1` … | Ciò che la sua regione di spawn aveva dato a chi è morto: la regione e i quattro angoli della sua casa |
| `corpse.killer` | Il seriale di chi lo ha ucciso, quando qualcuno lo ha fatto |
| `corpse.worn` | Ciò che chi è morto indossava ed è finito nel cadavere, come coppie `serial:layer` separate da virgole |
| `corpse.hair`, `corpse.hair_hue` | La grafica dei capelli di chi è morto e la sua tinta, quando aveva capelli |
| `corpse.beard`, `corpse.beard_hue` | Lo stesso per la barba |

## Il cadavere vestito

Il cadavere di un corpo umano, elfico o di gargoyle viene disegnato con ciò che l'NPC indossava, i suoi capelli e la sua barba,
come fa ModernUO: subito dopo il cadavere al client vengono inviati gli oggetti indossati ancora all'interno
(`0x3C`) e i loro livelli (`0x89`). Togli un oggetto dal cadavere e non viene più disegnato su di esso,
per chi vede il cadavere da quel momento. Capelli e barba non sono oggetti: non si possono prendere. Il cadavere
di qualsiasi altro corpo viene disegnato come il client disegna quel corpo da morto.

Quando manca il template `corpse`, l'NPC muore comunque e non lascia nulla; il log lo dice.

Una borsa che si trovava nello zaino finisce nel cadavere con ciò che contiene. Il salvataggio del mondo riscrive quei
contenuti anche se non sono cambiati: il database li elimina con lo zaino dell'NPC
morto, sotto cui si trovavano.

La caduta è riprodotta dal server, non dal pacchetto di morte. ClassicUO, dal 2019, toglie i vestiti
a un mobile che muore con `0xAF`: dà al mobile morente un seriale proprio, considera i suoi oggetti
indossati come a terra lontano e li elimina subito, così il corpo cade nudo qualunque cosa un
server invii; gli altri emulatori lo mostrano tutti così. Un'azione che al mobile viene detto di eseguire è disegnata
vestita, quindi a un corpo umano viene detto di cadere, e il suo cadavere, vestito, ne prende il posto quando la caduta è
finita. UOX3 riproduce la stessa azione per un NPC ucciso da una guardia.

## Il suono di morte

Il suono è `death` di [`[mobile.sounds]`](templates.md) nel template dell'NPC. Un corpo umano, elfico o
di gargoyle che non ne ha uno muore con una delle quattro voci del suo genere (da `0x15A` a `0x15D` maschili,
da `0x150` a `0x153` femminili). Qualsiasi altro corpo senza un suono muore in silenzio.

## Da Lua

```lua
-- Kills the NPC; the second argument, who did it, is kept on the corpse and may be left out.
mobile.kill(orc, user)
```

`mobile.kill` è false per un giocatore, per un mobile che non è nel mondo e per un NPC che sta
già morendo.

Lo script dell'NPC può definire `on_death`, che viene eseguito dopo che il cadavere esiste e prima che l'NPC
venga rimosso, così l'NPC può ancora essere letto. Ucciso da `.kill`, viene eseguito subito. Ucciso da uno script
(`mobile.kill` dentro un `on_think`, un `on_use`, un gump): il cadavere, la morte e il suono arrivano
subito, e `on_death` e la rimozione al turno successivo del game loop, perché uno script non può
essere eseguito dentro un altro. Fino ad allora l'NPC è ancora nel mondo, senza le sue cose.

- `on_death` non deve chiamare `wait()`: l'NPC viene rimosso comunque, e ciò che viene dopo l'attesa non lo
  trova più.
- Un `on_death` che fallisce viene scritto nel log e l'NPC viene rimosso comunque.
- Ciò che l'NPC indossava esegue il proprio `on_unequip` dopo che l'NPC non c'è più: lo script di un oggetto deve reggere
  un portatore che non esiste più.

```lua
function orc.on_death(serial, corpse, killer)
    -- corpse is nil when no corpse was made, killer when nobody killed it.
    if corpse then
        -- One more roll of a table of templates/loots, straight into the corpse.
        item.add_loot(corpse, "bonearmor")
    end
end
```

## Rialzare chi è morto

Un cadavere può restituire il suo NPC: un game master lo seleziona con [`.resurrect`](commands/resurrect.md),
oppure uno script chiama `mobile.resurrect(corpse)`.

1. Un NPC del template di mobile del cadavere (`corpse.template`) nasce dove giace il cadavere.
2. Prende il nome e l'orientamento di chi è morto e, quando veniva da una regione di spawn, quella regione
   e la sua casa, così conta per la regione e vaga dove vagava il vecchio.
3. Un corpo umano, elfico o di gargoyle si rialza con la sua caduta riprodotta al contrario.
4. Il cadavere sparisce, con ciò che vi era rimasto dentro.

È un nuovo NPC dello stesso tipo, non il vecchio che torna: arriva con l'equipaggiamento e il bottino del
suo template, e ha un altro seriale. Il suo script esegue `on_spawn` prima che prenda il nome di chi è
morto. Uno rimosso o ucciso nel momento tra la nascita e il rialzarsi non viene mostrato mentre si rialza, e
il cadavere resta. Ciò che è stato preso dal cadavere resta preso. Il cadavere di un NPC
creato senza un template, o il cui template non esiste più, non può essere rialzato e resta dov'è.

```lua
-- True when the serial is a corpse: the NPC is born on a later turn of the game loop.
mobile.resurrect(corpse)
```

## Morte di un giocatore

Un giocatore muore quando qualcosa gli toglie l'ultimo punto ferita (un combattimento, `.kill`, `mobile.kill`). Non esiste un flag
"morto" salvato: un giocatore è morto finché indossa il **corpo da fantasma** della sua razza e del suo genere (umano da 400/401 a
402/403, elfo da 605/606 a 607/608, gargoyle da 666/667 a 694/695), ed è questo che legge `mobile.is_dead(serial)`.

1. I giocatori intorno sentono il suo suono di morte; un criminale viene perdonato.
2. Il suo cadavere giace dove si trovava, come quello di un NPC, con ciò che indossava e ciò che aveva nello zaino, tranne ciò che
   non può muoversi e gli oggetti newbie e benedetti. Lo zaino, i capelli e la barba restano al giocatore.
3. Tutti intorno lo vedono morire (`0xAF`); il client del giocatore riceve lo stato di morte (`0x2C`).
4. La modalità guerra è disattivata e punti ferita, stamina e mana sono a 0.
5. Il giocatore prende il corpo da fantasma e indossa un **sudario** (`death_shroud`, livello esterno del torso, non può
   essere tolto).

Un fantasma:

- è **nascosto ai vivi**, come un giocatore nascosto, a meno che non sia in modalità guerra; lo staff e gli altri fantasmi dello
  staff lo vedono. Un passo non lo mostra, solo la modalità guerra lo fa.
- viene sentito dai vivi come `oOo` per ogni parola che dice; lo staff e i morti lo sentono come ha parlato, e nessun NPC,
  oggetto o guardia gli risponde.
- non combatte e non viene combattuto; gli NPC non lo percepiscono.
- non usa skill, non solleva oggetti e non recupera punti ferita, mana o stamina. Un doppio clic su un oggetto esegue il suo
  `on_ghost_use(serial, user)` se lo script ne ha uno, e dice `I am dead and cannot do that.` in caso contrario.

### Tornare in vita

- Un **ankh** (i due pezzi di ogni `AnkhWest` e `AnkhNorth` che `.decorate` posiziona, template
  `decoration_ankh`, script `ankh.lua`): a un fantasma che vi fa doppio clic da 2 caselle o meno viene chiesto in un gump
  se vuole vivere; Continue lo rialza con il suono `0x214` e le scintille `0x376A`, e deve essere ancora
  lì, morto ed entro 2 caselle quando risponde.
- Un **guaritore** (template `healer`, `m_healer`, `f_healer`, e gli erranti `whealer`, `m_whealer`,
  `f_whealer`, script `healer.lua`): a un fantasma che arriva entro 4 caselle da uno, con il guaritore in vista, viene
  offerto lo stesso gump, con il suono del guaritore `0x1F2` e le scintille. Un guaritore offre al massimo ogni 2 secondi
  e solo a un fantasma che si avvicina: deve allontanarsi e tornare per ricevere di nuovo l'offerta. A un fantasma incontrato mentre il guaritore attende il suo turno l'offerta arriva a fine attesa, cosa che ModernUO non fa. Anche al fantasma di un game master viene offerto. Un criminale viene
  rifiutato ("Thou art a criminal. I shall not resurrect thee."), e a uno con karma negativo viene detto che si è smarrito
  e l'offerta arriva comunque. Non costa nulla, e il fantasma può rispondere fino a 8 caselle di distanza.
- Un game master con [`.resurrect`](commands/resurrect.md), uno script con `mobile.resurrect(serial)`.

Una resurrezione tramite ankh o guaritore costa un decimo della **fama** del giocatore, come in ModernUO; `.resurrect` e
`mobile.resurrect` no.

Il giocatore torna nel suo corpo da vivo con **10 punti ferita**, stamina piena e niente mana, il sudario sparisce e al suo posto
indossa una **veste della morte** (`death_robe`, tinta 2301, newbie così non finisce mai in un cadavere). Il suo cadavere non
viene restituito: resta a terra per conto suo finché non decade.

```lua
mobile.is_dead(serial)   -- true for a player that is a ghost
mobile.resurrect(serial) -- the serial of a ghost raises it at once
```

## Conteggio degli omicidi

Un giocatore che attacca un giocatore innocente che non risponde è un criminale, e l'attacco viene annotato. Quando quella
vittima muore, pochi secondi dopo (`report_delay_seconds`) le viene chiesto in un gump, per ciascuno di quelli che l'hanno attaccata da
criminali negli ultimi due minuti, se denunciarli come assassini; il gump chiuso o la risposta No non denuncia
nessuno. Nulla viene contato dalla morte in sé, come in ModernUO.

- **Yes** aggiunge un'**uccisione** (a lungo termine) e un **omicidio a breve termine** all'uccisore, e porta il suo karma a −1000 per
  ogni uccisione. Legge "You have been reported for murder!" e a **cinque uccisioni** "You are now known as a murderer!":
  il suo nome è rosso per tutti. La stessa vittima non può denunciare di nuovo lo stesso uccisore per 10 minuti.
- Entrambi i conteggi vengono dimenticati uno alla volta, gli omicidi a breve termine dopo 8 ore e le uccisioni dopo 40, contate
  dall'ultima denuncia, in tempo reale: contano anche le ore passate fuori dal mondo (ModernUO conta il tempo online), e i
  conteggi vengono aggiornati mentre il giocatore è nel mondo e quando vi ritorna. Un giocatore è rosso finché ha cinque uccisioni
  o più.
- Un giocatore rosso è ricercato dalle guardie, come un criminale, e un guaritore lo rifiuta. Vedi
  [`[ultima.murder]`](server-configuration.md) per i tempi.
- **Resuscitare costa a un assassino**: da cinque omicidi a breve termine, un ankh o un guaritore toglie dal 5 al 15% di ogni statistica
  e di ogni skill (resta il `100 − (4 + murders/5)` percento, mai sotto l'85% né sopra il 95%), ma lascia intatta una statistica a 10 o
  una skill a 35 punti quando scenderebbe sotto.
- **Depredare è un crimine**: prendere un oggetto dal cadavere di un giocatore innocente (uno che non era né criminale né
  assassino quando è morto) rende criminale chi lo depreda, a meno che non sia il proprietario del cadavere o dello staff. Il cadavere di un
  criminale o di un assassino è libero. ModernUO lo fa solo fuori da Trammel; qui non c'è ancora una regola per Trammel.

I conteggi vengono salvati con il giocatore. `mobile.murders(serial)` li legge come `{ kills, short_term }` e
`mobile.is_murderer(serial)` dice se è rosso.

Un **guaritore malvagio** (`evilhealer`, `evilwhealer`, l'id inizia con `evil`) non respinge nessuno: rialza anche i giocatori
rossi e i criminali, come quelli di ModernUO prima di AOS. I dati non ne posizionano ancora nessuno.

## Che cosa non c'è ancora

- Incantesimi che rialzano, e i luoghi dei guaritori malvagi nel mondo. Un fantasma non è visto dagli altri fantasmi. Ossa: un
  cadavere semplicemente decade. Un party o una gilda che possono depredare un cadavere, e la taglia che una vittima può mettere su un assassino, come in
  ModernUO: qui non esiste nulla di tutto ciò.
- Un giocatore che attacca un giocatore che risponde subito non è un criminale e non può essere denunciato, come in ModernUO; ma l'elenco dei
  "denunciabili" è tenuto in memoria: un riavvio lo dimentica.
- Se il punto è libero per un corpo (`Map.CanFit` di ModernUO) non viene controllato quando un ankh o un guaritore rialza un
  fantasma.
- Scuoiatura, fama e karma, il saccheggio come crimine, bottino diviso tra chi ha combattuto.
- Creature evocate che non lasciano cadavere, e ossa.
