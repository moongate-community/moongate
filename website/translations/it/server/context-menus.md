<!-- translation: {"sourceHash":"644dedb4d5fdbae95a85a40ce43f19c524de62bc7e1da419efdc84e0b659aeaa","title":"Menu contestuali"} -->

# Menu contestuali

Fai clic su un mobile o su un oggetto e il client mostra il suo menu contestuale: un breve elenco di ciò che si può
fare con esso. Ogni voce è un testo del client, quindi ogni giocatore la legge nella propria lingua. Moongate
riempie il menu con alcune voci proprie e con quelle che aggiunge lo [script Lua](scripting.md) di quell'NPC o
oggetto.

Come si apre il menu lo decide il client, con un clic o con un clic tenendo premuto un tasto, secondo le sue opzioni:
il server risponde solo quando il client lo chiede.

## Le voci del server

| Su | Voce | Da | Fa |
| --- | --- | --- | --- |
| Un mobile con corpo umano, anche un fantasma | `Open Paperdoll` | 18 caselle | Ciò che fa un doppio clic su di esso |
| Te stesso, vivo, con uno zaino | `Open Backpack` | 18 caselle | Ciò che fa un doppio clic sul tuo zaino |

Vengono per prime nel menu e uno script non può toglierle.

Un fantasma riceve queste e nient'altro: ai morti non viene offerta alcuna voce di uno script, dato che nessuno
risponde alle loro parole.

## Le voci di uno script

Uno [script mobile](scripting/mobile-scripts.md) o uno [script oggetto](scripting/item-scripts.md) aggiunge le sue
voci con due funzioni:

```lua
-- The entries this NPC adds for that player; nothing, or an empty table, for none.
function banker.on_context_menu(serial, player)
    return {
        { id = "bank", cliloc = 3006105, range = 12 },
    }
end

-- The player chose one of them: id is the entry's own.
function banker.on_context_menu_select(serial, player, id)
    if id == "bank" then
        bank.open(player)
    end
end
```

| Campo di una voce | |
| --- | --- |
| `id` | Il nome della voce nello script, restituito alla scelta. Obbligatorio |
| `cliloc` | Il numero del testo del client che la voce mostra, come 3006105 `Open Bank Box`. Obbligatorio |
| `range` | Da quante caselle si può scegliere, da 0 a 18; 18 se non impostato |
| `enabled` | `false` mostra la voce in grigio; `true` se non impostato |

- `on_context_menu` risponde subito: uno script che lì chiama `wait` non aggiunge nulla.
- Una voce non ben formata (senza `id`, con un `cliloc` che non è un numero intero maggiore di 0, con un `range`
  fuori da 0-18) viene scartata e il log dice quale script l'ha data.
- Un menu contiene 20 voci; le altre vengono scartate, con un avviso nel log.
- `on_context_menu_select` può attendere. Viene chiamato solo per una voce del menu mostrato al giocatore,
  non in grigio, finché il giocatore è entro la sua portata e vede ancora il bersaglio.

## Cosa viene controllato

Il client chiede con il seriale di ciò su cui si è fatto clic, e poi dice quale voce è stata scelta. Moongate
conserva l'ultimo menu inviato a ogni giocatore e controlla la scelta rispetto a quello, come fa ModernUO, così un
client non può scegliere ciò che non gli è stato offerto.

| Quando | Controllato |
| --- | --- |
| Il client chiede | Il bersaglio esiste, sulla mappa del giocatore, entro il raggio di visuale (18 caselle), e il giocatore può vederlo e raggiungerlo: nessun menu per un mobile nascosto o un oggetto riservato allo staff, per un oggetto che porta un altro mobile, per uno nella banca del giocatore mentre la banca è chiusa, né per uno sollevato sul cursore. Un bersaglio senza voci non riceve alcun menu |
| Una voce è fuori dalla sua portata o non è abilitata | Viene mostrata in grigio |
| Il giocatore sceglie | Il menu è l'ultimo inviato e per lo stesso bersaglio; l'indice è una delle sue voci; la voce non è in grigio; il giocatore è ora nella sua portata; il bersaglio è ancora lì da vedere. Il menu vale per una sola scelta |

Una scelta che non supera uno di questi controlli non fa nulla, e al giocatore non viene detto niente.

## Le icone del client migliorato

Il client migliorato può scegliere una voce da un'icona propria, come la banca sulla barra di stato di un banchiere,
senza mostrare l'elenco del menu. Indica allora la voce con un numero fisso invece che con la sua
posizione nel menu: 0x78 per `Open Bank Box`, 0x12D per `Tame`, da 0x82 a 0x89 per i comandi di
un animale, e così via, come in ServUO. Moongate legge un tale numero come la voce del menu inviato che
mostra quel testo, e controlla la scelta come ogni altra: l'icona funziona solo per una voce che il menu
ha davvero, non in grigio e nella sua portata. Gli script non devono fare nulla: una voce con il
cliloc 3006105 è quella che sceglie l'icona della banca.

## Non ancora realizzato

- Le voci dei sistemi ancora da venire: compra e vendi presso un venditore, la stalla, i comandi di un animale,
  l'addomesticamento, il party.
- I vecchi menu con immagini di oggetti e domande (pacchetto 0x7C).

## Vedi anche

- [Banca](bank.md): `Open Bank Box` del banchiere
- [Script dei mobile](scripting/mobile-scripts.md) e [script degli oggetti](scripting/item-scripts.md)
