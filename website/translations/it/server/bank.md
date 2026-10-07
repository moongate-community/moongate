<!-- translation: {"sourceHash":"63b0a857021237bcab6577db2c46d6e8de3ce9892116c808cc4b80006dae49d1","title":"Banca"} -->

# Banca

Ogni giocatore ha una cassa bancaria, come in ModernUO: un contenitore indossato sul layer della banca, che il client
non disegna mai. Un banchiere la apre quando il giocatore lo chiede; il giocatore può quindi accedere al contenuto finché
non si allontana.

## Aprire la banca

Pronuncia *bank* vicino a un banchiere. Il client converte la parola nella parola chiave del parlato
`SpeechKeywordType.Bank` nella propria lingua, quindi *banca* su un client italiano funziona allo stesso modo; il
[`banker.lua`](#the-banker-script) distribuito legge anche la parola semplice, per un client che non invia
parole chiave. La cassa bancaria si apre con una riga sopra il giocatore:

```text
Bank container has 3 items.
```

La prima volta, la cassa bancaria viene creata (template `bank_box` di `templates/items/bank.toml`, grafica di una cassa
metallica) e salvata, poi mostrata. I template di banchiere `banker`, `m_banker` e `f_banker` usano
lo script, quindi ogni banchiere degli [spawn](spawns.md) risponde.

Un banchiere offre anche `Open Bank Box` nel suo [menu contestuale](context-menus.md), dalle stesse 12
caselle: apre la cassa come fa la parola, e un criminale riceve lo stesso rifiuto.

## Mentre è aperta

La banca rimane aperta mentre il giocatore resta dove è stata aperta: un passo, un teletrasporto, un cambio di mappa o un
nuovo accesso la chiudono, e tornare al punto non la riapre. Girarsi sul posto non
la chiude.

- Il proprietario solleva, rilascia e usa ciò che contiene solo mentre è aperta: una banca chiusa rifiuta il
  sollevamento, respinge ciò che vi viene rilasciato e non apre nulla.
- Nessun altro può accedervi; i game master e gli amministratori accedono alla propria anche mentre è
  chiusa.
- La cassa bancaria stessa non lascia mai il layer della banca, e `world.carries` non cerca al suo interno: una chiave
  in banca non apre una porta.

## Oro tramite il parlato

Entro 12 caselle da un banchiere, con la cassa aperta oppure no:

| Pronuncia | Il banchiere | Risponde |
| --- | --- | --- |
| *balance* | comunica l'oro nella tua banca | `Thy current bank balance is 1,234 gold.` |
| *withdraw 500* | sposta 500 monete dalla banca al tuo zaino | `Thou hast withdrawn gold from thy account.` |
| *deposit 500* | sposta 500 monete dal tuo zaino alla banca | `500 gold was deposited in your account.` |
| *check 5000* | scrive un [assegno bancario](#bank-checks) per 5000 monete della banca | `Into your bank box I have placed a check in the amount of: 5,000` |

- *balance*, *withdraw* e *check* sono parole chiave del parlato del client, come *bank*: funzionano nella
  lingua del client. *deposit* è solo una parola inglese, perché il client non ha una parola chiave
  corrispondente.
- L'importo è il primo numero della frase: `withdraw 500`, `I wish to withdraw 500 gold`
  e `500 withdraw` sono equivalenti. Una frase senza numero, o con 0, non sposta nulla e non riceve
  risposta. I separatori non vengono letti: `5,000` è 5.
- Il saldo conta le monete e il valore degli assegni ovunque nella cassa bancaria, comprese
  le borse.
- Un prelievo è al massimo di [`max_withdraw`](#settings) monete. Quando le monete della banca non sono
  sufficienti gli assegni forniscono il resto: un assegno esaurito scompare, l'ultimo conserva ciò che ne
  rimane. Si uniscono a una pila d'oro nello zaino
  quando c'è spazio, con un massimo di 60000 per pila, e altrimenti creano una nuova pila.
- Un deposito prende monete dallo zaino e dalle sue borse; in banca riempiono le pile della
  cassa, poi creano pile da 60000.
- L'oro pesa, una moneta 0,02 stone: come in ModernUO il banchiere consegna ciò che chiedi anche quando
  supera quello che puoi trasportare, e ti allontani sovraccarico. Solo uno zaino già al limite del proprio peso
  non prende nulla.
- Entrambe le operazioni sono tutto o niente: un rifiuto non sposta alcuna moneta.
- Le frasi del banchiere sono i testi nativi del client, quindi ogni giocatore le legge nella propria lingua.
- Quando più banchieri ti sentono, uno risponde e l'oro si sposta una sola volta.

| Il banchiere dice | Perché |
| --- | --- |
| `Thou art a criminal and cannot access thy bank box.` | Un criminale ha pronunciato *bank*. |
| `I will not do business with a criminal!` | Un criminale ha chiesto altro. |
| `Thou canst not withdraw so much at one time!` | Più di `max_withdraw`. |
| `Ah, art thou trying to fool me? Thou hast not so much gold!` | La banca, o lo zaino per un deposito, contiene meno di quell'importo. |
| `Your backpack can't hold anything else.` | Lo zaino è già al limite del peso oppure non ha spazio per una nuova pila. |
| `Your bank box is full.` | Il deposito richiede una nuova pila e la cassa ha raggiunto il suo [limite di oggetti](#how-much-it-holds). |
| `We cannot create checks for such a paltry amount of gold!` | Un assegno sotto [`min_check`](#settings). |
| `Our policies prevent us from creating checks worth that much!` | Un assegno sopra [`max_check`](#settings). |
| `There's not enough room in your bankbox for the check!` | La cassa è piena e le monete che pagano l'assegno non esauriscono alcuna pila. |

Un giocatore che pronuncia *deposit* prima di aver mai aperto la propria banca ottiene invece la creazione e la visualizzazione della cassa,
e deve chiedere di nuovo.

## Oro consegnato al banchiere

Rilascia una pila d'oro o un assegno bancario su un banchiere da 2 caselle o meno e viene trasferito nella tua
banca (da più lontano leggi `That is too far away.`; lo staff consegna da qualsiasi distanza): il banchiere dice `1,250 gold was deposited in your account.`, usando il valore di un assegno.

- L'oro riempie le pile d'oro della cassa e ciò che resta forma una pila propria; un assegno entra
  com'è. Tutto o niente: un rifiuto restituisce l'oggetto al punto da cui lo hai sollevato.
- La cassa non deve essere aperta.
- Questo è l'`on_drag_drop` dello script ([script dei mobile](scripting/mobile-scripts.md)), basato su
  `bank.deposit_item`.

| Il banchiere dice | Perché |
| --- | --- |
| `I will not do business with a criminal!` | L'ha consegnato un criminale. |
| `Your bank box is full.` | L'oro o l'assegno richiedono un posto e la cassa ha raggiunto il suo [limite di oggetti](#how-much-it-holds). |
| `I am not interested in this.` | Non è né oro né un assegno bancario. |

Un giocatore che non ha mai aperto la banca ottiene la creazione e la visualizzazione della cassa e consegna di nuovo l'oro.

## Assegni bancari

Un assegno è oro su carta: un oggetto che pesa una stone, qualunque sia il suo valore. Pronuncia *check 5000*
a un banchiere e prende 5000 monete dalla tua banca e mette un assegno nella tua cassa bancaria.

- Un assegno vale da [`min_check`](#settings) a [`max_check`](#settings) monete, da 5000 a
  1.000.000 salvo diversa impostazione dello shard.
- Viene pagato con le monete della banca: un altro assegno non paga un assegno.
- Il suo tooltip mostra `value: 5,000`. È blessed e non si impila.
- **Per incassarlo, fai doppio clic sull’assegno dentro la tua cassa bancaria aperta**, nella cassa o in una sua borsa:
  diventa monete della cassa, riempiendo le pile d'oro presenti e poi creando pile da 60000, e
  leggi `5,000 gold was deposited in your account.`
- Una cassa con spazio per parte dell'oro prende ciò che entra e l'assegno conserva il resto; senza spazio
  per nulla leggi `Your bank box is full.`
- Un singolo incasso crea al massimo 125 pile, 7.500.000 monete: un assegno che vale di più conserva il resto e
  un altro doppio clic prosegue.
- Ovunque altrove un doppio clic dice `That must be in your bank box to use it.`

Un game master crea un assegno di qualsiasi valore con [`create_check`](commands/create_check.md).

Un assegno è il template di oggetto `bank_check` di `templates/items/bank.toml`, con il suo valore nella
proprietà `bank.worth`; il suo script è `scripts/items/bank_check.lua`. Il nome che mostra è quello
nativo del client, `A bank check`.

## Quanto contiene

Una cassa bancaria contiene [`max_items`](#settings) oggetti, 125 salvo diversa impostazione dello shard, contando
tutto ciò che è dentro le sue borse: una borsa con dieci cose al suo interno vale undici. Un rilascio che supererebbe
il limite viene respinto e leggi `That container cannot hold more items.`

- Aggiungere monete o qualsiasi cosa impilabile a una pila già presente non aggiunge un oggetto.
- Un oggetto rilasciato in una borsa dentro la cassa bancaria conta anche per la cassa.
- I game master e i gradi superiori sono esenti.
- La cassa non ha limite di peso e ciò che contiene non pesa sul proprietario.

La stessa regola vale per ogni contenitore il cui [template di oggetto](templates.md) imposta `max_items`; un
template che non specifica nulla non ha limite. Il dono di uno script (`item.give`) a uno zaino senza
spazio non consegna nulla.

## Impostazioni

```toml
[ultima.bank]
max_items = 125        # Items in a bank box, bags included; 0 for no limit.
max_withdraw = 60000   # Coins a banker hands out at one time.
min_check = 5000       # The smallest check a banker writes.
max_check = 1000000    # The largest.
```

`max_items` va da 0 a 10000, `max_withdraw` da 1 a 60000 (una pila), `min_check` da 1 a
`max_check`, `max_check` fino a 2.000.000.000.

## Lo script del banchiere

`scripts/mobiles/banker.lua` contiene le regole sopra:
quale parola corrisponde a quale comando, la distanza, il criminale, l'importo e quale testo del client
risponde a cosa. Puoi modificarlo. Si basa su queste funzioni:

```lua
function banker.on_speech(serial, speaker, text, keywords)
    -- One banker serves when several hear the words.
    if not bank.attend(speaker) then
        return
    end

    if mobile.criminal(speaker) then
        npc.say_cliloc(serial, 500389) -- I will not do business with a criminal!
        return
    end

    -- A banker never walks: it turns to who asks.
    npc.look_at(serial, speaker)

    if bank.withdraw(speaker, 500) == BankResultType.Ok then
        npc.say_cliloc(serial, 1010005) -- Thou hast withdrawn gold from thy account.
    end
end
```

Il banchiere si gira verso chi chiede (`npc.look_at`): un NPC nasce rivolto a sud e un banchiere non cammina mai,
quindi senza questo ogni banchiere di una banca guarderebbe per sempre nella stessa direzione.

`on_speech` riceve come quarto argomento le parole chiave del parlato individuate dal client, un array di numeri;
`SpeechKeywordType` assegna i nomi a quelle della banca (`Withdraw`, `Balance`, `Bank`, `Check`).

| Funzione | Cosa fa |
| --- | --- |
| `bank.open(player)`, `bank.is_open(player)` | Apre la cassa bancaria; indica se è aperta. |
| `bank.balance(player)` | L'oro in banca; 0 per un giocatore che non l'ha mai aperta. |
| `bank.withdraw(player, amount)` | Monete verso lo zaino; un `BankResultType`. |
| `bank.deposit(player, amount)` | Monete dallo zaino; un `BankResultType`. |
| `bank.check(player, amount)` | Scrive un assegno pagato con le monete della banca; un `BankResultType`. |
| `bank.cash(player, check)` | Converte in monete un assegno dentro la cassa bancaria; un `BankResultType`. |
| `bank.worth(item)` | Quanto vale un assegno; nil per qualsiasi altra cosa. |
| `bank.attend(player)` | True per il primo banchiere che lo chiede nello stesso momento. |
| `npc.say_cliloc(npc, cliloc [, args [, affix]])` | L'NPC pronuncia un testo del client, per ogni giocatore nella propria lingua; `affix` viene scritto dopo. |

`BankResultType` è `Ok`, `NotEnoughGold`, `TooMuch`, `BackpackFull`, `BankFull`, `BadAmount`,
`NoPlayer`, `NoBank` (il giocatore non ha mai aperto la banca), `Busy` (riprova tra un momento),
`CheckTooSmall`, `CheckTooBig` o `NotInBank` (non è un assegno oppure non è nella cassa bancaria del giocatore). Il
modulo non controlla dove si trova il giocatore né chi è: lo fa lo script. Vedi il
[modulo `bank`](https://moongate.sh/lua/bank/).

## Vedi anche

- [Spawn degli NPC](spawns.md)
- [Scrivere script Lua](scripting.md)
