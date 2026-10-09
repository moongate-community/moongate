<!-- translation: {"sourceHash":"ffcc1e3de1c70bb721bda1d75257c394a36ee7fa2ee8714cfe2c753cd2b1f588","title":"Feste"} -->

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
mucchio di neve, un mucchio di neve glaciale, la luce del solstizio d'inverno e una decorazione (un
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

Le decorazioni vengono da `templates/items/misc/christmas.toml`. Il deed del vischio, gli altri
pezzi invernali del 2010 e la neve delle zone sicure non ci sono, e neppure le decorazioni delle
città: arrivano dopo.
