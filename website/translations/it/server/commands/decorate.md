<!-- translation: {"sourceHash":"58781f2e1e4304acb6581073a636007191970d169db052f4ac2193804fef6ed1","title":"decorate"} -->

# decorate

Posiziona la decorazione del mondo: porte, insegne, luci e mobili.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `decorate` | Sì | Sì | Administrator | Game |

```text
decorate
```

In gioco gli amministratori usano `.decorate`. Prima chiede conferma con il
[gump](../gumps.md) `templates/gumps/decorate_confirm.xml`: CONTINUE prosegue, CANCEL o la chiusura
stampa `Decoration canceled.` e non posiziona nulla (senza quel file non chiede). Mentre è in corso un'altra
decorazione, il comando risponde `A decoration is already running.` e non posiziona nulla. Posiziona i
[file di decorazione](../templates.md#decorations) di `templates/decorations/`, uno alla volta, come
oggetti fissi che non decadono mai; il salvataggio successivo del mondo li conserva. Porte e cancelli ricevono il
template `decoration_door`, il cui [script delle porte](../scripting/shipped-scripts.md#doorlua) li apre e chiude, e
due porte dello stesso tipo affiancate, alla stessa altezza e con cerniere sui lati opposti, si aprono
insieme. Le luci ricevono il template `decoration_light`,
accese o spente come nei dati e protette, quindi solo lo staff le accende o spegne con lo
[script delle luci](../scripting/shipped-scripts.md#lightlua). I teletrasporti ricevono il template `decoration_teleporter`: un giocatore
che cammina su uno si trova subito alla sua destinazione ([script dei teletrasporti](../scripting/shipped-scripts.md#teleporterlua)), e
solo lo staff li vede. Oltre a quelli dei file di città e dungeon, ogni cartella di mappa contiene i
teletrasporti di `[TelGen` di ModernUO (`teleporters.toml`, 1.478 in tutto); una casella conserva un teletrasporto
entro 12 di altezza, il primo posizionato, quindi i 230 già elencati da un file di città o dungeon vengono
segnalati come già presenti. Sette di Ter Mur si trovano fuori dalla sua mappa nei dati ModernUO e vengono saltati. Un teletrasporto verso un'altra mappa vi porta il giocatore quando quella mappa è caricata, altrimenti non fa nulla. Uno il cui `map_dest` non indica una mappa viene saltato. Un
`KeywordTeleporter`, come il mantra di un santuario, riceve il template `decoration_keyword_teleporter`:
un giocatore che pronuncia la sua parola entro la portata si trova alla destinazione
([script dei teletrasporti a parola chiave](../scripting/shipped-scripts.md#keyword_teleportlua)). I moongate pubblici non sono nei file di
decorazione: dopo questi, `.decorate` posiziona un portale con il template `decoration_public_moongate` su
ogni destinazione di [`moongates.toml`](../data-files/moongates.md) la cui mappa è caricata, segnalato
come `<map>/moongates`. Le casse, scatole, forzieri, barili e librerie delle città (tipi
`Fillable...` e `LibraryBookcase` di ModernUO, 2.803 punti nei file, circa 5.000 oggetti con quelli sia di Trammel sia di Felucca) assumono il template `decoration_fillable`
e si riempiono quando un giocatore li apre ([script dei contenitori riempibili](../scripting/shipped-scripts.md#fillablelua));
quelli posizionati come semplice decorazione da un'esecuzione precedente vengono convertiti e conteggiati come già presenti.
Gli orologi indicano l'ora con un doppio clic ([script degli orologi](../scripting/shipped-scripts.md#clocklua)), convertiti
allo stesso modo. Gli spawner dei file di decorazione (87, tutti di personaggi delle missioni come Haochi o
Uzeraan) restano saltati: nessuno dei loro template mobile esiste ancora. Ogni moongate pubblico emette luce (prop `light = "circle300"`, come ModernUO). Spawner, contenitori
mark, addon e ogni altro tipo di teletrasporto (quelli che richiedono un'
abilità, appartengono a una missione o vogliono un doppio clic) vengono per ora saltati: richiedono una logica propria.

Anche le insegne dei negozi e del mondo sono file di decorazione (`signs.toml` nelle cartelle `britannia`, `felucca`,
`trammel`, `ilshenar`, `malas` e `tokuno`); un'insegna mostra il proprio
testo quando il mouse vi si trova sopra. Dopo i file vengono le porte delle città, non elencate in nessun file:
come `[DoorGen` di ModernUO, vengono letti gli elementi statici della mappa alla ricerca di telai, e una porta di legno scuro va
tra due telai distanti due caselle, una porta doppia collegata quando sono distanti tre. I due
telai devono avere una differenza di altezza entro 1. Un vano porta
murato o senza pavimento non riceve porte, né uno dove è già presente una porta di un file:
gli oggetti a terra contano come gli elementi statici, quindi anche un muro posizionato da un file di decorazione chiude
il vano. Quando una metà della porta doppia non entra, non viene posizionata nessuna delle due. Alcuni vani porta
vengono lasciati aperti appositamente, come in ModernUO. Trammel e Felucca vengono lette entro i 16 rettangoli
scansionati da ModernUO, Ilshenar e Malas interamente; vengono lette, quando caricate, a piccoli pezzi per turno del ciclo di gioco,
così il gioco continua nel frattempo (circa quattro secondi in tutto); ciascuna viene segnalata come file
`<map>/generated_doors`.

Ogni file viene segnalato al termine, in gioco come messaggio di sistema:

```text
Decorating britannia/britain: 1180 placed, 3 already there, 5 skipped (SkillTeleporter 1, Spawner 4).
```

Il log del server contiene gli stessi numeri, con i tipi saltati come elenco di nomi e conteggi.

La console e il chiamante in gioco ricevono poi i totali:
`Decoration done: <placed> placed, <present> already there, <skipped> skipped in <files> files.`
Un oggetto con la stessa grafica già nel punto viene conservato, così come qualsiasi porta in un vano, quindi
eseguire nuovamente `decorate` posiziona solo ciò che manca. I file vengono letti a ogni esecuzione: un file modificato non richiede riavvio. Un
errore, come una cartella che non è una mappa, stampa `The decoration failed. Check the server
logs.` e il motivo viene registrato nel log; i file completati in precedenza restano posizionati.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`lock`](lock.md)
- [`key`](key.md)
