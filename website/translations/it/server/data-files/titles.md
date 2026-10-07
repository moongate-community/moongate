<!-- translation: {"sourceHash":"87a8164ee9c1b4532223831db2c112f9c9f0469f7bc988fc9ffaa1d7c4e735fa","title":"Titoli di fama e karma"} -->

# Titoli di fama e karma

`data/titles.toml` definisce il prefisso inglese del titolo derivato da fama e karma
correnti di un mobile. La copia nel repository è
`moongate_root/data/titles.toml`; `mgctl` la installa nella directory
`data/` della root del server quando manca. Il server di gioco la carica all'avvio. Riavvia
il server dopo aver modificato il file.

Ogni riga `[[titles]]` definisce punteggi minimi inclusivi di fama e karma. `title`
è il prefisso per i personaggi maschili e il valore predefinito per quelli femminili.
`female_title` è facoltativo e lo sostituisce per i personaggi femminili. Una stringa
vuota significa esplicitamente nessun prefisso.

```toml
[[titles]]
fame = 10000
karma = 10000
title = "The Glorious Lord"
female_title = "The Glorious Lady"
```

| Campo | Significato |
| --- | --- |
| `fame` | Fama minima intera obbligatoria, inclusiva. |
| `karma` | Karma minimo intero obbligatorio, inclusivo. |
| `title` | Stringa obbligatoria del prefisso. `""` è valido per una fascia senza titolo. |
| `female_title` | Prefisso femminile facoltativo; se assente si usa `title`. `""` sopprime esplicitamente il prefisso. |

Il file fornito contiene le 55 combinazioni classiche: cinque fasce di fama
(`0`, `1250`, `2500`, `5000`, `10000`) e undici fasce di karma (`-15000`,
`-9999`, `-4999`, `-2499`, `-1249`, `-624`, `625`, `1250`, `2500`, `5000`,
`10000`). Il server seleziona la soglia di fama più alta minore o uguale al
punteggio, poi la soglia di karma più alta minore o uguale al punteggio. I valori sotto
la prima soglia usano la prima fascia; quelli sopra l'ultima usano l'ultima.
L'ordine delle righe nel file non influisce sulla ricerca.

Ogni soglia di fama deve avere una riga per ogni soglia di karma. L'avvio fallisce
indicando percorso del file e motivo se il file manca, è malformato, vuoto, ha
campi mancanti, coppie duplicate, titoli composti solo da spazi o coppie mancanti.

I chiamanti C# risolvono `IFameKarmaTitleService` e chiamano `GetTitle(mobile)` o
`GetTitle(fame, karma, gender)`. Il risultato è solo un prefisso, come
`"The Glorious Lady"`; i chiamanti decidono come combinarlo con un nome. Il
servizio calcola il risultato dai punteggi correnti a ogni chiamata. Non
modifica `MobileEntity.Title`, che resta il suffisso personalizzato o del template.
La paperdoll mostra il prefisso prima del nome (vedi [pacchetti](../packets.md)), `female_title` per le donne; i tooltip e il nome con un clic non lo mostrano, come in ModernUO.

Vedi i [file di dati](../data-files.md) per indicazioni su installazione e validazione.
