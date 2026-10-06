<!-- translation: {"sourceHash":"7dfd90342f598e460b4b306389474bbdae198b11c7a0436f97888e0965343a1b","title":"Roadmap"} -->

# Roadmap

Cosa realizza Moongate prossimamente e in quale ordine. La [checklist delle funzionalità](feature-checklist.md) indica
cosa manca sistema per sistema; questa pagina indica cosa viene prima e perché.

L'ordine deriva da un confronto con altri cinque emulatori: ModernUO, ServUO, UOX3, POL e
SphereServer X. Per ciascuno abbiamo esaminato quali sistemi possiede, quanto sono grandi e cosa serve a ciascun
sistema prima di poter esistere. I cinque concordano sulla stessa catena di dipendenze, quindi le fasi
sotto seguono quella catena. All'interno di una fase vengono prima i sistemi che un giocatore nota per primi.

Questo è un ordine, non un calendario: non ci sono date. Una fase è completata quando i suoi sistemi sono marcati
✅ nella checklist.

## Dove siamo

Le fondamenta sono pronte: rete, login, persistenza, dati del mondo, settori, regioni, spawn,
decorazione, gump, scripting Lua. Un giocatore può effettuare il login, camminare in un mondo popolato, aprire porte, usare la
banca, prendere un teletrasporto o un moongate, aprire un forziere del tesoro in un dungeon e una cassa in un negozio.
Gli NPC aggirano gli ostacoli. Ciò che manca è il gioco: nulla può ancora essere combattuto,
appreso, acquistato o costruito.

Ogni passaggio sotto ha il proprio stato: ✅ completato, 🟡 parzialmente completato, ❌ non iniziato.

## Priorità

Le fasi indicano l'ordine delle dipendenze; questo elenco indica su cosa si lavora, dal più urgente.
Una priorità si chiude quando i suoi passaggi sono ✅ nelle tabelle sotto.

| Priorità | Cosa | Passaggi | Perché in questa posizione |
| --- | --- | --- | --- |
| 1 | **Completare ciò che serve a Lua** (completato) | 0.1, 0.3 e 0.4 sono completati; 0.5 e 0.6 sono completati per gli oggetti, e ciò che resta arriva con abilità, combattimento ed effetti temporanei | Ogni regola sotto viene scritta su questa base, quindi una lacuna qui si paga di nuovo in ogni sistema |
| 2 | **Abilità e rigenerazione** | 1.1, 1.2 | La verifica delle abilità viene chiamata da combattimento, magia, creazione, scassinamento e addomesticamento: nient'altro sblocca altrettanto |
| 3 | **Ciò che legge il combattimento** | 1.3, 1.6 e il peso di 1.4 | Effetti temporanei, campi di combattimento degli oggetti, peso |
| 4 | **Combattimento e morte** | 2.1 a 2.6 | Il ciclo fondamentale: i 29.000 NPC generati sono scenografia finché non esiste |
| 5 | **Un mondo che risponde agli attacchi** | 3.2 a 3.5 e mobile che bloccano in 3.1 | Rende il combattimento utile e le città sicure |
| 6 | **Venditori** | 1.5, 4.1 | Richiede solo la priorità 2, quindi può essere affrontata tra due passaggi del combattimento; dà un uso all'oro |
| 7 | **Magery e viaggio** | 4.3, 4.4, 4.5 | Metà dei personaggi lancia incantesimi; recall e gate richiedono le regole delle regioni della priorità 5 |
| 8 | **Professioni** | Fase 5 | Raccolta, creazione, addomesticamento e abilità rimanenti, ciascuna piccola quando esiste la priorità 2 |
| 9 | **Giocare insieme** | Fase 6, 4.2 | Il gruppo (6.1) non richiede altro e può arrivare prima, quando i giocatori lo chiedono |
| 10 | **Case e barche** | Fase 7 | La catena di prerequisiti più lunga |

[Gestire uno shard](#running-a-shard) non ha una posizione in questo elenco: le sue voci vengono completate quando un operatore
ne ha bisogno.

### Contenuti del mondo in attesa di un sistema

Alcuni contenuti sono già nei dati, posizionati o convertiti, e non fanno ancora nulla. Non vengono realizzati
separatamente: arrivano con la priorità che dà loro la regola.

| Contenuto | Attende | Priorità |
| --- | --- | --- |
| Manichini da allenamento, bersagli di tiro con l'arco e bersagli per freccette (107 posizionati) | Incremento delle abilità (1.2) | 2 |
| Serrature e scassinamento dei forzieri del tesoro e dei contenitori cittadini | Verifica delle abilità (1.2) | 2 |
| Trappole dei forzieri e le 490 trappole posizionate nei dungeon | Danno (2.1) | 4 |
| Bacchette nei forzieri del tesoro | Incantesimi (4.3) | 7 |
| Forge e incudini per la creazione | Creazione (5.2) | 8 |
| Scacchiere, tavoli da dama e bacheche delle taglie (le [bacheche](bulletin-boards.md) funzionano) | Chat e bacheche (6.3) | 9 |
| I 621 addon dei negozi e delle locande (incudini, forni, letti, telai), saltati da `.decorate` | Addon (7.4) | 10 |
| Gli 87 spawner dei personaggi delle missioni, saltati da `.decorate` | Missioni ([In seguito](#later)) | Dopo 10 |

## Prima una decisione: le regole

Ogni altro emulatore deve scegliere tra regole classiche (prima di Age of Shadows) e moderne
(proprietà degli oggetti, resistenze, mosse speciali). ModernUO controlla l'epoca in 378 file; UOX3 e
Sphere hanno un'impostazione dedicata.

Moongate realizza prima **un solo** insieme di regole: quello classico. È più piccolo, i dati UOX3 già
convertiti sono scritti per esso, e i sistemi moderni possono essere aggiunti sopra in seguito (vedi
[In seguito](#later)). L'Enhanced Client continua a funzionare: richiede solo i pacchetti, non le regole
moderne.

## Fase 0: ciò che serve a Lua prima di qualsiasi meccanica di gioco

POL lascia quasi tutto il gioco agli script, e Sphere e UOX3 fanno lo stesso tramite trigger. Tutti
e tre mostrano che il core deve esporre queste cose prima di poter scrivere combattimento, magia o
creazione. La [guida di riferimento API Lua](https://moongate.sh/lua/) elenca ciò che Lua ha oggi.

| Passaggio | Stato | Cosa | Perché viene qui |
| --- | --- | --- | --- |
| 0.1 | ✅ | **Stato dei mobile in Lua**: statistiche, abilità, punti vita, mana, stamina, flag (nascosto, congelato, modalità guerra), colore, corpo, nome; props sui giocatori; props globali. Il flag morto arriva con la morte (2.3) | Ogni regola li legge o modifica |
| 0.2 | ✅ | **Interrogazioni del mondo**: mobile e oggetti vicini a un punto, in vista; giocatori online; ricerche di regione, altezza e linea di vista | Ogni IA, incantesimo ed effetto di area ne ha bisogno |
| 0.3 | ✅ | **API degli oggetti**: creazione (a terra, in uno zaino, in un contenitore), spostamento in un contenitore, equipaggiamento, elenco del contenuto, ricerca per tipo, impostazione di colore e nome | Bottino, creazione, venditori, missioni |
| 0.4 | ✅ | **Input e output del giocatore**: messaggio di sistema, testo sopra qualsiasi oggetto, messaggi cliloc, cursore bersaglio, richiesta di testo | Ogni abilità e incantesimo inizia con un bersaglio |
| 0.5 | 🟡 | **Eventi che possono rifiutare**: un handler arresta o modifica l'azione predefinita. Completati per gli oggetti: `on_use`, `can_pick_up`, `can_drop`, `can_equip`, `can_insert`. Restano: `check_skill`, `on_damage`, che arrivano con abilità e combattimento | Permette a uno script di gestire una regola |
| 0.6 | 🟡 | **Timer mantenuti dall'oggetto** e salvati con il mondo. Completati per gli oggetti (`item.start_timer`, `on_timer`; le porte si chiudono con esso). Restano: timer sui mobile, che arrivano con gli effetti temporanei (1.3) | Gli effetti temporanei richiedono lo stesso meccanismo |
| 0.7 | 🟡 | **Eventi delle regioni**: ingresso e uscita. Completati per i giocatori (`player_region_changed`); non per gli NPC | Guardie, regole della magia, script musicali, missioni |

## Fase 1: un personaggio che vive

| Passaggio | Stato | Cosa | Perché viene qui | Dati pronti da importare |
| --- | --- | --- | --- | --- |
| 1.1 | ✅ | **Rigenerazione** di punti vita, mana e stamina; fame e sete, cibo che si mangia e bevande che si bevono | Nulla dipende da altro; visibile nel primo combattimento | |
| 1.2 | ✅ | **Uso, verifica e incremento delle abilità**; incremento delle statistiche; limiti e blocchi. Un'[abilità](skills.md) viene usata dal client, verificata da `skill.check`, incrementata entro il limite dell'abilità e quello totale e impostata in aumento, diminuzione o bloccata dalla finestra delle abilità; una verifica riuscita aumenta forza, destrezza e intelligenza secondo la regola classica di ModernUO, fino a 100 ciascuna e 225 complessivi, con i loro blocchi. Hiding è il primo script di abilità; gli altri arrivano con i rispettivi sistemi | La progressione del gioco; ogni sistema successivo chiama la verifica delle abilità | UOX3 `skills.dfn` (61 abilità: pesi delle statistiche, curve di incremento), ModernUO `skills.json` |
| 1.3 | ❌ | **Effetti temporanei**: un meccanismo per veleno, maledizioni, benedizioni, polymorph, nascondersi; la barra dei buff li mostra | Magia, pozioni e combattimento ne hanno bisogno | |
| 1.4 | ✅ | **Contenitori a terra**, con limiti di oggetti e peso; peso e sovraccarico: un contenitore a terra si apre, gli oggetti entrano ed escono, fino a 125 e al suo limite di stone; un giocatore trasporta 40 stone e 3.5 per punto di forza, e muoversi sovraccarichi o correre costa stamina | Cadaveri, venditori, forzieri e case ne hanno bisogno | |
| 1.5 | 🟡 | **Menu contestuali e menu classici**. Completato: richiesta di testo (`prompt.ask`) | Venditori, animali, creazione e gilde si aprono tramite essi | |
| 1.6 | ✅ | **Campi di combattimento degli oggetti** nei template e nel convertitore: danno, velocità, armatura, punti vita, requisito di forza | Il combattimento li legge; ora il convertitore li legge da UOX3 | UOX3 `items/gear/` |

## Fase 2: combattimento

| Passaggio | Stato | Cosa | Perché viene qui |
| --- | --- | --- | --- |
| 2.1 | 🟡 | **Modalità guerra, timer dei colpi, corpo a corpo e tiro con l'arco**: probabilità di colpire, danno, armatura, parata, durabilità. Completato: un [combattimento](combat.md) a pugni o con un'arma, timer dei colpi, colpo tramite abilità, danno e armatura di un NPC o di ciò che un giocatore indossa. Restano: parata, tiro con l'arco, durabilità | Il ciclo fondamentale del gioco |
| 2.2 | ❌ | **Elenchi degli aggressori** | Notorietà, guardie e diritti sul bottino si basano su di essi |
| 2.3 | 🟡 | **Morte, cadavere, fantasma, resurrezione**; NPC guaritori e santuari. Completato: un NPC o un giocatore muore in combattimento, con `.kill` o `mobile.kill` e lascia il proprio [cadavere](death.md) con ciò che trasportava; un giocatore resta come [fantasma](death.md#death-of-a-player) e torna in vita a un ankh o da un guaritore, con `.resurrect` o `mobile.resurrect`. Restano: ossa, taglie e le postazioni dei guaritori malvagi | Dà un risultato al combattimento |
| 2.4 | ❌ | **Bende e guarigione** | Necessarie appena esiste il danno |
| 2.5 | ❌ | **Eventi di combattimento per Lua**: attacco, colpo riuscito, colpo mancato, danno, morte, resurrezione | Permette ai contenuti di modificare le regole |
| 2.6 | ❌ | **Impostazioni del combattimento**: velocità dei colpi, regole del danno, decadimento dei cadaveri | Un proprietario di shard si aspetta di regolarle |

## Fase 3: un mondo che risponde agli attacchi

| Passaggio | Stato | Cosa | Perché viene qui | Dati pronti da importare |
| --- | --- | --- | --- | --- |
| 3.1 | 🟡 | **Ricerca del percorso** e movimento che controlla oggetti e mobile. Completato: ricerca A*, `npc.walk_to` e oggetti che bloccano; i mobile non bloccano ancora | L'IA non può inseguire senza questo | |
| 3.2 | 🟡 | **IA degli NPC**: corpo a corpo, arciere, mago, animale, fuga; memoria degli NPC di chi ha attaccato. Oggi uno script Lua a ogni tick, movimento casuale e `monster.lua`, l'IA corpo a corpo senza combattimento, sui non morti dei cimiteri | I 29.000 NPC generati diventano contenuti | Tag NPC UOX3 oggi scartati (`NPCAI`, `FLEEAT`, `SPATTACK`), ModernUO `npc-speeds.json` |
| 3.3 | 🟡 | **Bottino sui cadaveri**, smembramento, incremento di fama e karma. Il bottino viene generato nello zaino allo spawn e si trova nel cadavere di un NPC morto; nessuno smembramento, fama o karma | Ricompensa del combattimento | UOX3 `carve.dfn` (102 tabelle) |
| 3.4 | 🟡 | **Notorietà**: flag criminale e assassino, colori dei nomi, conteggi degli omicidi. Oggi il colore del nome dal template mobile, assegnato a ogni NPC alla creazione | Dà regole al PvP | |
| 3.5 | ❌ | **Regole delle regioni e guardie**: città sorvegliate, no recall, no gate, no case | Rende sicure le città | ModernUO `regions.json` (regioni tipizzate), UOX3 `regions.dfn` (179 insiemi di regole) |

## Fase 4: economia e magia

I venditori richiedono solo la fase 1, quindi possono essere realizzati in parallelo con le fasi 2 e 3.

| Passaggio | Stato | Cosa | Perché viene qui | Dati pronti da importare |
| --- | --- | --- | --- | --- |
| 4.1 | 🟡 | **Venditori**: acquisto, vendita, rifornimento; istruttori delle abilità. Completato: cassetta di banca, saldo, deposito e prelievo tramite parlato, assegni bancari, oro consegnato al banchiere | Dà un uso all'oro | UOX3 `shoplist.dfn` (38 elenchi usati da 86 NPC); i prezzi sono già convertiti |
| 4.2 | ❌ | **Scambio sicuro** tra giocatori | Economia dei giocatori | |
| 4.3 | ❌ | **Lancio degli incantesimi e Magery**: libri degli incantesimi, reagenti, pergamene, parole di potere, i 64 incantesimi | Metà dei personaggi lancia incantesimi | UOX3 `spells.dfn` (mana, reagenti, ritardo, mantra) |
| 4.4 | ❌ | **Recall, mark, gate, runebook** | Il modo in cui i giocatori viaggiano; richiede le regole delle regioni di 3.5 | |
| 4.5 | ❌ | **Pozioni ed effetti alchemici** | Piccolo quando esistono effetti temporanei e incantesimi | |

## Fase 5: professioni

| Passaggio | Stato | Cosa | Perché viene qui | Dati pronti da importare |
| --- | --- | --- | --- | --- |
| 5.1 | ❌ | **Raccolta**: estrazione mineraria, taglio della legna, pesca, con regioni di risorse che si esauriscono e rigenerano | Alimenta la creazione | |
| 5.2 | ❌ | **Motore di creazione**, poi ogni mestiere come dati; riparazione | Lo stile di gioco pacifico e l'economia dei giocatori | UOX3 `create/` (618 ricette) |
| 5.3 | ❌ | **Addomesticamento, comandi degli animali, stalle, cavalcature** | Richiede IA (3.2), notorietà (3.4) e venditori (4.1) | Tag UOX3 `TOTAME`, `CONTROLSLOTS`, `FOOD` |
| 5.4 | ❌ | **Le abilità attive rimanenti**: nascondersi, furtività, furto, frugare, abilità di conoscenza, abilità del bardo, inseguimento | Ciascuna è piccola quando esiste 1.2 | |

## Fase 6: giocare insieme

| Passaggio | Stato | Cosa | Perché viene qui |
| --- | --- | --- | --- |
| 6.1 | ❌ | **Gruppo** | Non richiede altro; un piccolo shard vive del gioco di gruppo |
| 6.2 | ❌ | **Gilde**, con colori di guerra e alleanza | Richiede notorietà (3.4) |
| 6.3 | 🟡 | **Chat, bacheche, libri, profilo** | Indipendente, piccolo. Le [bacheche](bulletin-boards.md) sono completate |

## Fase 7: case e barche

La catena di prerequisiti più lunga, e ciò che trattiene i giocatori per mesi.

| Passaggio | Stato | Cosa | Perché viene qui |
| --- | --- | --- | --- |
| 7.1 | ❌ | **Multi nel movimento e nella linea di vista** | Tutto ciò che segue si basa su questo |
| 7.2 | ❌ | **Posizionamento delle case, insegna, proprietari, amici, ban** | |
| 7.3 | ❌ | **Blocco degli oggetti, contenitori sicuri, decadimento** | |
| 7.4 | ❌ | **Addon** (forge, telai, mobili composti) | |
| 7.5 | ❌ | **Venditori dei giocatori** | Richiede case e venditori |
| 7.6 | ❌ | **Barche** | Richiede multi e parole chiave del parlato |

## Gestire uno shard

Queste voci non dipendono dalle fasi di gioco e vengono completate quando un operatore ne ha bisogno.

- Ban degli account, limiti IP, limiti ai tentativi di login, limitazione dei pacchetti.
- Strumenti dello staff: gump delle props, menu di aggiunta, comandi di area. I luoghi nominati di
  [`.go`](commands/go.md) e il loro gump sono completati.
- Coda di richieste ai GM, menu di aiuto e per personaggi bloccati. La [prigione](jail.md) è completata.
- Comandi scritti in Lua.

## In seguito

Dopo la fase 7. Ciascuna di queste voci richiede gran parte di ciò che precede.

- **Missioni**: un motore per missioni e scorte; circa 35.000 righe in ModernUO.
- **Spawn dei campioni, mappe del tesoro, accampamenti.** I forzieri dei dungeon ricompaiono e i contenitori cittadini
  si riempiono già; le loro serrature e trappole arrivano con le priorità 2 e 4.
- **Virtù e fazioni.**
- **Le regole moderne**: proprietà e resistenze degli oggetti, bottino magico casuale, mosse speciali,
  Necromancy, Chivalry, Bushido, Ninjitsu, Spellweaving, Mysticism, ordini di produzione in massa, progettazione
  personalizzata delle case. Richiedono pacchetti che un server classico non invia mai (stato esteso, numeri del danno,
  progettista delle case).
- **Contenuti delle espansioni successive**: imbuing, maestrie, navi High Seas, ricerca dei venditori, negozio.

## Non pianificato

- Esperienza e livelli, classi di abilità: solo Sphere li possiede.
- Tornei e classifiche, eventi stagionali, ricompense dei veterani: contenuti per uno shard attivo, meglio realizzati
  dal proprietario in Lua.

## Dati che possiamo importare

Moongate importa già oggetti, NPC e spawn UOX3, e decorazione, spawn, luoghi nominati e
teletrasporti ModernUO. Gli stessi convertitori possono portare gran parte dei dati delle regole necessari alle fasi sopra:

| Dati | Sorgente | Fase |
| --- | --- | --- |
| Definizioni delle abilità: pesi delle statistiche, curve di incremento, titoli | UOX3 `skills.dfn`, ModernUO `skills.json` | 1.2 |
| Campi di armi e armature | UOX3 `items/gear/` | 1.6 |
| Tag del comportamento e velocità degli NPC | File NPC UOX3, ModernUO `npc-speeds.json` | 3.2 |
| Tabelle di smembramento | UOX3 `carve.dfn` | 3.3 |
| Regole delle regioni: sorveglianza, recall, gate, case | ModernUO `regions.json`, UOX3 `regions.dfn` | 3.5 |
| Elenchi dei negozi | UOX3 `shoplist.dfn` | 4.1 |
| Incantesimi | UOX3 `spells.dfn` | 4.3 |
| Ricette di creazione | UOX3 `create/` | 5.2 |

ModernUO e ServUO mantengono elenchi dei venditori, pacchetti di bottino e definizioni di creazione come classi C#, non dati;
per questi la sorgente è UOX3.

## Le impostazioni arrivano con ogni sistema

Sphere ha circa 230 impostazioni e UOX3 circa 370; Moongate ne ha circa 60, quasi tutte per l'
infrastruttura. Ogni sistema sopra arriva con le proprie impostazioni (ritmi di rigenerazione, timer
criminali, rifornimento dei venditori, tempi di decadimento), documentate in [Configurazione](server-configuration.md).
