<!-- translation: {"sourceHash":"741cc862449bb3f8f0ff26297da44fa82263c73c61400790216a7ba929464bf0","title":"Pacchetti e handler"} -->

# Pacchetti e handler

Consulta il [riferimento interattivo dei pacchetti](/packets/) per ogni pacchetto registrato, la sua struttura
sul protocollo, direzione, dimensione, riepilogo dell'handler e file sorgente.

`Moongate.Network.Packets` definisce i formati del protocollo indipendentemente da TCP e dal server
di gioco. `PacketRegistry` descrive i frame e decodifica i pacchetti in ingresso;
`IPacketHandler<TPacket>` fornisce il comportamento di gioco sincrono, mentre
`IAsyncPacketHandler<TPacket>` gestisce i pacchetti che richiedono I/O. Sono registrazioni
separate. I formati integrati iniziali sono destinati a **ClassicUO 7.x**.

## Copertura dei pacchetti integrati

Le lunghezze includono l'opcode e, per i pacchetti variabili, l'intestazione della lunghezza.
Le direzioni sono relative al server. Questa è la tabella predefinita, non l'intero protocollo UO:

| Opcode | Classe | Direzione | Lunghezza | Handler predefinito dell'host |
| --- | --- | --- | --- | --- |
| `0x53` | `PopupMessagePacket` | In uscita | Fissa 2 | Game: inviato prima della disconnessione quando la creazione del personaggio viene rifiutata o fallisce |
| `0x55` | `LoginCompletePacket` | In uscita | Fissa 1 | — |
| `0x73` | `PingPacket` | Entrambe | Fissa 2 | Game: `PingPacketHandler`; Login: `LoginRolePingPacketHandler` |
| `0x80` | `AccountLoginPacket` | In ingresso | Fissa 62 | Login: verifica asincrona dell'account, poi elenco `0xA8` oppure rifiuto `0x82` |
| `0x82` | `LoginDeniedPacket` | In uscita | Fissa 2 | — |
| `0x8C` | `ServerRedirectPacket` | In uscita | Fissa 11 | Login: inviato dopo un `0xA0` valido, prima di chiudere la connessione di accesso |
| `0x91` | `GameLoginPacket` | In ingresso | Fissa 65 | Game: valida e consuma il ticket monouso di passaggio, poi invia `0xB9` e `0xA9` |
| `0xA0` | `ServerSelectPacket` | In ingresso | Fissa 3 | Login: verifica l'idoneità al realm, emette il ticket e reindirizza |
| `0xA8` | `ServerListPacket` | In uscita | Variabile, minimo 6 | — |
| `0xB9` | `SupportFeaturesPacket` | In uscita | Fissa 5 | Game: inviato dopo un `0x91` valido |
| `0xBD` | `ClientVersionPacket` | In ingresso | Variabile, minimo 4 | `ClientVersionPacketHandler`: registra la versione del client nella sessione |
| `0xBD` | `ClientVersionRequestPacket` | In uscita | Fissa 3 | — |
| `0xEF` | `LoginSeedPacket` | In ingresso | Fissa 21 | `LoginSeedPacketHandler`; sui listener di accesso registra anche la versione del client, che il ticket di passaggio porta alla sessione di gioco |

Il plugin Ultima aggiunge questi pacchetti nelle modalità game e standalone, con
`RegisterIncomingPacket` per quelli in ingresso (vedi
[Integrazione con l'host](#host-integration)). `0xD9` è l'eccezione: è registrato in ogni
modalità, perché l'Enhanced Client lo invia anche al server di accesso:

| Opcode | Classe | Direzione | Lunghezza | Handler |
| --- | --- | --- | --- | --- |
| `0x8D` | `CreateCharacterEnhancedPacket` | In ingresso | Variabile | `CreateCharacterEnhancedPacketHandler`: crea e salva il personaggio e gli oggetti iniziali, poi lo porta nel mondo |
| `0xA9` | `CharacterListPacket` | In uscita | Variabile, minimo 6 | — |
| `0xD9` | `ClientHardwareInfoPacket` | In ingresso | Fissa 268 | Game: `IgnoredPacketHandler<T>`; Login: `LoginRoleIgnoredPacketHandler<T>`. Riconosciuto e ignorato per ora (log Debug) |
| `0xF8` | `CreateCharacterPacket` | In ingresso | Fissa 106 | `CreateCharacterPacketHandler`: crea e salva il personaggio e gli oggetti iniziali, poi lo porta nel mondo |
| `0x5D` | `PlayCharacterPacket` | In ingresso | Fissa 73 | `PlayCharacterPacketHandler`: porta il personaggio scelto nel mondo |
| `0x83` | `DeleteCharacterPacket` | In ingresso | Fissa 39 | `DeleteCharacterPacketHandler`: contrassegna il personaggio per l'eliminazione |
| `0x02` | `MoveRequestPacket` | In ingresso | Fissa 7 | `MoveRequestPacketHandler`: gira o fa compiere un passo al personaggio, con risposta `0x22` o `0x21` |
| `0x22` | `MovementAckPacket` | In uscita | Fissa 3 | — |
| `0x21` | `MovementRejectPacket` | In uscita | Fissa 8 | — |
| `0x77` | `MobileMovingPacket` | In uscita | Fissa 17 | — |
| `0x06` | `UseRequestPacket` | In ingresso | Fissa 5 | `UseRequestPacketHandler`: apre un contenitore trasportato dal personaggio oppure un paperdoll |
| `0x03` | `AsciiSpeechRequestPacket` | In ingresso | Variabile, minimo 9 | `SpeechRequestPacketHandler`: parlato locale o comando con punto in gioco |
| `0xAD` | `UnicodeSpeechRequestPacket` | In ingresso | Variabile, minimo 14 | `SpeechRequestPacketHandler`: parlato Unicode e con parole chiave codificate oppure comando con punto |
| `0xAE` | `UnicodeSpeechMessagePacket` | In uscita | Variabile, minimo 50 | Parlato del giocatore e output privato dei comandi |
| `0x24` | `DisplayContainerPacket` | In uscita | 7, oppure 9 dal client 7.0.9.0 (senza intestazione di lunghezza) | — |
| `0x3C` | `ContainerContentPacket` | In uscita | Variabile, minimo 5 | — |
| `0x09` | `LookRequestPacket` | In ingresso | Fissa 5 | `LookRequestPacketHandler`: mostra il nome sopra l'oggetto (`0xC1`) |
| `0x34` | `MobileQueryPacket` | In ingresso | Fissa 10 | `MobileQueryPacketHandler`: risponde con lo stato del personaggio o di un mobile visibile (`0x11`) e le abilità del personaggio (`0x3A`) |
| `0x72` | `WarModeRequestPacket` | In ingresso | Fissa 5 | `WarModeRequestPacketHandler`: mette il personaggio in modalità guerra o pace, risponde con `0x72` e mostra la postura ai giocatori vicini (`0x77`) |
| `0xC8` | `UpdateRangePacket` | In ingresso | Fissa 2 | `UpdateRangePacketHandler`: risponde con il raggio visivo del server |
| `0xC8` | `ViewRangePacket` | In uscita | Fissa 2 | — |
| `0x88` | `DisplayPaperdollPacket` | In uscita | Fissa 66 | — |
| `0x07` | `LiftRequestPacket` | In ingresso | Fissa 7 | `LiftRequestPacketHandler`: raccoglie un oggetto trasportato o indossato dal personaggio, oppure uno a terra entro 2 caselle |
| `0x08` | `DropRequestPacket` | In ingresso | Fissa 15 | `DropRequestPacketHandler`: rilascia l'oggetto tenuto in un contenitore trasportato oppure a terra |
| `0x25` | `ContainerItemUpdatePacket` | In uscita | 20, oppure 21 dal client 6.0.1.7 (senza intestazione di lunghezza) | — |
| `0x27` | `LiftRejectPacket` | In uscita | Fissa 2 | — |
| `0x1D` | `RemoveEntityPacket` | In uscita | Fissa 5 | — |
| `0x1A` | `WorldItemPacket` | In uscita | Variabile, minimo 16 | — |
| `0xF3` | `WorldItemSaPacket` | In uscita | 24, oppure 26 dal client 7.0.9.0 | — |
| `0x13` | `EquipRequestPacket` | In ingresso | Fissa 10 | `EquipRequestPacketHandler`: mette l'oggetto tenuto sul personaggio oppure lo respinge |
| `0x2E` | `WornItemPacket` | In uscita | Fissa 15 | — |
| `0x6E` | `MobileAnimationPacket` | In uscita | Fissa 14 | — |
| `0xAF` | `DeathAnimationPacket` | In uscita | Fissa 13 | — |
| `0x2C` | `DeathStatusPacket` | In uscita | Fissa 2 | — |
| `0x89` | `CorpseEquipmentPacket` | In uscita | Variabile | — |
| `0x74` | `VendorBuyListPacket` | In uscita | Variabile | I prezzi e i nomi della finestra del negozio di un venditore |
| `0x3B` | `VendorEndPacket` | In uscita | Fissa 8 | Chiude la finestra del negozio |
| `0x3B` | `VendorBuyReplyPacket` | In entrata | Variabile, minimo 8 | `VendorBuyReplyPacketHandler`: l'acquisto della finestra del negozio |
| `0x9E` | `VendorSellListPacket` | In uscita | Variabile | Cosa offre di comprare un venditore dal giocatore |
| `0x9F` | `VendorSellReplyPacket` | In entrata | Variabile, minimo 9 | `VendorSellReplyPacketHandler`: la vendita dell'elenco |
| `0x6C` | `TargetCursorPacket` | In uscita | Fissa 19 | — |
| `0x6C` | `TargetResponsePacket` | In ingresso | Fissa 19 | `TargetResponsePacketHandler`: completa il bersaglio in attesa del giocatore |
| `0x95` | `HuePickerPacket` | In uscita | Fissa 9 | — |
| `0x95` | `HuePickerResponsePacket` | In ingresso | Fissa 9 | `HuePickerResponsePacketHandler`: fornisce la tinta scelta al selettore aperto del giocatore |
| `0x05` | `AttackRequestPacket` | In ingresso | Fissa 5 | `AttackRequestPacketHandler`: il personaggio [combatte](combat.md) il mobile; una richiesta rifiutata riceve `0xAA` e zero |
| `0x22`, `0x9B`, `0xB5`, `0xFB` | `ResynchronizeRequestPacket`, `HelpRequestPacket`, `OpenChatWindowPacket`, `PublicHouseContentPacket` | In ingresso | Fissa 3, 258, 64, 2 | `IgnoredPacketHandler<T>`: riconosciuto e ignorato per ora (log Debug) |
| `0xAA` | `CombatantPacket` | In uscita | Fissa 5 | — |
| `0x2F` | `SwingPacket` | In uscita | Fissa 10 | — |
| `0x0B` | `DamagePacket` | In uscita | Fissa 7 | — |
| `0x12` | `TextCommandPacket` | In ingresso | Variabile | `TextCommandPacketHandler`: il tipo `0x24` [usa l'abilità](skills.md) il cui numero inizia il testo; gli altri tipi vengono ignorati per ora (log Debug) |
| `0xB8`, `0xE1`, `0xF0` | `ProfileRequestPacket`, `ClientTypePacket`, `ProtocolExtensionPacket` | In ingresso | Variabile | `IgnoredPacketHandler<T>`: riconosciuto e ignorato per ora (log Debug) |
| `0xBF` | `ExtendedCommandPacket` | In ingresso | Variabile | `ExtendedCommandPacketHandler`: il sottocomando `0x10` risponde a un tooltip, `0x1A` imposta il blocco di una statistica; gli altri vengono ignorati per ora |
| `0xD6` | `QueryPropertiesPacket` | In ingresso | Variabile, al massimo 500 seriali | `QueryPropertiesPacketHandler`: un `0xD6` per oggetto visto dal personaggio |
| `0xD6` | `PropertyListPacket` | In uscita | Variabile | — |
| `0xDC` | `PropertyListInfoPacket` | In uscita | Fissa 9 | — |
| `0xC1` | `LocalizedMessagePacket` | In uscita | Variabile | — |
| `0xC2` | `TextPromptPacket` | In uscita | Variabile (21) | — |
| `0xC2` | `TextPromptResponsePacket` | In ingresso | Variabile | `TextPromptResponsePacketHandler`: completa la richiesta di testo in attesa del giocatore |
| `0x54` | `PlaySoundPacket` | In uscita | Fissa 12 | — |
| `0xC0` | `HuedEffectPacket` | In uscita | Fissa 36 | — |
| `0xC7` | `ParticleEffectPacket` | In uscita | Fissa 49 | — |
| `0x1B` | `LoginConfirmPacket` | In uscita | Fissa 37 | — |
| `0xBF` | `MapChangePacket` | In uscita | Variabile, 6 (sottocomando `0x08`) | — |
| `0xBF` | `StatLockInfoPacket` | In uscita | Variabile, 12 (sottocomando `0x19`) | — |
| `0xBC` | `SeasonChangePacket` | In uscita | Fissa 3 | — |
| `0x4F` | `GlobalLightLevelPacket` | In uscita | Fissa 2 | — |
| `0x4E` | `PersonalLightLevelPacket` | In uscita | Fissa 6 | — |
| `0x20` | `MobileUpdatePacket` | In uscita | Fissa 19 | — |
| `0x78` | `MobileIncomingPacket` | In uscita | Variabile, minimo 23 | — |
| `0x11` | `MobileStatusPacket` | In uscita | Variabile, 91 (versione 5), oppure 43 (versione 0) per un altro mobile | — |
| `0xA1`, `0xA2`, `0xA3` | `MobileHitsPacket`, `MobileManaPacket`, `MobileStaminaPacket` | In uscita | Fissa 9 | Inviato da `MobileStateService`; i punti vita arrivano ai giocatori vicini come proporzione su 100 |
| `0x3A` | `SkillsPacket` | In uscita | Variabile, minimo 6 | L'intero elenco delle abilità oppure un'abilità cambiata |
| `0x3A` | `SkillLockPacket` | In ingresso | Variabile, minimo 6 | `SkillLockPacketHandler`: imposta il blocco di un'abilità del personaggio (aumenta, diminuisce o bloccata); un'abilità o un blocco inesistenti vengono ignorati |
| `0x72` | `WarModePacket` | In uscita | Fissa 5 | — |
| `0x5B` | `CurrentTimePacket` | In uscita | Fissa 4 | — |
| `0x65` | `WeatherPacket` | In uscita | Fissa 4 | — |
| `0x6D` | `PlayMusicPacket` | In uscita | Fissa 3 | — |
| `0x86` | `CharacterListUpdatePacket` | In uscita | Variabile, minimo 4 | — |
| `0x85` | `CharacterDeleteResultPacket` | In uscita | Fissa 2 | — |
| `0xB0` | `GumpPacket` | In uscita | Variabile, minimo 23 | — |
| `0xDD` | `CompressedGumpPacket` | In uscita | Variabile, minimo 35 | — |
| `0xB1` | `GumpResponsePacket` | In ingresso | Variabile, minimo 23 | `GumpResponsePacketHandler`: consegna la risposta verificata al gump inviato al giocatore |
| `0xBF` | `CloseGumpPacket` | In uscita | Variabile, 13 (sottocomando `0x04`) | — |

Il parlato raggiunge chi parla e gli altri personaggi giocanti sulla stessa mappa entro
la sua portata, come in ModernUO: 15 caselle a voce normale o come emote, 1 casella per un
sussurro, 18 per un urlo. Il client sceglie il modo da ciò che il giocatore scrive (`; ` sussurra,
`! ` urla, `: ` fa un emote) e il testo esce invariato, con il suo tipo. La chat globale e la
finestra di chat separata non sono ancora supportate. Un `.` iniziale richiama il sistema dei comandi esistente
in privato; `..` permette di scrivere un punto iniziale. Il parlato vuoto o superiore a 128 caratteri viene ignorato.

`0xAD` viene letto in modo tollerante, come negli altri emulatori, perché un pacchetto rifiutato disconnette il
client: un terminatore mancante viene accettato, un carattere codificato male diventa U+FFFD e un
codice lingua che non consiste di tre lettere ASCII viene letto come inglese. Viene rifiutato solo un elenco troncato di
parole chiave.

Un gump viene inviato come `0xDD` (compresso) ai client da 5.0.0a e come `0xB0` ai precedenti;
la risposta torna come `0xB1` e il server chiude un gump con `0xBF` sottocomando `0x04`. Vedi
[Gump](gumps.md).

Un effetto grafico viene inviato come `0xC0` (36 byte): tipo (0 in movimento, 1 fulmine, 2 fisso in un
punto, 3 fisso su un oggetto), i due seriali, la grafica, entrambi i punti, velocità, durata, i
due flag, la tinta e la modalità di rendering. Il semplice `0x70` non viene mai inviato, come in ModernUO. Un
effetto con un id di particella viene inviato all'Enhanced Client come `0xC7` (49 byte), lo stesso corpo
seguito dai campi della particella; ogni altro client riceve `0xC0`, oppure nulla quando l'effetto
non ha grafica. `EffectService` li invia ai giocatori della mappa entro il raggio visivo
dell'effetto e, per un effetto in movimento, anche entro il raggio della destinazione, una volta ciascuno.

Quando un personaggio entra nel mondo il server invia, in quest'ordine (quello di ModernUO, verificato con
ServUO, UOX3, POL e Source-X): `0x1B` conferma di accesso, `0xBF` sottocomando `0x08` mappa, `0xBC`
stagione, `0x4F` e `0x4E` luce, `0x20` il giocatore, `0x78` il giocatore con oggetti indossati, capelli e
barba (seriali virtuali da `IMobileService`), `0x11` stato (versione 5), `0x72` pace, `0x55`
accesso completato e `0x5B` ora; poi viene pubblicato `CharacterEnteredWorldEvent`. Le classi in uscita
risiedono in `Moongate.Server.Ultima.Packets.World`. Il personaggio è quindi una `MobileEntity` attiva
in `IMobileService`, rivolta nella direzione con cui era stata salvata.

Un passo (`0x02`) contiene la direzione, un bit di corsa (`0x80`) e una sequenza. La sequenza è 0
dopo l'accesso o un passo rifiutato, poi da 1 a 255 e ricomincia da 1; ogni altro valore viene rifiutato.
Una direzione verso cui il personaggio non è rivolto lo gira soltanto e non consuma tempo. Un passo nella direzione
verso cui è rivolto prenota il successivo 400 ms dopo camminando o 200 ms dopo correndo, e può arrivare fino a 200 ms
in anticipo; un passo precedente, oppure uno bloccato da `IMovementService`, viene rifiutato. `0x22` accetta il passo con
la notorietà del personaggio; `0x21` lo rifiuta e riporta il client alla posizione reale.

I giocatori si vedono entro il raggio visivo (18 caselle per impostazione predefinita) su entrambi gli assi. `ISectorService` mantiene i mobile attivi in
settori 16×16 per mappa e `IWorldViewService` ricalcola ciò che è cambiato dalla vecchia e dalla nuova
posizione di ogni passo, come fanno ModernUO e POL. Dopo la sequenza di ingresso nel mondo il giocatore riceve
`0x78` di tutti quelli nel raggio e loro ricevono il suo `0x78`. Un passo o una rotazione accettati inviano `0x77` (il bit
`0x80` della direzione indica una corsa) ai giocatori che vedevano già chi si muove, `0x78` in entrambe le direzioni
ai giocatori appena entrati nel raggio e `0x1D` ai giocatori che non lo vedono più; il client di chi si muove
elimina da sé ciò da cui si allontana. Quando un personaggio lascia il mondo i giocatori nel
raggio ricevono il suo `0x1D`. Il raggio è `ultima.world.view_range` (predefinito 18, da 5 a 24); alla richiesta
`0xC8` del client si risponde con esso, qualunque fosse la richiesta, così entrambe le parti usano lo stesso raggio.
`ISectorService.Query` restituisce i giocatori, gli NPC e gli oggetti a terra attorno a un punto, nel
raggio visivo salvo che ne venga indicato un altro. Un settore è attivo mentre un giocatore si trova entro due settori da esso (i settori
5×5 attorno a ogni giocatore, come in ModernUO); gli NPC non attivano i settori. `ISectorService.IsActive` lo indica
per la futura IA degli NPC.

Quando la sessione si chiude, `CharacterLeaveWorldService` (un `ISessionClosedListener`) copia il
personaggio, lo rimuove dal mondo, salva la copia e pubblica `CharacterLeftWorldEvent`. Il
salvataggio del mondo scrive anche i personaggi attivi, quindi un arresto anomalo perde al massimo i passi dall'ultimo salvataggio.

Gli oggetti indossati da un personaggio e tutto ciò che contengono a qualsiasi profondità vengono caricati a `0x5D` e
risiedono in `IItemService` mentre gioca; lasciano il mondo e vengono salvati dopo il personaggio
quando la sua sessione si chiude, e anche il salvataggio del mondo li scrive. Un doppio clic (`0x06`) su un oggetto
attivo lo apre quando la sua grafica ha il flag tiledata Container e il personaggio della sessione
lo trasporta, indossato oppure dentro qualcosa di indossato: `0x24` con il gump da `containers.toml` (la
voce predefinita quando la grafica non ne ha uno), poi `0x3C` con il suo contenuto diretto, anche se vuoto.
I client precedenti a 7.0.9.0 ricevono il `0x24` da 7 byte e quelli precedenti a 6.0.1.7 un `0x3C` senza il byte della griglia;
una versione sconosciuta riceve i formati moderni.

Il byte della griglia di `0x3C` e `0x25` è lo slot dell'oggetto (da 0 a 124) nella griglia con cui l'Enhanced Client
mostra un contenitore; il client classico lo ignora e usa la posizione del gump. Ogni oggetto riceve uno
slot libero quando entra in un contenitore e lo conserva nel database (`grid_index`). Un rilascio (`0x08`)
contiene lo slot richiesto dall'Enhanced Client: l'oggetto lo prende quando è libero, altrimenti prende il
successivo libero. Un contenitore con più di 125 oggetti condivide gli slot.

Un doppio clic su un mobile apre il suo paperdoll (`0x88`) quando il suo corpo è umano in
`data/bodies.toml` (un mostro non ne ha) e si trova sulla mappa del personaggio entro
`ultima.world.view_range` lungo X e Y; il pulsante del paperdoll del personaggio stesso invia il suo seriale
con il bit alto `0x80000000` impostato e si apre senza il controllo del raggio (il corpo deve comunque essere
umano). Un corpo assente da `bodies.toml` non ha paperdoll. Il titolo viene costruito come in ModernUO: il prefisso di fama
e karma di [`titles.toml`](data-files/titles.md), le cui righe da 10.000 di fama indicano `Lord`
o `Lady`, il nome, poi `, <title>` quando il mobile ne ha uno (i template NPC danno titoli come "il
mago"), come "Il Glorioso Lord Aria, il mago"; sia per i giocatori sia per gli NPC umani. I titoli delle abilità non vengono
ancora aggiunti. I flag
indicano se il mobile è in modalità guerra e se chi guarda può togliere oggetti, impostato solo sul
paperdoll del personaggio stesso. Gli oggetti indossati sono già noti al client da `0x78`. Gli altri
doppi clic non sono ancora gestiti.

Raccogliere un oggetto (`0x07`) lo registra come tenuto nella sessione; rimane nel suo contenitore fino
al rilascio. Un oggetto intero dentro un contenitore trasportato dal personaggio, oppure uno indossato dal personaggio
(dal paperdoll; non lo zaino), può essere raccolto: avere già un altro oggetto tenuto
(`AreHolding`), parte di un oggetto non impilabile o di una pila indossata, lo zaino o un oggetto non
trasportato vengono rifiutati con `0x27` (`CannotLift`); un oggetto del personaggio dentro un contenitore viene mostrato nuovamente con `0x25` e uno
indossato viene rimesso sul paperdoll con `0x2E`, come in ModernUO; un oggetto di un altro giocatore non viene mai
mostrato. Un oggetto indossato raccolto rimane sul personaggio fino al rilascio e gli altri giocatori nel
raggio lo vedono rimosso (`0x1D`). Rilasciarlo (`0x08`)
lo inserisce in un contenitore trasportato alla posizione di rilascio, riportata entro i limiti del gump, oppure in un
punto casuale quando viene rilasciato sull'icona del contenitore; rilasciato su un oggetto trasportato che non è un
contenitore, va nel contenitore di quell'oggetto alla posizione di quell'oggetto. I mobile, gli oggetti non
trasportati e un contenitore dentro sé stesso o qualcosa al suo interno respingono l'oggetto. Ogni rilascio libera
la mano e invia `0x25` con la posizione reale dell'oggetto, oppure mostra un oggetto a terra dove si trova.

Sollevare parte di un oggetto impilabile (tiledata `Generic`) lo divide: la parte tenuta conserva il seriale,
perché il client la trascina, e il resto prende un seriale da `IItemSerialPool` (64 seriali riservati
dalla sequenza degli oggetti, riforniti sotto 16), rimane dove si trovava la pila e viene mostrato con `0x25`.
Se non rimangono seriali riservati il sollevamento viene rifiutato con `Inspecific`. Rilasciare l'oggetto tenuto su una
pila trasportata dello stesso tipo (grafica, tinta, template, nome e rarità, entrambe senza proprietà), fino
a 60000, le unisce: la pila cresce (`0x25`),
l'oggetto tenuto viene rimosso dal client (`0x1D`) e dalla memoria e la sua riga viene eliminata dal successivo
salvataggio del mondo nella stessa transazione della pila cresciuta, oppure dal salvataggio all'uscita, che prende in carico le
eliminazioni in sospeso del personaggio e le abbandona se fallisce (il database contiene quindi ancora entrambe
le pile com'erano prima dell'unione). Rilasciare su un contenitore non unisce mai. `0x5D` attende i salvataggi all'uscita
dell'ultima sessione dell'account prima di caricare il personaggio, quindi non legge mai righe più vecchie
di ciò che quella sessione ha salvato. Un oggetto tenuto rilasciato su un paperdoll (`0x13`) viene indossato quando il paperdoll è quello del personaggio e
`IEquipmentService` lo consente: il layer proprio dell'oggetto (quello del template, altrimenti quello dei tiledata per una
grafica indossabile; il layer suggerito dal client viene ignorato, come in ModernUO) deve essere indossabile dal
paperdoll (non zaino, capelli, barba, cavalcatura, negozio o banca), libero e compatibile con entrambe le mani come in
ModernUO e POL: un template con `two_handed_weapon = true` richiede entrambe le mani libere e nient'altro
va in mano mentre uno è indossato; scudi e torce si accompagnano a un'arma a una mano. L'oggetto
lascia il suo contenitore o il terreno e tutti nel raggio, incluso il personaggio, ricevono `0x2E`.
Tutto il resto viene respinto dove l'oggetto si trova ancora: il suo contenitore (`0x25`), il terreno oppure
il personaggio da cui è stato tolto (`0x2E` a tutti nel raggio). Una pila di più di un oggetto non viene
indossata. La mano viene sempre liberata e un rilascio o un equipaggiamento che indica un oggetto diverso da quello tenuto
rimette a posto quello tenuto. Per mantenere soddisfatto l'indice univoco `(mobile_id, layer)` dopo un cambio di
abiti, il salvataggio del mondo e quello all'uscita eliminano prima le righe rimosse e scrivono gli oggetti non indossati
prima di quelli indossati, e un salvataggio all'uscita salta un oggetto lasciato dal personaggio che ora qualcun altro
trasporta o indossa (lo scrive il salvataggio di quel proprietario).

Gli oggetti a terra risiedono nella griglia dei settori insieme ai mobile. Rilasciare l'oggetto tenuto sul
terreno (`0x08` con destinazione `0xFFFFFFFF`) funziona entro 2 caselle dal personaggio, in linea di
vista dai suoi occhi: la Z del client viene ignorata e l'oggetto atterra sulla superficie più alta del
terreno o di uno statico fino a 16 unità sopra i piedi del personaggio, come in ModernUO; gli altri oggetti a terra non vengono
sovrapposti in altezza, quindi due oggetti sulla stessa casella si sovrappongono. Viene mostrato a tutti nel raggio, incluso chi
lo rilascia: `0x1A` per i client precedenti a 7.0.0.0, `0xF3` dopo, due byte più lungo da 7.0.9.0.
Chiunque può raccogliere (`0x07`) un oggetto a terra entro 2 caselle in linea di vista; scompare da ogni schermo
(`0x1D`) mentre è tenuto e un sollevamento parziale lascia il resto a terra con un nuovo seriale. Troppo lontano
o fuori dalla linea di vista viene rifiutato con `OutOfRange` o `OutOfSight` e l'oggetto viene mostrato di nuovo solo a quel
giocatore; un oggetto tenuto da qualcun altro non viene mai mostrato. Un oggetto a terra
 tenuto che viene respinto torna dov'era, e anche uno ancora tenuto quando la sessione si chiude
viene rimesso a posto. Rilasciare su una pila a terra raggiungibile le unisce come nello zaino. I giocatori
che camminano entro il raggio di un oggetto a terra, o entrano nel mondo vicino ad esso, lo ricevono con lo stesso controllo di vecchia e
nuova posizione dei mobile. Gli oggetti a terra e tutto ciò che contengono vengono caricati
all'avvio e salvati dal salvataggio del mondo. Un oggetto a terra decade dopo il tempo del suo template, 60 minuti
salvo diversa impostazione di `decay_minutes`, contati da quando è stato posato a terra e riavviati
ogni volta che viene posato di nuovo; un contenitore decaduto porta con sé il contenuto (vedi
[Template](templates.md)). Un contenitore a terra, o al suo interno, si apre quando è raggiungibile dal
personaggio (`0x24` e `0x3C`), e `0x25` mostra gli oggetti che entrano ed escono a tutti quelli vicini.

`IPromptService` chiede a un giocatore una riga di testo con il prompt Unicode (`0xC2`) e consegna ciò che ha
digitato a un callback sul game loop. Un giocatore ha un solo prompt alla volta: uno nuovo conclude il precedente senza
testo, così come `Cancel` e la chiusura di una sessione. Il client non ha un pacchetto che chiude il proprio prompt, quindi
una risposta tardiva viene ignorata: gli id aumentano per sessione e una risposta con un id diverso non fa nulla. Come in
ModernUO, il tipo 0 è l'annullamento del giocatore e un testo superiore a 128 caratteri viene ignorato; il testo viene letto fino
al terminatore, senza caratteri di controllo, e ripulito dagli spazi ai bordi.

`ITargetService` mostra a un giocatore il cursore del bersaglio (`0x6C`) e consegna la scelta a un callback sul
game loop, oppure a un comando in attesa di `RequestAsync`. Un giocatore ha un solo bersaglio alla volta: uno nuovo
conclude il precedente come sostituito, `Cancel` invia `0x6C` con flag 3 e id 0, e una sessione che si chiude lo conclude
come disconnesso. Gli id dei cursori aumentano per sessione e una risposta con un id diverso viene ignorata.
La risposta viene risolta come in ModernUO: x e y -1 senza seriale sono l'annullamento del giocatore; un
seriale deve essere un oggetto o mobile attivo; il terreno prende l'altezza media della mappa, non quella del client;
uno statico deve esistere davvero e fornisce la propria sommità (metà dell'altezza di un ponte). Raggio e linea di
vista sono lasciati al chiamante. I comandi in gioco vengono eseguiti separatamente dal pacchetto del parlato, quindi un comando
in attesa di un bersaglio non trattiene i pacchetti della sessione; il suo output arriva quando termina.
Un giocatore esegue un comando alla volta: un altro nel frattempo viene rifiutato con "Un comando è già
in esecuzione."
`.where` (game master) stampa ciò che seleziona un bersaglio.

`IHuePickerService` mostra a un giocatore il selettore di tinta del client (`0x95`) con una grafica e consegna
la tinta scelta a un callback sul game loop. Un giocatore ha un solo selettore alla volta: uno nuovo conclude il
precedente senza tinta, così come una sessione che si chiude. Gli id dei selettori aumentano per sessione e non sono il seriale di un
oggetto, come in ModernUO: una risposta con un altro id, o senza un selettore aperto, viene ignorata, quindi un
client non può ricolorare nulla inviando `0x95` autonomamente (UOX3 ricolora qualsiasi seriale indicato dal
pacchetto). La tinta viene mascherata con `0x3FFF` e mantenuta tra 2 e 1001, come il
`ClipDyedHue` di ModernUO. Un client che chiude il selettore non invia nulla, quindi il callback potrebbe non essere mai eseguito: chi
apre un selettore controlla di nuovo, nel callback, ciò che era vero all'apertura.

`IDeathService` uccide un NPC ([Morte e resurrezione](death.md)). Ai giocatori che lo vedono vengono inviati, in
quest'ordine, il cadavere (`0xF3`, oppure `0x1A` a un client vecchio), la morte (`0xAF`: il mobile, il suo
cadavere, quattro byte zero, come in ModernUO) e, quando l'NPC viene rimosso, `0x1D`. Il client riproduce
l'animazione di morte del corpo autonomamente; il server non ne indica una. Un cadavere è la grafica `0x2006` e
comunica al client il proprio corpo al posto della quantità e il modo in cui giace nel byte della luce, come
fa ServUO: l'oggetto stesso mantiene quantità 1 ed entrambi provengono dalle proprietà `corpse.body` e
`corpse.direction`. Dopo il cadavere di un corpo umano, elfo o gargoyle arrivano `0x3C`, con gli oggetti
indossati ancora al suo interno più capelli e barba con seriali virtuali, e `0x89`, che indica il
layer di ciascuno come layer più uno e termina con un byte zero: il client disegna il cadavere vestito. Un corpo del genere
non riceve `0xAF`: gli viene detto di riprodurre la caduta (`0x6E`, azione 21) e il suo cadavere arriva 1,5 secondi
dopo, perché ClassicUO elimina gli oggetti indossati di un mobile che muore tramite `0xAF` e lo mostra cadere
nudo.

Lo stesso opcode può avere definizioni diverse in ciascuna direzione, come `0xBD`.
L'elenco dei realm viene filtrato in base al livello minimo del realm dell'account autenticato.
Contiene l'indirizzo IPv4 di ogni realm ma nessuna porta. `0xA0` seleziona un realm attivo
idoneo e `0x8C` fornisce il suo indirizzo IPv4, la porta e una chiave monouso. Il mittente di accesso
svuota il buffer di `0x8C` prima di chiudere la connessione di accesso. Sulla nuova connessione di gioco
il client invia quella chiave come seed grezzo di quattro byte, seguito da `0x91` con la
stessa chiave, nome utente e password. Il gioco controlla il seed e consuma atomicamente
il ticket Redis. Il ticket scade 30 secondi dopo l'emissione, quindi la
riconnessione al gioco deve completarsi entro quel tempo. Un seed diretto di versione client `0xEF`
funziona ancora sui listener di gioco.

Dopo un `0x91` valido il server di gioco copia la versione del client dal ticket alla
sessione, attiva la compressione Huffman per tutto ciò che invia da quel momento
e invia `0xB9` seguito da `0xA9`: i personaggi salvati dell'account nel
numero configurato di slot (`ultima.characters.max_per_account`, predefinito 7), più le
città iniziali da `data/starting_cities.toml`. Gli handler di creazione salvano un nuovo
personaggio e i suoi oggetti iniziali in un'unica transazione. `0x5D` porta il personaggio scelto
nel mondo (vedi sotto).

`TryGetDescriptor(opCode, out descriptor)` preferisce l'ingresso, poi l'uscita;
`descriptor.PacketType.Name` restituisce il nome della classe. L'overload che accetta
`PacketDirection` seleziona esplicitamente una direzione quando necessario.

`registry.TryDecode(bytes, out packet, out opCode)` imposta sempre `opCode` al
primo byte, anche in caso di errore. Un input vuoto restituisce false con opcode zero; ispeziona
la lunghezza dell'input per distinguere quel caso. La decodifica richiede un **frame completo in
ingresso**, compresa l'intestazione. Non memorizza in un buffer un flusso TCP. I pacchetti solo in uscita
hanno metadati ma nessun parser in ingresso.

## Tooltip

I flag dell'elenco personaggi (`0xA9`) includono AOS (`0x20`), quindi il client usa i tooltip AOS
(elenchi delle proprietà degli oggetti) e li richiede con `0xD6` (un elenco di seriali, al massimo 500; una
lunghezza che non consiste di seriali interi, oppure più seriali, non può essere letta e chiude la connessione, come
qualsiasi pacchetto illeggibile) oppure `0xBF` sottocomando `0x10` (un seriale). Il
server risponde con un `0xD6` per oggetto che il personaggio può vedere: un oggetto che trasporta o indossa, un
oggetto indossato da un mobile o a terra entro `ultima.world.view_range`, oppure un mobile in
quel raggio sulla sua mappa; tutto il resto non riceve nulla. `ITooltipService` costruisce le righe, come
ModernUO e UOX3:

- il nome di un oggetto: il cliloc del client per la sua grafica (1020000 + grafica, 1078872 + grafica
  da `0x4000`), che il client mostra nella propria lingua, oppure il nome dell'oggetto o del template
  come testo; un oggetto senza nome proprio che ha la proprietà `label_number` mostra invece quel
  cliloc (un'insegna); una pila usa 1050039 con la quantità;
- nella lingua del server, dai file dei messaggi (`ILocalizationService`, come UOX3):
  blessed o newbie (9055 "[Benedetto]") oppure cursed (30005), il tipo di bottino dell'oggetto altrimenti quello del
  template; il peso dell'intera pila (30006 / 30007); la rarità (30000–30004),
  colorata (comune bianco). Un testo assente dai file usa l'inglese come fallback;
- un mobile: 1050045 con nome e titolo.

I nomi (cliloc degli oggetti, quantità, nomi e titoli dei mobile) rimangono quelli del client finché il server
non ha nomi tradotti; ogni altra riga è del server, quindi i tooltip seguono la lingua del
server.

Il testo libero passa attraverso i cliloc il cui testo completo è `~1_NOTHING~` (1042971, 1070722, ...),
uno per riga; un argomento viene tagliato a 504 caratteri, limite che i client più vecchi non possono superare. La
revisione del tooltip è un hash di 26 bit delle sue righe, come in ModernUO: `0xD6` lo contiene e `0xDC`
lo contiene con il bit 30 impostato. Come in ModernUO, ciò che viene mostrato è seguito dal suo `0xDC`: un
mobile che entra in vista (`0x78`) con ciascuno dei suoi oggetti indossati, un oggetto a terra (`0x1A`/`0xF3`), un
oggetto indossato (`0x2E`), ogni oggetto di un contenitore aperto (`0x3C`) e un oggetto di contenitore aggiornato
(`0x25`) dopo una divisione, un'unione, un posizionamento o un rifiuto; il client richiede di nuovo quando una revisione
cambia. Il `0x78` del personaggio stesso all'ingresso nel mondo non è ancora seguito: il client richiede
 i tooltip che non possiede quando il cursore vi passa sopra. Un tooltip dipende solo da pochi campi del suo
oggetto (per un oggetto: template, grafica, quantità, nome, rarità, tipo di bottino, spostabile, `label_number`; per un mobile:
nome e titolo), quindi viene memorizzato in cache in base ad essi: oggetti uguali condividono un tooltip e una modifica dà
un'altra chiave, quindi nulla viene mai invalidato. La cache mantiene fino a 10000 tooltip e ricomincia
da capo quando è piena. Un singolo clic (`0x09`) mostra la prima riga, il nome, sopra
l'oggetto con `0xC1`.

## Definire un pacchetto e verificarne i byte

Questo esempio usa un opcode privato illustrativo. Non è un'aggiunta al
protocollo ClassicUO; usalo solo con un client di test corrispondente. Aggiungi un riferimento a
`Moongate.Network.Packets` e inserisci questo in `ExamplePacket.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

[PacketHandler(0xFE, PacketSizing.Fixed, Length = 3)]
public sealed class ExamplePacket : BaseFixedPacket<ExamplePacket>,
    IIncomingPacket<ExamplePacket>, IOutgoingPacket
{
    public ushort Value { get; }

    public ExamplePacket(ushort value)
    {
        Value = value;
    }

    public static bool TryParse(ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out ExamplePacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
        {
            return false;
        }
        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt16BigEndian(out var value))
        {
            return false;
        }
        packet = new ExamplePacket(value);
        return true;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(Value);
    }
}
```

L'attributo dei metadati si chiama `PacketHandler`, ma descrive il **pacchetto sul protocollo**;
non registra la classe dell'handler di gioco. Chiama `RegisterIncoming<T>()` per un
pacchetto che implementa `IIncomingPacket<T>` (un pacchetto bidirezionale copre entrambe
le direzioni in una chiamata), oppure `RegisterOutgoing<T>()` per uno che implementa solo
`IOutgoingPacket`. Tipi duplicati o coppie opcode/direzione in conflitto fanno fallire
la registrazione. Blocca la tabella solo dopo averla composta.

Esegui questo `Program.cs` come smoke test a livello di byte senza server né client:

```csharp
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;

var registry = new PacketRegistry();
PacketTable.Register(registry); // Optional: include the built-in formats.
registry.RegisterIncoming<ExamplePacket>();
registry.Freeze();

var bytes = PacketCodec.Encode(new ExamplePacket(0x1234));
if (!bytes.AsSpan().SequenceEqual(new byte[] { 0xFE, 0x12, 0x34 }) ||
    !registry.TryDecode(bytes, out var packet, out var opCode) ||
    packet is not ExamplePacket { Value: 0x1234 } || opCode != 0xFE)
{
    throw new InvalidOperationException("Packet round trip failed");
}
if (registry.TryDecode(new byte[] { 0xFE }, out _, out var failedOpCode) || failedOpCode != 0xFE)
{
    throw new InvalidOperationException("Malformed-frame opcode was lost");
}
Console.WriteLine("Packet bytes and failure opcode verified");
```

Per i pacchetti variabili, usa `BasePacket<T>` con
`[PacketHandler(opcode, PacketSizing.Variable, MinimumLength = ...)]`, valida
l'intero frame dichiarato e scrivi la sua intestazione di lunghezza completa. Vedi i pacchetti integrati
`ClientVersionPacket` e `ServerListPacket` per esempi di parsing e scrittura.

## Registrare un handler di gioco

Aggiungi un riferimento a `Moongate.Server.Core`. `ExamplePacketHandler.cs` può reinviare il
pacchetto tramite il mittente con capacità limitata senza attendere l'I/O del socket:

```csharp
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

public sealed class ExamplePacketHandler : IPacketHandler<ExamplePacket>
{
    private readonly IPacketSendService _sender;

    public ExamplePacketHandler(IPacketSendService sender)
    {
        _sender = sender;
    }

    public void Handle(GameSession session, ExamplePacket packet)
    {
        if (!_sender.TrySend(session.SessionId, new ExamplePacket(packet.Value)))
        {
            // The sender owns and observes connection cleanup.
            _ = _sender.DisconnectAsync(session.SessionId);
        }
    }
}
```

Nella composizione dei servizi di `Program.cs`, oppure nel callback `Register` di un plugin, importa
`Moongate.Server.Core.Extensions` e chiama:

```csharp
container.RegisterPacketHandler<ExamplePacket, ExamplePacketHandler>();
```

Gli handler sono singleton. Mantieni lo stato per giocatore nella sessione/nel mondo, non in campi mutabili
dell'handler. `Handle` viene eseguito sul game loop; mantienilo breve e sincrono.
`TrySend` acquisisce una copia dei byte codificati e restituisce lo stato di ammissione, non una conferma
 di consegna. Decidi cosa fare quando restituisce false; l'esempio disconnette.

Per un handler che attende I/O di database o rete, implementa
`IAsyncPacketHandler<TPacket>` e registralo con
`RegisterAsyncPacketHandler<TPacket, THandler>()`. Il suo `HandleAsync` viene eseguito fuori
dal game loop. L'handler riceve un `PacketContext` invece di una `GameSession`
mutabile. Questo esempio presuppone un servizio di ricerca specifico dell'applicazione:

```csharp
// Define this service in your plugin; keep its interface in a separate file.
public interface IExampleLookup
{
    Task<ushort?> LoadAsync(ushort value, CancellationToken cancellationToken);
}

public sealed class ExampleAsyncPacketHandler : IAsyncPacketHandler<ExamplePacket>
{
    private readonly IExampleLookup _lookup;

    public ExampleAsyncPacketHandler(IExampleLookup lookup)
    {
        _lookup = lookup;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        ExamplePacket packet,
        CancellationToken cancellationToken
    )
    {
        var value = await _lookup.LoadAsync(packet.Value, cancellationToken);

        if (value is not null)
        {
            context.TrySend(new ExamplePacket(value.Value));
        }
    }
}

container.RegisterAsyncPacketHandler<ExamplePacket, ExampleAsyncPacketHandler>();
```

Quando il risultato deve cambiare lo stato di gioco, torna sul loop con
`await context.RunOnGameLoopAsync(session => { /* update session/world */ }, cancellationToken)`.
Restituisce `false` se la sessione originale si è disconnessa prima dell'esecuzione dell'azione.
Un handler asincrono non deve modificare direttamente una sessione dopo un `await`.

Per una sessione può essere in corso un solo pacchetto asincrono. I pacchetti inviati dalla sessione
nel frattempo attendono, fino a 1024 (`PacketDispatchService.MaxPendingPerSession`), e vengono
inoltrati in ordine di arrivo quando termina; uno in più viene rifiutato e il client
 disconnesso. L'esecutore accetta al massimo 64 operazioni alla volta ed esegue al massimo
quattro handler contemporaneamente.
L'ammissione rimane non bloccante. Disconnessione e arresto del server annullano il
token dell'handler; osservalo in ogni chiamata I/O attesa. Le eccezioni vengono registrate
senza contenuti dei pacchetti e non fermano il game loop.

### Integrazione con l'host

**Un nuovo pacchetto in ingresso richiede due registrazioni.** La registrazione dell'handler
non aggiunge l'opcode alla tabella del protocollo. Aggiungi anche il tipo del pacchetto:

```csharp
container.RegisterIncomingPacket<ExamplePacket>();
container.RegisterPacketHandler<ExamplePacket, ExamplePacketHandler>();
```

All'avvio l'host costruisce un registro da `PacketTable` più ogni chiamata
`RegisterIncomingPacket`, lo blocca e lo fornisce ai framer, ai listener di accesso
e gioco e al dispatcher. I pacchetti in uscita non richiedono registrazione per essere
inviati. Un pacchetto registrato senza handler viene decodificato ma rifiutato e il server di
gioco chiude la connessione, come fa per un opcode sconosciuto. Registra
`IgnoredPacketHandler<T>` per un pacchetto inviato dal client su cui il server non
agisce ancora.

L'host registra anche LoginSeed e un handler AccountLogin asincrono. Quest'ultimo
verifica le credenziali con `IAccountService`, invia `0x82` per un accesso rifiutato o
un elenco vuoto di realm idonei e invia un elenco `0xA8` filtrato dopo il successo.
L'host solo di accesso usa una pipeline asincrona ordinata di connessione dedicata; standalone
esegue listener separati per accesso e gioco. Selezione e passaggio usano lease
basati su Redis e ticket monouso ([Stato dell'implementazione](implementation-status.md)). Vedi
[Trasporto e proprietà dello stato di gioco](network-game-separation.md) per il ciclo di vita della connessione,
i limiti delle code e la politica di sovraccarico, e [Game loop e timer](game-loop-and-timers.md)
per la proprietà dei thread e il completamento.
