<!-- translation: {"sourceHash":"1935fe1db5d87fa40a6972034e2c3fa6e3dc354f71a5fba48d8f4a1840f89662","title":"Feste"} -->

# Feste

Gli eventi stagionali del [calendario](schedule.md) che arrivano con dei contenuti. Lo staff ne
accende o spegne uno con [`.event`](commands/event.md); le date sono in `data/schedule.toml`.

| Evento | Date | Cosa fa |
| --- | --- | --- |
| `halloween` | Dal 24 ottobre al 15 novembre | [Dolcetto o scherzetto](#halloween-trick-or-treat) |
| `christmas` | Dal 24 dicembre al 1° gennaio | [Palle di neve e regali](#christmas-snowballs-and-gifts) |

## Halloween: dolcetto o scherzetto

Mentre `halloween` è attivo, un giocatore che dice `trick or treat` entro 4 caselle da un
negoziante riceve una risposta:

- Nove volte su dieci il negoziante dice una battuta, mette una caramella nello zaino del giocatore
  (una tra `lollipops`, `wrapped candy`, `jelly beans`, `taffy`, `nougat swirl`) e il giocatore
  legge `You receive some candy.`
- Una volta su dieci grida `TRICK!` e del sangue schizza attorno al giocatore.
- Quando più negozianti sentono la stessa frase, risponde solo il primo.
- Dopo aver risposto, un negoziante riposa da 5 a 10 minuti. Se glielo si chiede prima non risponde, e
  il giocatore legge `That doesn't appear to have any more candy.` Ogni negoziante riposa per conto suo.

Le caramelle sono cibo: un doppio clic la mangia. Non si vendono. Quando l'evento inizia e finisce,
tutti vengono avvisati.

Le parole sono in inglese qualunque sia la lingua del server; le battute e i messaggi sono nella
[lingua del server](localization.md). Fuori dall'evento non succede niente.

### File

| File | Contenuto |
| --- | --- |
| `templates/items/misc/halloween.toml` | I template delle caramelle (`lollipop1` a `lollipop3`, `wrappedcandy`, `jellybeans`, `taffy`, `nougatswirl`), convertiti da UOX3. |
| `scripts/common/trick_or_treat.lua` | Il gioco; `shopkeeper.lua` lo chiama da `on_speech`. |
| `scripts/events/halloween.lua` | Gli hook `on_start` e `on_end`: gli annunci. |
| `data/schedule.toml` | L'`[[event]]` con le date; cambia `from` e `to` per spostare la stagione. |

### Idee di ModernUO che non ci sono

I dolcetti speciali per un maestro di elemosina, gli scherzi del colore pieno e del gemello
birichino, il campo di zucche, gli zombie dei giocatori e le maschere.

## Natale: palle di neve e regali

**Il regalo.** Un personaggio che entra nel mondo mentre `christmas` è attivo trova nello zaino un
mucchio di neve, un mucchio di neve glaciale, una candela natalizia e una decorazione (un
topiario decorativo 60 volte su 100, un cactus natalizio 24, un albero innevato 16), e legge
`Happy Holidays! Gift items have been placed in your backpack.` Il regalo arriva una volta a stagione: un
personaggio che ne ha ricevuto uno meno di 200 giorni fa non ne riceve, e neppure uno il cui zaino non
può accogliere i mucchi (lo riceverà al prossimo accesso). L'inizio e la fine della stagione vengono
annunciati a tutti.

**La palla di neve.** Un doppio clic su un mucchio che è nello zaino prepara una palla di neve e apre
un cursore. Il bersaglio deve essere un mobile entro 10 caselle che porta a sua volta un mucchio di
neve (può rilanciarla). La palla vola fino a lui con il suono e il gesto del lancio; entrambi leggono
il testo del client, `You have just been hit by a snowball!` e
`You throw the snowball and hit the target!`. Un giocatore aspetta 5 secondi tra due palle di neve e non può lanciarne una in sella, contro
se stesso o contro qualcosa che non porta neve.

| File | Contenuto |
| --- | --- |
| `templates/items/misc/winter_gifts.toml` | `snow_pile` e `glacial_snow`, i due mucchi con lo script della neve. |
| `scripts/items/snow_pile.lua` | La palla di neve. |
| `scripts/events/christmas.lua` | `on_start`, `on_end` e `on_login`: gli annunci e il regalo. |

Le decorazioni del regalo vengono da `templates/items/misc/christmas.toml`. Il deed del vischio,
gli altri pezzi invernali del 2010 e la neve delle zone sicure non ci sono.

## Le decorazioni delle città

Quando un evento inizia le città vengono decorate, e quando finisce o lo staff lo spegne le
decorazioni vengono tolte, qualunque sia il motivo per cui l'evento si ferma: `.event off`, la data di
fine, o un server che era spento quel giorno (l'hook parte al prossimo avvio).

- **Dove.** Attorno al centro di Britain, Trinsic, Vesper, Minoc, Yew, Skara Brae e Moonglow (i luoghi
  della categoria `Factions/Towns` di [`locations.toml`](data-files/locations.md)), su Felucca e su
  Trammel: fino a 8 punti per città e mappa, da 3 a 6 caselle dal centro. Un punto viene saltato quando
  c'è una creatura o un oggetto sopra, quando non c'è un pavimento, o quando il pavimento è a più di 8 livelli dal centro.
- **Cosa.** Halloween: zucche intagliate, zucche, teschi su una picca, uno spaventapasseri di zucca, una
  statua di gatto nero, una statua di ghoul. Natale: alberi innevati, topiari, cactus natalizi, stelle di
  Natale. I pezzi sono i template di `templates/items/misc/holiday_decorations.toml`, che non si possono
  sollevare e non decadono mai.
- **Come si ricorda.** I serial sono nella proprietà del mondo `holiday.<event>.items`, quindi un
  riavvio a metà evento non le mette due volte, e la fine le toglie anche dopo un riavvio. Il server
  crea pochi oggetti alla volta: una serie lunga si completa qualche secondo dopo. Se l'evento finisce
  prima, il lavoro si ferma.

| File | Contenuto |
| --- | --- |
| `scripts/common/holiday_decor.lua` | `place` e `remove`. |
| `templates/items/misc/holiday_decorations.toml` | I template delle decorazioni. |

Un nuovo evento ottiene le decorazioni chiamando `holiday_decor.place(id, templates)` nel suo
`on_start` e `holiday_decor.remove(id)` nel suo `on_end`.
