<!-- translation: {"sourceHash":"c7c0efd260ab2e333f6a9c106184e30fdd4b06de9a03034286a4028139ea3a62","title":"Skill"} -->

# Skill

Un giocatore usa una skill dalla finestra delle skill o da una macro; uno script mette alla prova un mobile su una skill con
`skill.check`, e la prova può far crescere la skill. Questa è la prima parte: viene fornita una sola skill,
[Hiding](scripting/shipped-scripts.md#hidinglua), e il resto del gioco chiama lo stesso controllo man mano che
viene costruito.

## Usare una skill

Il client invia un comando testuale (pacchetto `0x12`, tipo `0x24`) il cui testo inizia con il numero della
skill, come `21 0` per Hiding.

1. Un prigioniero della [prigione](jail.md) legge "You may not use skills in jail." (messaggio 30168); lo
   staff non viene mai rifiutato.
2. Un personaggio ancora in attesa dopo la sua ultima skill legge "You must wait a few moments to use
   another skill." (cliloc 500118), al massimo una volta al secondo.
3. Viene eseguito lo script della skill: `on_use(user)` della tabella che porta il nome della skill in
   `scripts/skills/<skill>.lua`, con i nomi di `data/skills.toml` (`hiding`, `animal_lore`).
4. Una skill senza script risponde "That skill cannot be used directly." (500014), e non chiede
   attesa.
5. Il personaggio poi attende prima di un'altra skill: il numero restituito da `on_use`, in secondi da 0 a
   3600; quando non ne restituisce, il `delay` della skill in
   [`data/skills.toml`](data-files/skills.md); un secondo quando nemmeno il file ne indica uno. Un
   `on_use` che chiama `wait()` è ancora in esecuzione quando l'attesa viene impostata: chiede il `delay` della
   skill, almeno 10 secondi, e ciò che restituisce dopo non viene letto.

L'attesa è del personaggio ed è una sola per tutte le sue skill, come in ModernUO. Non viene salvata: non
sopravvive a un riavvio.

```lua
-- scripts/skills/hiding.lua
hiding = {}

function hiding.on_use(user)
    if skill.check(user, "hiding", 0, 100) then
        mobile.set_hidden(user, true)
    end
end
```

```toml
# data/skills.toml
[[skill]]
id = "hiding"
delay = 10.0
```

Gli script vengono caricati all'avvio, come gli [script degli oggetti](scripting/item-scripts.md); gli altri
tipi di comando testuale (lanciare un incantesimo, aprire una porta, un'azione) vengono letti ma per ora non hanno effetto.

## Il controllo

`skill.check(who, skill, min, max)` mette alla prova un mobile su un compito: `min` sono i punti a cui il
compito può appena essere iniziato, `max` quelli a cui non fallisce mai.

| Punti del mobile | Risultato | Può insegnare? |
| --- | --- | --- |
| Sotto `min` | Fallisce | No |
| A `max` o oltre | Riesce | No |
| Nel mezzo | Riesce con la probabilità `(points - min) / (max - min)` | Sì |

Quindi un compito troppo difficile o troppo facile non insegna nulla, come in ModernUO.

## La crescita

Una prova nel mezzo, superata o fallita, può far crescere la skill di un giocatore. Gli NPC non imparano mai.

- Sotto i 10.0 punti una skill impara sempre.
- Da 10.0 in poi, la probabilità è quella di ModernUO: la media dello spazio rimasto sotto il limite totale e sotto
  il limite della skill, poi la media tra quella e la difficoltà del compito (un successo conta metà di
  ciò che mancava alla certezza, un fallimento un quinto), moltiplicata per il `gain_factor` della skill in
  `data/skills.toml`; mai meno di una su cento.
- Una skill fino a 10.0 punti guadagna da 0.1 a 0.4 punti alla volta, sopra guadagna 0.1.
- Una skill al suo limite (100.0) non cresce, e nemmeno una il cui lucchetto non è su.
- Il totale delle skill di un giocatore si ferma a [`ultima.skills.total_cap`](server-configuration.md)
  (700.0 punti). Più il totale gli è vicino, più spesso una crescita abbassa prima della stessa quantità un'altra skill
  con il lucchetto giù; al limite, una skill cresce solo quando un'altra può essere abbassata.

## I lucchetti

Ogni skill ha un lucchetto, su per ogni skill di un nuovo personaggio: la freccia accanto a essa nella finestra
delle skill. Il giocatore lo sposta e il client lo comunica al server (pacchetto `0x3A`), che lo conserva con la
skill e lo salva.

| Lucchetto | La skill |
| --- | --- |
| Su | Può crescere quando viene messa alla prova. |
| Giù | Non cresce, e può essere abbassata per fare spazio a un'altra che cresce quando il totale è al limite. |
| Bloccato | Non cresce e non viene mai abbassata. |

Una skill o un lucchetto che non esiste viene ignorato.

`ultima.skills.gain_enabled = false` ferma ogni crescita: i controlli continuano a riuscire e a fallire.

## Le statistiche

Un controllo riuscito di un giocatore può anche far crescere forza, destrezza o intelligenza, secondo la regola
classica di ModernUO, che la skill stessa sia cresciuta o no (un controllo fallito non lo fa mai). I campi
`str_gain`, `dex_gain` e `int_gain` della skill in `data/skills.toml` dicono quanto la skill
favorisce ciascuna statistica; una statistica il cui numero è 0 non viene mai provata da quella skill.

1. Per ogni statistica che la skill favorisce e il cui lucchetto è su, un tiro con la probabilità `gain / 33.3` (un
   guadagno di 0.8 è il 2,4%).
2. Una statistica che passa viene provata una volta ogni `stat_gain_minutes` (10): l'attesa inizia quando viene provata,
   anche se non cresce nulla. È tenuta in memoria, non salvata: dopo un riavvio una statistica può essere provata subito.
3. Più la somma delle tre statistiche si avvicina a `stat_cap` (225), più spesso il giocatore *cede*:
   viene tolto un punto a una statistica con il lucchetto giù (sopra i 10 punti), la più bassa delle due quando entrambe
   possono. Una volta che il totale è al limite cede sempre.
4. Se il totale è poi sotto `stat_cap`, e la statistica ha il lucchetto su ed è sotto `stat_max` (100), cresce di uno.

Il massimo della sua barra si muove con essa: la forza per i punti ferita, la destrezza per la stamina e
l'intelligenza per il mana, e il giocatore rivede l'intero stato. Gli NPC non guadagnano mai statistiche.

Forza, destrezza e intelligenza hanno un lucchetto ciascuna, come le skill, salvato con il personaggio:
le frecce accanto a esse nella finestra di stato (`0xBF` sottocomando `0x1A` dal client, e `0x19` verso
il client al login e ogni volta che un lucchetto cambia).

| Lucchetto | La statistica |
| --- | --- |
| Su | Può crescere quando una skill viene messa alla prova. |
| Giù | Non cresce; cede un punto quando un'altra cresce e il totale è al limite. |
| Bloccato | Non cresce e non viene mai abbassata. |

L'attesa di una statistica è tenuta in memoria per personaggio, quindi uscire e rientrare non la salta; un riavvio
sì. I personaggi esistenti hanno tutti i lucchetti su dopo l'aggiornamento; la migrazione `0024_mobile_stat_locks.sql`
aggiunge le tre colonne.

## Istruttori

Un venditore o un guaritore insegna le abilità che ha a 60,0 o più, come gli istruttori di ModernUO. Le abilità sono quelle del suo
template mobile, quindi un venditore insegna quelle uscite così alte. I banchieri non insegnano: l'oro trascinato su di loro viene
depositato.

- **Chiedere.** Il menu contestuale del PNG ha una voce *Train* per ogni abilità che insegna e di cui il giocatore sa meno, da
  8 caselle. Dire *train* entro 4 caselle, da vivo, fa elencare al PNG le abilità che insegna, o dire che non ha nulla da insegnare.
- **Prezzo.** Il PNG insegna fino a un terzo del suo valore, al massimo 42,0 e mai oltre il tetto dell'abilità del giocatore.
  Scegliere una voce gli fa dire il prezzo: 1 oro per ogni decimo di punto, quindi 10 oro per un punto intero (420 oro per
  42,0), e che per meno insegna meno. Il preventivo dura finché non viene pagato, viene dato un altro prezzo o la sessione finisce.
- **Pagare.** Il giocatore trascina oro sul PNG, da 2 caselle. L'abilità sale subito di un decimo di punto per ogni moneta,
  fino a quanto preventivato; si prende solo l'oro necessario e il resto della pila resta al giocatore. Un trascinamento
  senza preventivo di quel PNG, o di qualunque cosa che non sia oro, torna indietro.
- **Rifiuti.** Il giocatore sa già quanto il PNG insegnerebbe (*thou knowest all I can teach*) o di più; l'abilità non è
  bloccata verso l'alto, oppure il tetto totale (`ultima.skills.total_cap`) non lascia posto nemmeno abbassando le abilità
  bloccate verso il basso, che cedono nell'ordine delle abilità; il giocatore è morto. Un oro trascinato che non può più
  insegnare riceve la stessa risposta, e il preventivo viene scartato.

Uno script dà a un PNG queste lezioni con il modulo `trainer` e `common/training.lua`; `shopkeeper.lua` e
`healer.lua` lo fanno già. `trainer.skills(npc, player)` elenca le abilità, `trainer.quote(npc, player, skill)` dà un
prezzo e `trainer.pay(npc, giver, item)` prende l'oro, che è ciò che risponde un `on_drag_drop`.

## Vedi anche

- [`hiding.lua`](scripting/shipped-scripts.md#hidinglua)
- [`bandage.lua`](scripting/shipped-scripts.md#bandagelua): Healing, con le bende
- [Le abilità di osservazione](scripting/shipped-scripts.md#the-lore-skills): Anatomy, Evaluating Intelligence, Forensic Evaluation e Detecting Hidden
- [Configurazione del server](server-configuration.md): `[ultima.skills]`
- [Roadmap](roadmap.md): 1.2
