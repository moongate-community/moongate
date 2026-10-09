<!-- translation: {"sourceHash":"5923d139ee01c33e6d1e4f77d9966e7b2ce9f0df3fb23bb9e271028302edc11e","title":"Feste"} -->

# Feste

Gli eventi stagionali del [calendario](schedule.md) che arrivano con dei contenuti. Lo staff ne
accende o spegne uno con [`.event`](commands/event.md); le date sono in `data/schedule.toml`.

| Evento | Date | Cosa fa |
| --- | --- | --- |
| `halloween` | Dal 24 ottobre al 15 novembre | [Dolcetto o scherzetto](#halloween-trick-or-treat) |

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
birichino, il campo di zucche, gli zombie dei giocatori e le maschere. Il Natale e le decorazioni di
entrambi arrivano dopo.
