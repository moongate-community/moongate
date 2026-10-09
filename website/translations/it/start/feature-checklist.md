<!-- translation: {"sourceHash":"bb31db1ce43ef665b07f169640aaad564918692559573705b68bbe447da45ae3","title":"Checklist delle funzionalità"} -->

# Checklist delle funzionalità

I sistemi normalmente offerti da un emulatore completo di server Ultima Online e lo stato di
Moongate per ciascuno. Completa lo [Stato dell'implementazione](implementation-status.md), che descrive
più in dettaglio cosa funziona oggi. La [Roadmap](roadmap.md) indica in quale ordine vengono
realizzati i sistemi mancanti.

✅ completato · 🟡 parzialmente completato · ❌ non ancora realizzato

**269 sistemi:** ✅ 86 completati, 🟡 57 parzialmente completati, ❌ 126 non ancora realizzati.

**Copertura: 32%** dei sistemi completati, **43%** contando un sistema parzialmente completato come metà.

Le fondamenta (rete, login, persistenza, scripting, dati del mondo) sono pronte; restano i sistemi di gioco (combattimento, magia, abilità, economia, case).

## Account, login e rete

| Sistema | Moongate | Note |
| --- | --- | --- |
| Account | ✅ | Creazione, elenco e verifica dalla console e dall'API di amministrazione; livelli di accesso |
| Login ed elenco server | ✅ | Server di login e realm di gioco separati, oppure un unico processo autonomo |
| Selezione, creazione ed eliminazione dei personaggi | ✅ | L'eliminazione è ritardata e lo staff può annullarla |
| Messaggio del giorno | ✅ | Configurabile ed estensibile |
| Versioni del client e cifratura | ✅ | Politiche di cifratura compatibili con POL; l'[Enhanced Client](enhanced-client.md) effettua il login, crea un personaggio ed entra nel mondo, il resto è parziale |
| Server ping UDP | ✅ | Restituisce i ping su UDP 12000, come ModernUO |
| Keepalive e client inattivi | ✅ | |
| Ban IP e firewall | ❌ | |
| Ban ed espulsione degli account | ❌ | |
| Negoziazione delle funzionalità dell'assistente (Razor) | ❌ | |
| Stato dello shard per gli elenchi pubblici di shard | ❌ | |
| Logout sicuro nelle locande e nelle case | ❌ | Un personaggio lascia il mondo quando la sua sessione si chiude |
| Password con hash | ✅ | |
| Limiti ai tentativi di login e protezione dal flooding di connessioni | ❌ | |
| Protocollo proxy (indirizzo reale del client dietro un proxy) | ❌ | |
| Filtro dei pacchetti per fase di login | 🟡 | I pacchetti sconosciuti vengono rifiutati; nessun filtro per fase |
| Hook dei pacchetti: sostituire o estendere qualsiasi pacchetto | 🟡 | I plugin aggiungono pacchetti in ingresso in C#; non dagli script |
| Registrazione delle azioni sospette (risposte contraffatte, rilasci fuori portata) | ❌ | Le azioni fuori portata vengono rifiutate, non registrate come sospette |
| Statistiche di rete per client | ❌ | |
| Uno o più personaggi nel mondo per account | 🟡 | Uno per account, non configurabile |
| Script di login e logout | 🟡 | Eventi di script quando un personaggio entra nel mondo o lo lascia |
| Filtro delle parole oscene | 🟡 | Nomi vietati alla creazione del personaggio; nessun filtro sul parlato |

## Personaggi

| Sistema | Moongate | Note |
| --- | --- | --- |
| Creazione: razze, professioni, oggetti iniziali, città iniziali | ✅ | [Lettere iniziali personalizzate](data-files/starting-items.md#personalized-starting-letters) salvate nella stessa transazione di creazione |
| Statistiche | ✅ | Generate casualmente, memorizzate e impostate dagli script; un'abilità riuscita le aumenta secondo la regola classica di ModernUO, fino a 100 ciascuna e 225 complessivi, con i blocchi della finestra di stato |
| Abilità | 🟡 | Memorizzate, mostrate nella finestra delle abilità e impostate dagli script; [utilizzate, verificate e incrementate](skills.md), con i blocchi della finestra delle abilità (aumento, diminuzione, bloccata). Funzionano anche i blocchi delle statistiche della finestra di stato |
| Rigenerazione di punti vita, mana e stamina | ✅ | Un punto alla volta, ai ritmi classici di ModernUO; mana in base a intelligenza e Meditation; ritmi per mobile dagli script |
| Titoli | 🟡 | Titoli di fama e karma nella paperdoll; nessun titolo di abilità |
| Fama e karma | 🟡 | Impostati dallo staff (`.fame`, `.karma`); nulla li aumenta o riduce ancora |
| Notorietà (innocente, criminale, assassino) | 🟡 | Colore del nome dal template del mobile, grigio mentre il mobile è criminale, rosso da cinque uccisioni segnalate |
| Crimini, timer criminale e conteggio degli omicidi | 🟡 | Flag criminale con relativo timer, salvato con il mobile; attaccare un innocente o saccheggiare il cadavere di uno rende criminali. Una vittima segnala i suoi assassini in un gump: uccisioni e omicidi a breve termine, cinque rendono assassino con il nome rosso, dimenticati con il tempo (8 e 40 ore). Ancora niente furti o altri crimini |
| Fame e sete | ✅ | Entrambe da 0 a 20 e in diminuzione nel tempo: un giocatore affamato non recupera punti vita, uno assetato non recupera stamina; il cibo si mangia, le bevande si bevono un sorso alla volta |
| Veleno | ❌ | |
| Nascondersi e furtività | 🟡 | [Hiding](scripting/shipped-scripts.md#hidinglua) nasconde un giocatore fino al suo primo passo, [Stealth](scripting/shipped-scripts.md#stealthlua) gli lascia fare alcuni passi inosservato; parlare o essere colpiti non lo rivela ancora |
| Morte, cadaveri, fantasmi e resurrezione | 🟡 | Un NPC o un giocatore muore in combattimento, con `.kill` o `mobile.kill`: cadavere con ciò che trasportava, animazione e suono di morte, decadimento dopo 7 minuti. Un giocatore resta come fantasma (nascosto ai vivi se non in modalità guerra, sentito come oOo, senza combattere, usare abilità o sollevare oggetti) e viene resuscitato a un ankh o da un guaritore (per un decimo della sua fama), con `.resurrect` o `mobile.resurrect`. Niente ossa |
| Protezione dei giovani giocatori | ❌ | |
| Denunce di omicidio e bacheche delle taglie | 🟡 | Il gump di denuncia che una vittima riceve dopo la morte funziona, con i conteggi che aggiunge; niente taglie né bacheche |
| Virtù | ❌ | |
| Barra di stato | ✅ | Nome, statistiche, punti vita, mana, stamina, oro, peso |
| Stato esteso (resistenze, fortuna, limiti) | ❌ | I blocchi delle statistiche sono realizzati, vedi Statistiche |
| Privilegi dello staff (spostare qualsiasi cosa, vedere i nascosti, invulnerabilità) | 🟡 | Gli oggetti a terra e i mobile nascosti sono mostrati solo allo staff, e `.go` porta un game master ovunque; nessuno spostamento universale o invulnerabilità |
| Volo dei gargoyle | ❌ | |
| Costo del movimento e consumo di stamina in base al peso | ✅ | Correre costa un punto ogni 16 passi; in sovraccarico, ogni passo costa 5 o più, e senza stamina il passo viene rifiutato. Nessuna cavalcatura ancora |
| Polymorph e incognito | ❌ | |
| Esperienza e livelli (opzionali) | ❌ | |
| Fazioni | ❌ | |
| Armi Slayer | ❌ | |
| Rinominare animali e personaggi | ❌ | |
| Selezione del volto | ❌ | |

## Combattimento

| Sistema | Moongate | Note |
| --- | --- | --- |
| Modalità guerra, corpo a corpo e tempi dei colpi | 🟡 | Un giocatore [combatte](combat.md) a pugni o con l'arma che impugna: timer del colpo, probabilità di colpire tramite abilità, danno con tactics, forza, anatomy e armatura del bersaglio, suoni e animazioni |
| Tiro con l'arco | ✅ | Gli NPC e i giocatori che impugnano un arco o una balestra sparano dalla sua portata, con la freccia che vola verso il bersaglio; un giocatore consuma una freccia o un dardo a ogni colpo, deve stare fermo un secondo e ritrova il 40% delle munizioni a terra |
| Armi e armature: danno, armatura, durabilità, resistenze | 🟡 | L'arma impugnata e l'armatura indossata da un giocatore determinano [tempi dei colpi, probabilità di colpire, danno e riduzione del danno subito](combat.md#weapons-and-armor); nessuna durabilità, parata, scudo o resistenza |
| Parata | ❌ | |
| Mosse speciali delle armi | ❌ | |
| IA di combattimento degli NPC | 🟡 | `monster.lua` su ogni creatura malvagia o caotica in UOX3 (circa 200 template): notano un giocatore o un cittadino, gli si avvicinano, lo combattono e fanno la guardia quando lo perdono, anche gli incantatori in corpo a corpo, e le guardie cittadine li attaccano. Gli animali passeggiano e rispondono se colpiti, quelli paurosi fuggono. Se feriti scappano (sotto il 20% dei punti vita, mai i non morti o gli elementali). Gli arcieri sparano dalla portata del loro arco. Niente incantesimi |
| Guardie nelle regioni sorvegliate | 🟡 | Un giocatore che dice "guards" fa arrivare una guardia accanto a ciascun criminale nelle vicinanze: appare, pronuncia la sua battuta e se ne va. Le guardie presenti nelle città notano autonomamente un criminale e lo raggiungono (`guard.lua`). Un NPC criminale viene ucciso con un colpo e lascia il cadavere; un giocatore criminale viene solo raggiunto: le guardie non puniscono ancora i giocatori. Le guardie chiamate a Ilshenar e Malas sono arcieri: sparano dalla portata del loro arco |
| Abilità speciali dei mostri | ❌ | |
| Danno elementale e resistenze | ❌ | |
| Elenchi degli aggressori e timeout degli attacchi | ❌ | |
| Sangue sul colpo | ✅ | Un colpo che fa danno lascia sangue a terra che sparisce dopo qualche secondo; il `blood_hue` di una creatura lo colora o, a `-1`, lo impedisce (`ultima.combat.blood_*`) |
| Numeri del danno sopra le teste | ✅ | Il danno di un colpo appare sopra chi lo subisce (`ultima.combat.display_damage_numbers`) |

## Magia

| Sistema | Moongate | Note |
| --- | --- | --- |
| Lancio degli incantesimi: mana, reagenti, fallimento, resistenza | ❌ | |
| Incantesimi di magery | ❌ | |
| Necromancy | ❌ | |
| Libri degli incantesimi, pergamene e bacchette | ❌ | |
| Campi ed evocazioni | ❌ | |
| Regole magiche delle regioni (no recall, no gate) | ❌ | |
| Chivalry | ❌ | |
| Bushido | ❌ | |
| Ninjitsu | ❌ | |
| Spellweaving | ❌ | |
| Mysticism | ❌ | |
| Maestrie delle abilità | ❌ | |
| Parole di potere | ❌ | |

## Abilità

| Sistema | Moongate | Note |
| --- | --- | --- |
| Usare e incrementare un'abilità | 🟡 | Un'[abilità](skills.md) viene usata dalla finestra delle abilità (`scripts/skills/<skill>.lua`), verificata da `skill.check` e incrementata con la formula di ModernUO; viene fornita solo Hiding, e le statistiche non aumentano ancora con le abilità |
| Raccolta: estrazione mineraria, taglio della legna, pesca | 🟡 | [Pesca](fishing.md) con una canna: acqua entro 4 caselle, 8 secondi, un pesce, una vecchia calzatura o niente in base all'abilità; i pesci di un luogo si esauriscono per zona e ritornano ([`harvest.toml`](data-files/harvest.md)). [Taglio della legna](lumberjacking.md) con un'ascia in mano: un albero entro 2 caselle, da uno a tre colpi, 10 tronchi in base all'abilità, la legna di un luogo che si esaurisce allo stesso modo. [Estrazione](mining.md) con un piccone o una pala: minerale di ferro dalla roccia di montagne e grotte, fuso in lingotti a una forgia. Niente altri metalli, niente prese speciali, niente tipi di legno, niente assi |
| Motore di creazione: menu, ricette, risorse, qualità | ❌ | |
| Mestieri: forgiatura, sartoria, carpenteria, meccanica, alchimia, cucina, iscrizione, fabbricazione di archi e frecce, cartografia | ❌ | |
| Riparare e migliorare gli oggetti | ❌ | |
| Addomesticamento e conoscenza degli animali | ❌ | |
| Guarigione e veterinaria | 🟡 | La [benda](scripting/shipped-scripts.md#bandagelua) pulita cura un giocatore o una creatura (Healing e Anatomy, Veterinary e Animal Lore) e resuscita un fantasma; niente veleno, niente sanguinamento, niente animali |
| Scassinamento, rimozione delle trappole | 🟡 | Il [grimaldello](scripting/shipped-scripts.md#lockpicklua-and-treasure_chestlua) scassina un oggetto chiuso (i forzieri del tesoro dei dungeon nascono chiusi); niente trappole, quindi niente rimozione. Le porte si chiudono e si aprono con la loro chiave |
| Frugare e rubare | 🟡 | [Snooping](scripting/shipped-scripts.md#snoopinglua) apre lo zaino di un altro mobile con un doppio clic; niente furto |
| Inseguimento, individuazione dei nascosti, medicina legale, parlare con gli spiriti | 🟡 | [Individuazione dei nascosti e medicina legale](scripting/shipped-scripts.md#the-lore-skills) funzionano (niente trappole, niente gilda dei ladri); niente inseguimento, niente parlare con gli spiriti |
| Abilità del bardo: musicalità, pacificazione, provocazione, discordanza | ❌ | |
| Abilità di conoscenza: anatomia, conoscenza delle armi, identificazione degli oggetti, valutazione dell'intelligenza, identificazione dei sapori | 🟡 | [Anatomia e valutazione dell'intelligenza](scripting/shipped-scripts.md#the-lore-skills) leggono le caratteristiche di un bersaglio; niente conoscenza delle armi (manca la durabilità), niente identificazione degli oggetti, niente identificazione dei sapori |
| Meditazione, mendicità, pastorizia, campeggio, avvelenamento | ❌ | |
| Smembrare i cadaveri | ❌ | |
| Regioni delle risorse (minerali, legna, pesci per area, rigenerazione) | ❌ | |
| Lavorazione delle risorse: fusione, telai, filatoi, pelli | ❌ | |
| Classi e limiti delle abilità (totale abilità, totale statistiche) | 🟡 | Il totale delle abilità si ferma a `ultima.skills.total_cap` (700.0) e il totale delle statistiche a `ultima.skills.stat_cap` (225), entrambi riducendo ciò che è impostato in diminuzione; nessuna classe |
| Oggetti di allenamento: manichini, bersagli per borseggio, bersagli per tiro con l'arco | 🟡 | I [manichini](scripting/shipped-scripts.md#training_dummylua) rispondono al colpo e fanno salire l'abilità dell'arma fino a 25, e i [bersagli per il tiro con l'arco](scripting/shipped-scripts.md#archery_buttelua) prendono le frecce e danno il punteggio; niente bersagli per borseggio, niente freccette |

## NPC

| Sistema | Moongate | Note |
| --- | --- | --- |
| Template, nomi, equipaggiamento e bottino degli NPC | ✅ | Vestiti e con il loro bottino alla comparsa |
| Comportamento da script | ✅ | Script Lua per i mobile: `on_think`, `on_speech`, `on_spawn`, `on_mobile_in_range`, `on_death`, `on_drag_drop` |
| Inattività lontano dai giocatori | ✅ | Gli NPC pensano solo vicino a un giocatore |
| Movimento casuale | 🟡 | `wander.lua` mantiene gli NPC generati nella loro area di origine |
| Parole chiave del parlato e risposte | 🟡 | Le parole chiave del client raggiungono `on_speech` in qualsiasi lingua; i banchieri rispondono a *bank*, *balance*, *withdraw* e *check*, e alla parola *deposit*, che non ha una parola chiave; nessuna parola chiave dei venditori ancora |
| Creature acquatiche e anfibie | ✅ | Compaiono sull'acqua e nuotano |
| Tipi di IA (venditore, guardia, guaritore, animale, mostro, incantatore) | ❌ | |
| Ricerca del percorso, inseguimento e fuga | 🟡 | Ricerca del percorso A*; uno script porta un NPC in un luogo o dietro qualcuno con `npc.walk_to`, passando dalle porte chiuse che apre e aggirando quelle a chiave e i mobili. Le creature ferite scappano da uno scontro (20% dei loro punti vita, 10% gli animali) e gli animali paurosi da un colpo; un NPC che vaga non apre porte, e i mobile non bloccano il percorso. Uno script fa camminare il client di un giocatore verso un punto con `mobile.pathfind_to` (pacchetto 0x38) |
| Animali e seguaci: comandi, lealtà, legame | ❌ | |
| Cavalcature | ❌ | |
| Eventi di script per gli NPC (parlato, portata, danno) | 🟡 | Parlato e portata; nessun evento di combattimento |
| Raccolte di nomi | ✅ | Elenchi di nomi per tipo e genere |
| Bisogni: cibo, pascolo, desideri | ❌ | |
| Azioni speciali delle creature (soffio, lancio di rocce, ragnatele) | ❌ | |
| Ritorno a casa quando si perdono | 🟡 | Gli NPC generati tornano a piedi nella loro area di origine |
| Statuette degli animali (riduzione degli animali) | ❌ | |
| Spawn dei campioni | ❌ | |
| Boss Peerless e paragoni | ❌ | |
| Accampamenti (briganti, orchi, prigionieri) | ❌ | |

## Servizi degli NPC

| Sistema | Moongate | Note |
| --- | --- | --- |
| Venditori: acquisto, vendita, rifornimento | ✅ | La finestra del negozio, la merce e le tabelle di vendita dei negozi di ModernUO, oro dello zaino poi della banca, tutto o niente, il rifornimento orario e la rivendita di ciò che i giocatori hanno venduto; vedi [Venditori](vendors.md) |
| Banchiere e cassetta di banca | ✅ | La parola chiave *bank* in qualsiasi lingua del client; aperta mentre il giocatore resta fermo; saldo, prelievo e deposito tramite parlato; limite di oggetti; vedi [Banca](bank.md) |
| Stalliere, veterinario | ❌ | |
| Istruttori delle abilità | 🟡 | Venditori e guaritori insegnano le abilità che hanno a 60,0 o più, in cambio di oro trascinato su di loro; vedi [Abilità](skills.md#trainers) |
| Guaritori che resuscitano | 🟡 | Un fantasma che si avvicina a un guaritore riceve l'offerta di tornare in vita; i guaritori malvagi non sono ancora posizionati nel mondo |
| Venditori dei giocatori | ❌ | |
| Mercenari | ❌ | |
| Missioni di scorta | ❌ | |
| Maestri di gilda | ✅ | I dodici maestri di gilda di ModernUO insegnano il loro mestiere, accettano membri per la loro gilda con parole e oro, e sono collocati dagli spawn di ModernUO; vedi [Abilità](skills.md#guildmasters) |
| Ordini di produzione in massa | ❌ | |

## Oggetti

| Sistema | Moongate | Note |
| --- | --- | --- |
| Template e creazione degli oggetti | ✅ | Template TOML con ereditarietà |
| Spostamento, impilamento, divisione e unione | ✅ | |
| Indossare: livelli e armi a due mani | ✅ | Nessun requisito di forza ancora |
| Contenitori del personaggio | ✅ | |
| Contenitori a terra, limiti di peso e di oggetti | ✅ | Un contenitore a terra si apre entro due caselle; gli oggetti possono essere estratti e inseriti, e i giocatori intorno li vedono entrare e uscire; massimo 125 oggetti e fino al limite di stone del contenitore (400 in assenza di un limite); le pile si uniscono all'interno |
| Tooltip e nomi con un clic | ✅ | |
| Oggetti a terra e relativo decadimento | ✅ | |
| Oggetti da script | ✅ | Script Lua per gli oggetti: uso, equipaggiamento, rimozione, raccolta, rilascio, creazione, oscurità, un giocatore che vi cammina sopra, parlato nelle vicinanze |
| Tabelle del bottino | ✅ | Generato nello zaino di ogni NPC comparso, nei forzieri del tesoro e nei contenitori cittadini, e dagli script con `item.add_loot` |
| Porte | ✅ | Apertura e chiusura; porte doppie collegate; una porta chiusa blocca il passaggio, lo staff la attraversa; umani e mostri che camminano verso un luogo aprono quelle non a chiave |
| Serrature e chiavi | ✅ | Le porte chiuse a chiave si aprono per un giocatore che porta la loro chiave |
| Luci | ✅ | Accensione e spegnimento; i lampioni si accendono di notte |
| Pozioni e cibo | 🟡 | Il cibo si mangia: fame, stamina, suono e gesto. Le bevande si bevono a sorsi, lasciando una brocca o un bicchiere vuoto; nessun riempimento, nessuna ubriachezza. Una pozione di esempio viene bevuta e consumata, senza ancora alcun effetto |
| Libri | 🟡 | Creazione da parte dello staff con [`.book`](commands/book.md) e pergamene leggibili da [template di testo TOML](data-files/books.md), variabili del destinatario congelate, titolo/autore/testo salvati e [allegati alle lettere ritirabili una sola volta](data-files/books.md#letter-attachments); [62 libri di lore in otto lingue](book-content-import.md), che aprono il [libro](data-files/books.md#books-and-parchments) del client con copertina e pagine; [libri in cui il giocatore scrive](data-files/books.md#books-a-player-writes-in), uno per ogni nuovo personaggio; copia e firma dei libri non realizzate |
| Mappe e mappe del tesoro | ❌ | |
| Rune, recall e portali | ❌ | |
| Moongate e teletrasporti | 🟡 | Teletrasporti attivati camminandoci sopra e quelli che rispondono a una parola, anche tra mappe, posizionati da `.decorate` con quelli del mondo e dei dungeon di ModernUO; moongate pubblici con un gump di destinazione; moongate semplici con una destinazione (`.moongate`); nessun Gate Travel |
| Tinture e vasche per tintura | 🟡 | Le tinture danno alla vasca il colore scelto nel selettore del client, la vasca lo trasferisce a ciò che è tingibile, come gli abiti; nessuna vasca per pelle, mobili, nera o metallica |
| Parrucchiere, tintura per capelli e kit di travestimento | ❌ | |
| Scambio sicuro | ❌ | |
| Oggetti magici e proprietà degli oggetti | ❌ | |
| Bottino magico casuale e artefatti | ❌ | |
| Imbuing e reforging | ❌ | |
| Pergamene del potere, pergamene delle statistiche e soulstone | ❌ | |
| Giochi (scacchi, dama, backgammon) | ❌ | |
| Piante e agricoltura | ❌ | |
| Trappole ed enigmi dei dungeon | ❌ | |
| Forzieri dei dungeon e delle città che si riempiono di nuovo | 🟡 | I forzieri del tesoro dei dungeon di ModernUO, livelli da 1 a 4: una regione di spawn ne crea uno con oro e bottino, decade e ne compare uno nuovo. Le casse, i forzieri, i barili e le librerie cittadini si riempiono quando vengono aperti, ogni 60–90 minuti, dalle 35 tabelle di ModernUO. Nessuna serratura o trappola |
| Messaggi in bottiglia e tesori pescati | ❌ | |
| Attributi degli oggetti: benedetto, maledetto, newbie, assicurato | 🟡 | Benedetto e maledetto nei tooltip; nessuna regola sottostante |
| Atti e riconversione in atti | ❌ | |
| Bende | ❌ | |
| Strumenti musicali | ❌ | |
| Strumenti di navigazione: sestante, cannocchiale, orologi | 🟡 | Gli orologi indicano la parte del giorno e l'ora del luogo in cui si trovano (`clock.lua`); nessun sestante o cannocchiale |
| Barili di pozioni e atti per merci | ❌ | |
| Cestini | ❌ | |
| Cristalli di comunicazione | ❌ | |
| Pietre degli oggetti (distributori) | ❌ | |
| Cannoni | ❌ | |
| Talismani e proprietà dei gioielli | ❌ | |
| Controlli di integrità e rilevamento degli orfani | ❌ | |

## Mondo

| Sistema | Moongate | Note |
| --- | --- | --- |
| Mappe, elementi statici e multi dai file del client | ✅ | MUL e UOP |
| Controlli del movimento e della linea di vista | ✅ | Movimento: terreno, elementi statici e oggetti a terra (una porta chiusa, una cassa); linea di vista: terreno ed elementi statici. Mobile e multi non sono ancora inclusi in nessuno dei due |
| Settori delle mappe e portata visiva | ✅ | |
| Giorno e notte | ✅ | Per mappa e longitudine, con le fasi lunari; `.globallight`, `.time` |
| Illuminazione di dungeon e prigioni | ✅ | |
| Meteo per regione | ✅ | Pioggia, neve, temporali con tuoni, asciutto al chiuso; nessun danno meteorologico |
| Stagioni | ✅ | Per mappa e regione, rotazione facoltativa con i giorni di gioco; `.season` |
| Regioni | 🟡 | Individuate per ogni giocatore; impostano meteo, musica, stagione e illuminazione dei dungeon, e il giocatore legge il luogo in cui entra o che lascia e se è protetto dalle guardie, e le guardie delle città agiscono di conseguenza; nessuna regola per le case |
| Musica delle regioni | ✅ | Il brano della regione, altrimenti quello della mappa; `.music` |
| Regole delle regioni: sicura, no PvP, no case, logout istantaneo | ❌ | |
| Politica cittadina (sindaci, tasse) | ❌ | |
| Decorazione del mondo | ✅ | Posizionata da `.decorate`, con le insegne dei negozi e le porte cittadine lette dalla mappa |
| Regioni di spawn | ✅ | Su ogni mappa: dati UOX3, quelli di ModernUO per New Haven, Malas, Tokuno e TerMur; primo popolamento rapido, `.initial_spawn`, rigenerazione, terra e acqua; anche regioni di oggetti, come i forzieri del tesoro; vedi [Spawn degli NPC](spawns.md) |
| Oggetti spawner | ❌ | |
| Case: posizionamento, insegna, proprietari, amici e ban | ❌ | |
| Blocco degli oggetti nelle case, contenitori sicuri e decadimento | ❌ | |
| Componenti aggiuntivi delle case (forge, telai, mobili composti) | ❌ | |
| Barche | ❌ | |
| Cambi e regole delle facet | ❌ | |
| Suoni ambientali | ❌ | |
| Più mappe contemporaneamente | ✅ | Ogni mappa di `maps.toml` |
| Fasi lunari | ✅ | Trammel e Felucca sull'orologio di gioco, come il cannocchiale di ModernUO; `.time`, `world.moon` in Lua |
| Progettazione personalizzata delle case | ❌ | |
| Inattività dei settori | ✅ | Gli NPC lontani dai giocatori non hanno alcun costo |
| Importazione ed esportazione del mondo | ❌ | |
| Salvataggi paralleli e incrementali del mondo | 🟡 | Vengono scritte solo le snapshot cambiate, in una transazione in background; non paralleli |

## Socialità

| Sistema | Moongate | Note |
| --- | --- | --- |
| Parlato locale | ✅ | Giocatori e NPC sentono ciò che viene detto nelle vicinanze |
| Gruppi | ❌ | |
| Gilde | ❌ | |
| Finestra della chat | ❌ | |
| Bacheche | ✅ | Ogni bacheca ha i propri messaggi, in discussioni con risposte; l'autore o un game master li rimuove; le discussioni scadono alcuni giorni dopo l'ultima risposta e una bacheca piena elimina la discussione più vecchia, entrambi configurabili nelle impostazioni; gli script pubblicano, elencano e rimuovono con il modulo `board`; vedi [Bacheche](bulletin-boards.md) |
| Duelli, arene e tornei | ❌ | |
| Profilo del personaggio | ❌ | |
| Finestra dei suggerimenti | ❌ | |
| Freccia e pulsante delle missioni | ❌ | |
| Missioni | ❌ | |
| Modalità del parlato: parlare, sussurrare, urlare, emote | ✅ | Un sussurro si sente a 1 casella, un urlo a 18, parlato ed emote a 15; gli script di NPC e oggetti sanno come è stato detto |

## Economia

| Sistema | Moongate | Note |
| --- | --- | --- |
| Oro | 🟡 | Oro iniziale e bottino degli NPC; nulla su cui spenderlo |
| Banca e assegni bancari | ✅ | Cassetta di banca con limite di oggetti; saldo, prelievo, deposito e assegno tramite parlato; assegni incassati con doppio clic nella cassetta; oro e assegni consegnati al banchiere vengono depositati; criminali rifiutati; vedi [Banca](bank.md) |
| Costi e limiti delle case | ❌ | |
| Oro condiviso nell'account | ❌ | |
| Ricerca dei venditori e aste | ❌ | |
| Ricompense dei veterani | ❌ | |

## Amministrazione

| Sistema | Moongate | Note |
| --- | --- | --- |
| Comandi con livelli di accesso | ✅ | Dalla console (completamento di comandi e argomenti con TAB, cronologia con Su/Giù) e in gioco; vedi [Comandi](commands.md) |
| Salvataggio del mondo | ✅ | Periodico e allo spegnimento, con `.save` |
| Backup del database | ✅ | Esportazioni SQL a rotazione, pianificate e con `.sql_backup`; ripristino con psql |
| Console | ✅ | |
| Configurazione del server | ✅ | `moongate.toml`, validato all'avvio |
| Regole per epoca (classiche o moderne) | ❌ | Un insieme di regole, prima quello classico; vedi la [Roadmap](roadmap.md) |
| Impostazioni delle regole di gioco (combattimento, magia, rigenerazione, crimini) | ❌ | Arrivano con ogni sistema |
| Amministrazione degli account | 🟡 | Console e API di amministrazione; nessun ban |
| Amministrazione remota | 🟡 | API gRPC con TLS; nessun pannello web |
| Metriche e diagnostica | ✅ | Metriche del processo e dei plugin |
| Ricaricamento a caldo | 🟡 | Script Lua; non dati o template |
| Coda di richieste ai GM (page) | ❌ | |
| Menu di aiuto e menu per personaggi bloccati | ✅ | Il pulsante Help apre un menu: «Sono bloccato» porta un personaggio alla città di partenza più vicina dopo un'attesa, con una pausa; comandi utili; regole del server; vedi [Aiuto](help.md) |
| Prigioni | ✅ | Un gump elenca le celle e i detenuti; pene in giorni reali, multa in oro e nota di rilascio alla fine, una cassa di pane e acqua in ogni cella; un giocatore offline viene incarcerato per nome e sconta la pena dal login successivo; vedi [Prigione](jail.md) |
| Elenco dei presenti | ❌ | |
| Strumenti dello staff: gump delle proprietà, menu di aggiunta, comandi di area | 🟡 | Il gump dei luoghi nominati, `.go`, con i 558 luoghi di ModernUO, e `.gmtools`, un gump con barra laterale di strumenti: forza il meteo, imposta la stagione della mappa, mostra l'ora e imposta la luce; nessun gump delle proprietà, menu di aggiunta o comando di area |
| Luoghi nominati e menu di viaggio per lo staff | ✅ | `.go <place>` e il gump go, con i 558 luoghi di ModernUO da `data/locations.toml` |
| Pagine web di stato | ❌ | |
| Segnalazioni di bug | 🟡 | Rapporti delle eccezioni pronti per una issue GitHub; nessuna segnalazione in gioco |
| Registrazione nei log | ✅ | Log strutturati con livelli |
| Log dei pacchetti per client | ❌ | L'opzione `--log-packets` viene analizzata ma non usata |
| Log dei comandi | ❌ | |
| Console remota (telnet) | ❌ | Al suo posto l'API gRPC |
| Dump dei crash e watchdog | ❌ | |
| Servizio Windows | ❌ | Al suo posto immagini Docker |

## Scripting e contenuti

| Sistema | Moongate | Note |
| --- | --- | --- |
| Motore di script | ✅ | Lua 5.2 isolato con un budget di istruzioni |
| Script associati ai template | ✅ | `script_id` sui template di oggetti e mobile |
| Più script su un oggetto, script per tipo di oggetto | ❌ | |
| Eventi di script | 🟡 | Eventi di NPC, oggetti e personaggi, `player_say` e `player_region_changed`; nessun evento di combattimento o abilità |
| Eventi che possono rifiutare l'azione predefinita | 🟡 | Oggetti: `on_use`, `can_pick_up`, `can_drop`, `can_equip`, `can_insert`; nessuno ancora per abilità e combattimento |
| Eventi di ingresso e uscita dalle regioni | 🟡 | `player_region_changed` per i giocatori; non per gli NPC |
| API di script | 🟡 | `npc`, `item`, `world`, `mobile`, `gump`, `bank`, `effect`, `moongates`, `locations`, `dice`, `localization`, `timer`, `events`, `engine`, `log`, `target`, `prompt`, `skill`, `commands`; statistiche e abilità di un mobile vengono lette e scritte |
| Interrogazioni del mondo dagli script (oggetti vicini, visibili, per seriale) | ✅ | `world.mobiles_in_range`, `world.items_in_range`, `world.players`, `world.line_of_sight`, `world.standing_z`, `world.region`, `world.is_occupied`, `world.carries` |
| Creare e spostare oggetti dagli script | ✅ | Creazione a terra o in uno zaino, bottino in un contenitore, spostamento, equipaggiamento, ricerca per template, consumo, eliminazione |
| Messaggi, cursore bersaglio e richieste dagli script | ✅ | `npc.say`, `mobile.message`, `item.message`, `item.message_cliloc`, `target.pick`, `prompt.ask`, gump |
| Timer di script | ✅ | |
| Timer mantenuti da un oggetto e salvati con il mondo | 🟡 | Oggetti: `item.start_timer` e `on_timer`, mantenuti tra i riavvii; non sui mobile |
| Comandi dai plugin | ✅ | In C#; non da Lua |
| Contenuti definiti dai dati | ✅ | Template e file di dati TOML, validati all'avvio |
| Importazione dei contenuti di un altro emulatore | ✅ | Oggetti, bottino, NPC, nomi, oggetti iniziali, elenchi NPC e regioni di spawn UOX3; spawner, decorazione, insegne, teletrasporti, luoghi nominati e forzieri del tesoro ModernUO |
| Protezione dagli script fuori controllo | ✅ | Budget di istruzioni per ripresa e per blocco |
| Valori persistenti sugli oggetti | ✅ | Props su oggetti, NPC e giocatori, salvate insieme a essi |
| Dati globali persistenti degli script | ✅ | `world.get_prop`, `world.set_prop`: props dell'intero shard salvate con il mondo |
| Debugger di script per un IDE | ❌ | Solo definizioni per il completamento nell'editor |
| Profilazione degli script | 🟡 | Metriche degli script; nessun profilo per funzione |
| File, HTTP, SQL ed email dagli script | ❌ | |
| Servizi TCP esterni gestiti dagli script | ❌ | |
| Gump e finestre di dialogo da script | ✅ | Layout XML con callback Lua, slot e builder; vedi [Il tuo primo gump](gump-tutorial.md) |
| Hook che sostituiscono le regole fondamentali (verifica abilità, combattimento, decadimento) | ❌ |  |
| Messaggi di sistema sostituibili | ✅ | Ogni messaggio nei file `data/messages` |

## Interfaccia

| Sistema | Moongate | Note |
| --- | --- | --- |
| Cursore bersaglio | ✅ | |
| Messaggi localizzati | ✅ | 8 lingue; una lingua può essere suddivisa in `data/messages/<language>/*.toml` |
| Razze | 🟡 | Corpi e aspetti di umani, elfi e gargoyle; nessuna meccanica di gioco razziale |
| Gump | ✅ | Layout XML verificati da un XSD, script Lua, gump costruiti in Lua, gump concatenati, risposte verificate |
| Menu | ❌ | |
| Menu contestuali | 🟡 | Il [menu](context-menus.md) di un mobile o di un oggetto: Apri paperdoll e Apri zaino, e le voci che aggiunge uno script Lua (`on_context_menu`, `on_context_menu_select`), come Apri cassetta di sicurezza del banchiere; la scelta viene verificata rispetto al menu inviato. Le icone del client migliorato scelgono la voce con il suo testo. Ancora nessuna voce per venditori, animali, addomesticamento o party: arrivano con quei sistemi |
| Barra dei buff | ❌ | |
| Controlli della sequenza e della velocità di camminata | ✅ | |
| Peso e sovraccarico | ✅ | Ciò che un mobile trasporta e può trasportare (40 e 3.5 per punto di forza) viene conteggiato e mostrato; un giocatore sovraccarico si stanca a ogni passo |
| Effetti temporanei (buff e debuff) | ❌ | |
| Richieste di testo e input | ✅ | Prompt Unicode (0xC2), dagli script con il modulo `prompt` |
| Effetti visivi: movimento, fulmini, particelle | ✅ | Dagli script con il modulo `effect`; particelle per l'Enhanced Client |
| Suoni e musica | ✅ | Suoni dagli script, tuoni e musica delle regioni |
| Lingua del client | ❌ | Una lingua del server per tutti, tra le 8 fornite |
| Negozio e altri pannelli moderni del client | ❌ | |

## Cosa aggiunge Moongate

Sistemi assenti dalla maggior parte degli emulatori:

- Server di login e realm di gioco come processi separati, individuati tramite Redis, con ticket
  di trasferimento monouso.
- Persistenza PostgreSQL con migrazioni versionate, applicate da `mgctl migrate` mentre il server è
  fermo, oppure dal server stesso all'avvio quando `persistence.auto_apply_migrations` è attivo.
- Salvataggi del mondo che non fermano mai il gioco: circa 0.1 s sul ciclo di gioco per copiare 173.000 entità,
  poi solo le righe cambiate vengono scritte in background; vedi
  [Un salvataggio non ferma il gioco](persistence-operations.md#a-save-does-not-stop-the-game).
- Un'API di amministrazione gRPC con TLS.
- Una [prigione](jail.md) gestita da un gump: lo staff sceglie una cella e i giorni, e il server fa
  il resto, dalla multa e dalla nota di rilascio a fine pena al pane e all'acqua nella
  cella.
- Plugin che aggiungono servizi, comandi, moduli Lua, metriche, entità e le proprie impostazioni.
- Immagini Docker e un esempio con più realm.
