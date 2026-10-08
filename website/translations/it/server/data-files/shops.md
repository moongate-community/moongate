<!-- translation: {"sourceHash":"1f599ad47d77a22c393f4998003857170852c751706d74bb4c078647ac35fa5e","title":"Negozi"} -->

# Negozi

Un negozio dice cosa vende un tipo di venditore. Un file in `templates/shops/` contiene uno o più negozi e il server
li carica all'avvio dopo i template degli oggetti e dei mobile. I negozi forniti sono quelli di ModernUO, un file per
ogni tipo di venditore: `baker.toml`, `blacksmith.toml`, `mage.toml` e così via.

```toml
[[shop]]
id = "baker"
vendors = ["baker", "m_baker", "f_baker"]

[[shop.buy]]
item = "0x103b_bread_loaf"
price = 6
amount = 20
hue = 0
name = ""
```

| Campo | Significato |
| --- | --- |
| `id` | L'id stabile del negozio. È unico tra i file. |
| `vendors` | Gli id dei template mobile i cui venditori usano il negozio. Un template è in un solo negozio. |
| `item` | L'id di un template oggetto. |
| `price` | Oro per un pezzo, almeno 1. |
| `amount` | Quanti pezzi ha il venditore all'inizio, almeno 1. |
| `hue` | Il colore della merce. 0 mantiene quello del template oggetto. |
| `name` | Il nome mostrato nella finestra del negozio. Vuoto: il nome del client per il grafico. |

Un negozio può avere anche righe `[[shop.sell]]` per ciò che un venditore compra da un giocatore. Hanno un `item` e un `price`, l'oro
pagato per un pezzo; `amount`, `hue` e `name` sono ignorati. Lo stesso oggetto può essere sia venduto che comprato, a
prezzi diversi.

Il server rifiuta di avviarsi, indicando il file e il negozio, quando un negozio non ha id, un id è usato due volte, una riga nomina
un template oggetto che non esiste, un prezzo è sotto 1, una quantità è fuori da 1–60000, un colore è fuori da 0–65535, un nome non è ASCII o supera 253 caratteri, due righe di acquisto hanno lo stesso oggetto, prezzo e colore, un venditore non è un template mobile, oppure un
venditore è in due negozi. Un template di venditore ha bisogno solo dello script `shopkeeper`, che `basevendor` già dà, per
aprire la sua finestra: vedi [Venditori](../vendors.md).

## Aggiungere o cambiare un negozio

1. Copia un file fornito in `<root>/templates/shops/`, oppure modificalo direttamente.
2. Dai un id al negozio, elenca i template mobile che lo usano e scrivi le sue righe.
3. Riavvia il server. Un errore lo ferma con un messaggio che nomina la riga.

## Convertire i negozi di ModernUO

```bash
mgctl convert modernuo-vendors --source ~/projects/others/ModernUO/Projects/UOContent \
  --items moongate_root/templates/items --mobiles moongate_root/templates/mobiles \
  --destination moongate_root/templates/shops
```

Il convertitore legge il C# come sintassi e non esegue nulla. Prende le righe delle classi `SBInfo` che ogni classe di
venditore aggiunge e scrive un file per ogni classe di venditore, con il suo nome. Una riga diventa il template oggetto con il
grafico che ModernUO le dà; quando più template condividono un grafico, vince quello chiamato come il tipo C#, altrimenti il pezzo
semplice della prima epoca che il grafico ha (`lbr`, poi `aos`, `t2a`, `tol`), altrimenti il primo, e il rapporto lo segnala. Un
grafico che ha solo varianti di materiale (agapite, bronzo e così via) e nessun pezzo semplice viene escluso dalle righe di acquisto. Un
venditore compra un pezzo di qualunque materiale, quindi le righe di vendita di un grafico di armature o armi elencano ogni template
di epoca e di materiale. Il rapporto conta ciò che ha lasciato fuori: tipi senza template oggetto, animali, righe
e classi `SBInfo` che dipendono dall'epoca o dal venditore, e classi di venditore senza template mobile. Uno `switch`
che sceglie un insieme casuale di `SBInfo` per ogni venditore viene letto come l'unione dei suoi insiemi.
