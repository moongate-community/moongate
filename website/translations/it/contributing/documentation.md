<!-- translation: {"sourceHash":"2a4d32d7571af04b4e09dd96460a6651a2148fe90a3c18ce4718676762d2ea26","title":"Scrivere documentazione"} -->

# Scrivere documentazione

Moongate usa [Astro Starlight](https://starlight.astro.build/) per la documentazione in inglese e
italiano. L'inglese conserva gli URL root esistenti; le pagine scritte in italiano usano `/it/`. Il sito pubblico usa il dominio personalizzato GitHub Pages
[moongate.sh](https://moongate.sh/).
Le release pubblicano automaticamente il sito, e lo stesso workflow può essere eseguito manualmente per pubblicare
la documentazione tra le release.

## Eseguire localmente

Usa Node **24.21.0** (registrato anche in `website/.nvmrc`). Dalla root del repository:

```sh
npm --prefix website ci
npm --prefix website run dev
```

Apri l'URL `/` stampato da Astro. La ricerca in sviluppo non è disponibile;
usa l'anteprima di produzione per controllare l'indice di ricerca.

```sh
npm --prefix website test
npm --prefix website run build
npm --prefix website run preview -- --host 127.0.0.1 --port 4321
```

Apri `http://127.0.0.1:4321/`. La build importa i documenti sorgenti,
costruisce il sito e l'indice di ricerca e verifica link locali, immagini e frammenti.
Il validatore non scarica i link esterni.

## Modificare una pagina

Mantieni il testo sorgente nella posizione esistente:

- `docs/*.md` contiene guide del server, di riferimento e per i contributori; la guida Lua è `docs/scripting.md`
  con le sue pagine in `docs/scripting/`.
- `src/*/README.md` contiene documentazione delle librerie ed esempi NuGet.
- Il `README.md` della root fornisce la panoramica.
- `website/src/content/docs/index.mdx` è la pagina iniziale scritta manualmente. Le schede di download
  usano `website/src/components/Downloads.astro` e la versione in
  `.release-please-manifest.json` per collegarsi agli archivi della release corrispondente.

Non modificare né includere nei commit `website/src/content/docs/generated/` o
`website/public/generated/`. L'importer sostituisce queste directory.
Conserva i file originali, inclusi marcatori dei test smoke NuGet ed esempi di codice.

L'importer copia anche `scripts/install.sh` in `website/public/install.sh`, che il sito
serve a `https://moongate.sh/install.sh`. Modifica lo script, mai la copia.

La guida di riferimento dei pacchetti a `/packets/` è una route Astro. `npm run dev` e `npm run build`
rigenerano `website/src/generated/packets.json` dal registro C# e dai tipi di pacchetti Ultima
con attributi, poi lo uniscono a `website/packets/overrides.json`. Il JSON generato viene
ignorato da Git. Ogni pacchetto richiede un override con chiave `opcode:direction` (per esempio
`0xBD:incoming`); i sottocomandi usano ID come `0xBF/0x08:outgoing`. La build fallisce quando
una sorgente ha una voce assente nell'altra. Il dump richiede l'SDK .NET 10.

Anche la guida di riferimento API Lua a `/lua/` è generata: una panoramica, una pagina per modulo e una pagina
di enum. `npm run dev` e `npm run build` eseguono `website/lua/dump`, che elenca ciò che il server
pubblica in Lua: moduli ed enum registrati da `AddUltimaScriptModules` e moduli di
`Moongate.Scripting`, con la firma di ogni funzione letta dal relativo metodo C#.
`website/scripts/build-lua.mjs` converte l'elenco in Markdown sotto
`website/src/content/docs/lua/`, ignorato da Git. Nulla viene scritto a mano: la descrizione di un modulo
proviene da `[ScriptModule]`, il testo di una funzione dal `helpText` di
`[ScriptFunction]`, e la stessa descrizione alimenta `scripts/definitions.lua`, così editor e
sito concordano. La build fallisce quando un modulo non ha descrizione, una funzione non ha testo di aiuto o un
nome si ripete. Ogni funzione ha un'ancora con il proprio nome, come `/lua/npc/#walk_to`. Il dump
richiede l'SDK .NET 10.

Un esempio va in `website/lua/examples/<module>.md`: una sezione `## <function>` per funzione, il cui
corpo, una frase e un blocco di codice Lua oppure una nota, viene inserito sotto quella funzione nella pagina del modulo. Il corpo
è Markdown. La build fallisce per un file che non indica un modulo, una sezione che non indica una funzione del modulo, una
sezione ripetuta o vuota, testo prima della prima sezione, un file senza sezioni, un blocco di codice lasciato aperto e
qualsiasi altra intestazione dentro una sezione. La suite di test controlla
anche i nomi (`PublishedScriptModulesTests`), quindi rinominare una funzione che ha un esempio fa fallire una pull request.

Il testo di aiuto viene scritto nelle pagine come HTML, non Markdown, così Lua come `g:text{...}` o
`~1_NAME~` appare come scritto. Una costante letta tramite una proprietà C#, come `engine.version`,
mostra "impostato all'avvio del server": il suo valore appartiene al processo che la legge. Il sito viene
costruito al momento della release, quindi `PublishedScriptModulesTests` controlla gli stessi requisiti nella suite di
 test: una pull request che aggiunge una funzione senza testo di aiuto fallisce lì.

La pagina [Copertura dei test](test-coverage.md) viene riempita dal rapporto di copertura in
`artifacts/coverage` (o `MOONGATE_COVERAGE_DIR`): l'importer sostituisce il
marcatore `<!-- coverage-summary -->` con la tabella per assembly e copia il rapporto HTML
in `website/public/coverage/`, servito a `https://moongate.sh/coverage/`.
`MOONGATE_COVERAGE_COMMIT` indica nella pagina il commit misurato. Senza rapporto la
pagina indica che non ne era disponibile uno. Esegui prima `scripts/coverage.sh all` per visualizzarla localmente.

Dopo aver modificato un sorgente importato mentre il server di sviluppo è attivo, esegui questo in un
secondo terminale:

```sh
npm --prefix website run prepare:docs
```

Astro osserva le pagine generate; non c'è un osservatore separato dei sorgenti
fuori da `website/`. Anche i comandi di sviluppo e build di produzione preparano prima le pagine.

## Aggiungere una guida o libreria

1. Scrivi il sorgente Markdown inglese in `docs/` o nel `README.md` della libreria.
2. Aggiungi una voce a `website/content-manifest.mjs` con percorso sorgente relativo al
   repository, slug univoco, titolo e gruppo di navigazione.
3. Esegui i test e la build di produzione sopra.

Gli slug usano lettere minuscole, cifre, trattini e segmenti separati da slash.
Usa link relativi ad altri documenti o sorgenti del repository e conserva
i frammenti delle intestazioni. I documenti importati diventano link del sito, le immagini locali vengono copiate
e altri file e directory del repository diventano link GitHub al tag della release pubblicata.
Il primo titolo di livello principale viene rimosso dal contenuto importato perché Starlight
rende il titolo dal manifest; l'ancora Markdown originale o l'ID HTML esplicito viene conservato. Anche i link assoluti GitHub esistenti a documenti importati
su `develop` o `main` vengono convertiti.

Sorgenti mancanti, slug duplicati, link locali non risolti e link rotti
nell'output costruito fanno fallire la build. Un'importazione fallita conserva il contenuto generato
precedente e la homepage scritta manualmente.

## Tradurre i contenuti scritti manualmente

I sorgenti inglesi restano nelle posizioni esistenti. Le traduzioni italiane risiedono in
`website/translations/it/<manifest-slug>.md`, con il Markdown tradotto completo
sotto un commento di metadati sulla prima riga:

```markdown
<!-- translation: {"sourceHash":"<64-character SHA-256 of the English source>","title":"Italian page title"} -->

# Translated original heading

Translated content.
```

Calcola l'hash del sorgente con `sha256sum docs/<source>.md` dopo aver revisionato la
traduzione completa rispetto a quel preciso sorgente. Conserva livelli e ordine delle intestazioni,
tutti i blocchi di codice, identificatori API, comandi, chiavi di configurazione, destinazioni dei link
e ID HTML espliciti. Traduci prosa, intestazioni, descrizioni nelle tabelle ed etichette dei link.
I link relativi vengono risolti dalla posizione del sorgente inglese, non dalla cartella della traduzione.
L'importer fornisce le ancore delle intestazioni inglesi originali affinché i frammenti esistenti continuino a funzionare.

I link tra guide scritte manualmente restano nella lingua selezionata. Guide di riferimento API Lua,
registro dei pacchetti, changelog e rapporti di copertura conservano sempre contenuti inglesi;
non aggiungere traduzioni per questi riferimenti generati. Anche esempi Lua e testo di aiuto generato
restano in inglese. La navigazione italiana collega ai riferimenti inglesi;
il selettore di lingua su quei riferimenti conduce alla homepage italiana.

Le pagine iniziali vengono scritte separatamente in `website/src/content/docs/index.mdx`
e `website/src/content/docs/it/index.mdx`. Aggiorna entrambe quando cambi la homepage.
Le etichette dei gruppi della barra laterale risiedono in `website/sidebar-translations.mjs`; Starlight fornisce
le traduzioni standard dell'interfaccia, integrate da `website/src/content/i18n/it.json`
per messaggi di ricerca ed etichette rimanenti. Le schede di download personalizzate e il piè di pagina contengono
etichette inglesi e italiane.

Esegui il controllo rigoroso del catalogo prima di pubblicare contenuti tradotti:

```sh
npm --prefix website run check:translations
```

Segnala traduzioni mancanti, hash sorgenti obsoleti, codice o link modificati
e file di traduzione inattesi. Una build normale avvisa delle traduzioni obsolete
e usa il ripiego inglese di Starlight per pagine mancanti o obsolete, con un avviso
visibile. Non aggiornare mai un hash senza revisionare e aggiornare la traduzione.
Metadati non validi o struttura di codice/link alterata fanno fallire l'importazione senza sostituire
l'ultimo output generato correttamente. Riesegui `prepare:docs` dopo aver modificato una traduzione
mentre il server di sviluppo è attivo.

## Pubblicazione delle release

Dopo la pubblicazione iniziale della sola documentazione, build e distribuzioni automatiche della documentazione
avvengono **quando il workflow di release esistente crea una release**. Commit su `develop`, push ordinari su `main` e pull
request non costruiscono né pubblicano il sito.

Le build della documentazione non aspettano mai la CI del server. Includono la copertura solo quando
un'esecuzione CI riuscita ha già un rapporto per il commit esatto che viene pubblicato.
Altrimenti la pagina di copertura indica che non era disponibile un rapporto; pubblica di nuovo dopo la conclusione
della CI per includerlo.

Il job `docs` in `.github/workflows/release.yml` chiama il workflow riutilizzabile
`.github/workflows/docs.yml`, passando SHA e tag rilasciati. Esegue il checkout
di quello SHA e mostra il tag nel titolo del sito. Questa chiamata diretta funziona anche
quando release-please crea la release con `GITHUB_TOKEN`.

I job vengono eseguiti su runner Ubuntu ospitati da GitHub. GitHub Pages deve usare **GitHub
Actions** come sorgente, e l'ambiente `github-pages` deve consentire `main`.
Il dominio personalizzato è `moongate.sh`; build e controllo link condividono il percorso base
root tramite `website/site-config.mjs`. Le distribuzioni Pages vengono serializzate. Una distribuzione fallita può essere riprovata dalla propria
esecuzione del workflow di release senza pubblicare una nuova release.

Ogni release sostituisce il sito corrente; le versioni storiche non vengono ospitate.
Le build locali usano `develop` per i link ai sorgenti e leggono la versione corrente da
`.release-please-manifest.json` per il titolo del sito. Per
visualizzare in anteprima un'etichetta di release e un riferimento ai sorgenti, imposta `MOONGATE_DOCS_VERSION` e
`MOONGATE_DOCS_REF` prima di eseguire la build.

## Pubblicare tra le release

`.github/workflows/docs.yml` viene eseguito anche da **Actions → Documentation → Run workflow**. Scegli
il branch da pubblicare e lascia entrambi gli input vuoti: il sito viene costruito da quel branch, i link ai sorgenti
puntano a `develop`, e il titolo mostra la versione in `.release-please-manifest.json`.
Per riprodurre esattamente ciò che pubblica una release, imposta `release_sha` al commit rilasciato e
`release_tag` al suo tag.

L'ambiente `github-pages` deve consentire il branch da cui parte l'esecuzione, e le distribuzioni vengono
serializzate, quindi un'esecuzione manuale e una di release non possono pubblicare contemporaneamente. Ogni pubblicazione
sostituisce l'intero sito; le versioni storiche non vengono ospitate. Dopo una pubblicazione manuale, controlla
le pagine modificate, l'indice di ricerca e i link ai sorgenti sull'URL pubblico.
