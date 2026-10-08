<!-- translation: {"sourceHash":"ab6509a2755340f50a7bcfa4e75cc7a548afe3f3bf2fe82d95a31d748e183eda","title":"Venditori"} -->

# Venditori

Un venditore PNG vende merce in cambio di oro, come in ModernUO. Il giocatore apre la finestra del negozio di un venditore,
vede cosa vende con il prezzo di un pezzo, sceglie cosa comprare e riceve la merce nello zaino. Ciò che ogni venditore vende è un
[negozio](data-files/shops.md), convertito dalle classi `SBInfo` di ModernUO. Il giocatore può anche vendere al venditore. Il rifornimento
degli scaffali non è ancora realizzato.

## Aprire la finestra

Il giocatore apre la finestra in due modi, da dove il venditore è raggiungibile:

- Cliccare il venditore e scegliere *Buy* o *Sell* nel [menu contestuale](context-menus.md), entro 8 caselle.
- Dire *vendor buy* o *vendor sell* entro 4 caselle. Il client trasforma le parole in una parola chiave del parlato in qualsiasi lingua del client,
  quindi *vendor buy* funziona anche con un client italiano. Quando più venditori sentono le parole, risponde uno solo.

Un template che eredita `basevendor` esegue lo script, ma vendono solo quelli con un negozio: gli altri non fanno nulla quando
viene scelto *Buy*. Un venditore apre la finestra solo quando ha un [negozio](data-files/shops.md) con merce disponibile, è nel mondo, dista
al massimo 10 caselle, è in vista e il giocatore è vivo. Un assassino in un luogo sorvegliato viene rifiutato dalla voce
del venditore (cliloc 501522).

## Comprare

La finestra elenca la merce con i prezzi. Il giocatore sceglie un numero di pezzi per ogni riga e conferma. Un
acquisto è tutto o niente: quando un controllo fallisce, non viene preso nulla e non viene dato nulla.

- Un acquisto ha da 1 a 100 righe. Una riga scelta due volte si somma. Ogni riga è una riga della finestra e la sua quantità è
  al massimo la scorta del venditore.
- Il totale è il prezzo di un pezzo per i pezzi di ogni riga; un totale oltre `int.MaxValue` viene rifiutato. Un game
  master non paga nulla.
- L'oro viene dallo zaino e dalle borse al suo interno. Quando lo zaino non basta e il totale è 2000 o più,
  la banca copre la differenza; sotto 2000 il venditore dice *thou canst not afford* (cliloc 500192) e la banca
  non viene toccata. Una banca che non può coprire la differenza riceve la risposta cliloc 500191. Il controllo viene prima della creazione di
  qualsiasi oggetto, quindi un acquisto rifiutato non costa nulla.
- Una riga impilabile dà una pila, ogni altra riga dà un oggetto per ogni pezzo. I serial per tutti sono
  riservati prima di prendere qualsiasi cosa; un ordine che ne richiede più di quelli pronti sul server viene rifiutato con il cliloc
  500187.
- La merce va nello zaino. Quando non ci sta, viene messa a terra ai piedi del giocatore.
- Il giocatore legge quanto ha pagato: cliloc 1151639 per l'oro dello zaino, 1151638 quando è stata usata la banca.

La finestra si chiude dopo ogni risposta, acquisto o rifiuto. Una risposta che non è per la finestra aperta di quel
venditore, o che ha più di 100 righe, viene scartata.

## Vendere

*Sell* apre un elenco di ciò che il giocatore porta e che il negozio del venditore compra, con l'oro pagato per un pezzo. L'elenco contiene
gli oggetti dello zaino e delle borse al suo interno, al massimo 250. Un oggetto indossato, tenuto sul cursore, non spostabile o un
contenitore con qualcosa dentro non viene offerto. Un venditore che non compra nulla di ciò che il giocatore porta dice *you have nothing I
would be interested in*.

Il giocatore sceglie gli oggetti e quanti pezzi di ciascuno, e conferma. Una vendita è tutto o niente, come un acquisto:

- La risposta ha meno di 100 righe. Ogni oggetto deve essere uno di quelli offerti e ancora nello zaino, e una riga
  scelta due volte si somma. Una quantità oltre quella dell'oggetto viene ridotta a essa. Non si vendono più di 500 pezzi per volta, e
  il totale è rifiutato oltre `int.MaxValue`.
- Il venditore deve essere raggiungibile e il giocatore vivo, e un assassino in un luogo sorvegliato viene rifiutato, come per un acquisto.
- L'oro è pagato in pile da 60000 nello zaino, o nella cassetta di banca quando lo zaino non ha posto per esse. Quando
  nessuno dei due ha posto, non si vende nulla e al giocatore viene detto che lo zaino è pieno.
- I pezzi vengono tolti dagli oggetti, e un oggetto venduto per intero viene eliminato. Il venditore non li tiene ancora sui
  suoi scaffali.

## Scorta

Ogni venditore parte con la quantità di ogni riga del suo negozio. Un acquisto la riduce e la finestra successiva mostra ciò che
resta. La scorta è tenuta solo in memoria: un riavvio restituisce a ogni venditore gli scaffali pieni, come in ModernUO. Il
rifornimento nel tempo non è ancora realizzato.

## Cosa riceve il client

La finestra è quella del client, guidata da questi pacchetti: `0x2E` mette due contenitori virtuali sul venditore (i layer
*ShopBuy* e *ShopResale*, di cui il client ha bisogno o va in crash), `0x3C` riempie il primo con un oggetto virtuale per
ogni riga, `0x74` dà il prezzo e il nome di ogni riga, e `0x24` apre la finestra con il gump `0x30`. L'acquisto
torna in `0x3B`, e il server risponde con un `0x3B` che chiude la finestra. I serial degli oggetti
virtuali scendono da `Serial.MaxVirtual` e non significano nulla fuori dalla finestra aperta. L'elenco di vendita esce in `0x9E` e la scelta del giocatore torna in `0x9F`;
i suoi oggetti sono del giocatore, con i loro serial reali. Vedi [Pacchetti](packets.md).

## Per chi scrive script

I template dei venditori che ereditano `basevendor` eseguono `scripts/mobiles/shopkeeper.lua`, che offre *Buy* nel menu
contestuale e ascolta *vendor buy*. Lo script si chiama `shopkeeper` perché `vendor` è il nome del modulo Lua
che apre la finestra:

```lua
vendor.open_buy(npc, player)
```

Lo script di un venditore che ne ha uno proprio, come `banker.lua` o `healer.lua`, può chiamarla a sua volta. Vedi
[Moduli Lua](lua-modules.md).

## Limiti

- Il riacquisto e il rifornimento degli scaffali sono la prossima parte.
- Istruttori di abilità, animali, ordini di lavorazione, il moltiplicatore dei prezzi delle città, venditori dei giocatori e l'oro
  che un venditore possiede non sono realizzati.
- I negozi forniti con il server contengono le righe di ModernUO che hanno un template oggetto. Il convertitore segnala le
  altre: armi e qualche altra merce non hanno ancora un template, quindi quei venditori vendono meno che in ModernUO.
