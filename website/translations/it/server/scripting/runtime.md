<!-- translation: {"sourceHash":"b540acc32723b0d8089d3ab17f4c23645090846d8ec51eaeb871f889acc331cf","title":"Ricaricamento, limiti ed editor"} -->

# Ricaricamento, limiti ed editor

Questa pagina fa parte di [Scrivere script Lua](../scripting.md).

## Ricaricamento e proprietà

La console accetta:

```text
script reload init.lua
script metrics
```

Il ricaricamento invalida il file indicato, rimuove la voce corrispondente dalla cache di `require`,
annulla timer/coroutine appartenenti a quel file e lo esegue di nuovo sul loop.
Non ricostruisce l'intero stato Lua, non invalida ricorsivamente le dipendenze e non
ripristina le globali se il nuovo file fallisce. I riferimenti esistenti a una tabella di modulo
rimangono riferimenti a quella vecchia tabella. Dopo aver modificato un helper richiesto, invalida
quell'helper e ricarica lo script che lo usa per ottenere il nuovo valore del modulo.

La proprietà segue il contesto attivo di caricamento/coroutine del motore. In particolare,
`require` esegue un modulo nel contesto del chiamante; non bisogna presumere che i timer creati lì
appartengano al nome del file del modulo. Preferisci moduli che restituiscono
funzioni/dati e lascia che `init.lua` o un proprietario caricato esplicitamente creino i timer.
Questo rende prevedibile l'annullamento al ricaricamento. L'arresto del motore annulla tutto il lavoro
pianificato di sua proprietà prima di rilasciare lo stato Lua.

`script metrics` mostra caricamenti dei file, chiamate, riprese/completamenti/errori delle coroutine,
coroutine attive, interruzioni per limiti, raggiungimenti del limite delle stringhe ed eventi server scartati
perché il game loop li ha rifiutati (ogni scarto viene anche registrato come avviso). Gli errori degli script includono informazioni
sulla sorgente quando disponibili, vengono registrati e pubblicano `ScriptErrorEvent`. Gli errori ordinari
di runtime vengono segnalati dallo scheduler; i chiamanti C# dell'host di `LoadFile` devono
gestirne le eccezioni. File mancanti e annullamento hanno propri percorsi di errore.

## Limiti e sandbox

I valori predefiniti consentono 150.000 istruzioni per ripresa di coroutine e 10.000.000 per
blocco di primo livello caricato, verificate ogni 1.000 istruzioni. Limitano l'esecuzione delle istruzioni della VM,
non il tempo reale né il lavoro C# nativo. Un modulo che si blocca sull'I/O
può ancora bloccare il loop; mantieni le funzioni collegate all'host brevi e sincrone.
`string.rep` limita il risultato a 16.777.216 caratteri UTF-16 per impostazione predefinita. Le altre
allocazioni, comprese tabelle e concatenazione, non hanno un limite globale di memoria.
Considera gli script e i plugin C# contenuti fidati dello shard, non un confine di isolamento
per codice ostile arbitrario.

Quali librerie sono disponibili a uno script e dove il Lua del server differisce dal manuale sono descritti in
[Lua in Moongate](lua-in-moongate.md).

## Supporto dell'editor

Con `write_definitions = true`, l'avvio scrive `scripts/definitions.lua` e
`scripts/.luarc.json` da moduli, funzioni, costanti ed enum registrati.
Apri la cartella degli script in un editor che usa il language server Lua per il completamento.
Questi file sono generati: inserisci i contenuti scritti a mano in file separati e registra
i moduli C# prima dell'avvio affinché compaiano nelle definizioni. Forniscono metadati
per l'editor, non caricamento a runtime; non usare `require("definitions")`.
