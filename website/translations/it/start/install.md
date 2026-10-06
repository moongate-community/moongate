<!-- translation: {"sourceHash":"550ab2035434c19946614d1b1dd2013f37d3d0348bcd4e396f04a46dc7fb0ea6","title":"Installare su Linux"} -->

# Installare su Linux

Questa pagina installa il binario del server rilasciato. Per eseguire invece il container pubblicato, usa
[Eseguire con Docker](docker.md); per compilare dai sorgenti, usa [Primo avvio](getting-started.md).

## Installazione

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

Lo script individua l'ultima versione, scarica l'archivio per questa macchina, lo verifica
rispetto al checksum pubblicato insieme ad esso e lo installa:

| Percorso | Contenuto |
| --- | --- |
| `/opt/moongate/` | Il contenuto dell'archivio: il binario del server, l'SQL principale in `migrations/`, i dati dello shard in `data/`, i template in `templates/`, gli script di esempio in `scripts/`, [`mgctl`](mgctl.md) (`mgboot`, con il runner delle migrazioni in `migration-runner/`, nelle versioni da 0.7 a 0.11), `LICENSE`, `THIRD-PARTY-NOTICES.md` e i simboli di debug |
| `/usr/local/bin/moongate` | Un collegamento simbolico a `/opt/moongate/mgserver`, il server (`Moongate.Server` nelle versioni fino a 0.11) |
| `/usr/local/bin/mgctl` | Un collegamento simbolico a `/opt/moongate/mgctl`, quando la versione lo contiene |
| `/usr/share/bash-completion/completions/mgctl`, `/usr/local/share/zsh/site-functions/_mgctl` (oppure, in assenza di quella directory, `/usr/share/zsh/vendor-completions/_mgctl` o `/usr/share/zsh/site-functions/_mgctl`), `/usr/share/fish/vendor_completions.d/mgctl.fish` | Il [completamento con TAB](mgctl.md#tab-completion) di mgctl, un file per ogni shell la cui directory esiste; uno script che non può essere scritto viene saltato senza far fallire l'installazione |

Entrambe le posizioni richiedono root. Esegui la riga come root, oppure lascia che intervenga `sudo`, che lo script
usa autonomamente quando non viene eseguito come root. Non viene creato altro: nessun servizio, nessun utente di sistema
e nessuna directory dei dati, perché scegli dove collocare la directory radice del server al primo avvio.

Le versioni distribuiscono `linux-x64` e `linux-arm64`. Entrambi i binari sono autonomi, quindi la macchina
non richiede il runtime .NET. I sistemi che usano musl, tra cui Alpine, vengono rifiutati: il binario è compilato
per glibc. Su macOS e Windows, scarica l'archivio per la tua piattaforma dalla
[pagina delle versioni](https://github.com/moongate-community/moongate/releases), oppure usa Docker.

## Passo successivo: primo avvio

Non eseguire mai il server dentro `/opt/moongate`. L'aggiornamento sostituisce tutta la directory,
quindi configurazione, plugin e file generati conservati lì vanno persi alla successiva esecuzione della
riga di installazione; come utente ordinario il tentativo fallisce comunque con
`Access to the path '/opt/moongate/moongate.pid.lock' is denied`. Assegna al server una
directory radice propria e preparala:

```sh
sudo mkdir -p /srv/moongate && sudo chown "$USER" /srv/moongate
mgctl init /srv/moongate
```

`mgctl` è distribuito nelle versioni successive a 0.11.0 (`mgboot /srv/moongate` nelle versioni da 0.7 a 0.11); per 0.6.0 la guida al primo avvio mostra i
passaggi manuali equivalenti. Poi segui [Avviare un server Moongate](getting-started.md#first-start): modifica il
file generato `config/moongate.toml`, crea i due database PostgreSQL, applica le
migrazioni principali con `mgctl migrate apply`
e avvia con `moongate --root-directory /srv/moongate`.

## Aggiornamento

Esegui di nuovo la stessa riga. La nuova versione viene preparata accanto a quella attuale e sostituita tramite una
rinomina, quindi un download fallito o un checksum errato lasciano intatta l'installazione in uso. Arresta
prima il server: la sostituzione del binario mentre il processo è attivo non è supportata.

Dopo l'aggiornamento esegui di nuovo `mgctl init /srv/moongate` per aggiungere i file distribuiti dalla nuova versione, poi
`mgctl migrate apply --root-directory /srv/moongate --target auth` e lo stesso con
`--target world`: il server rifiuta di avviarsi finché ci sono migrazioni in sospeso, a meno che
[`persistence.auto_apply_migrations`](persistence-migrations.md#apply-at-startup) sia attivo, nel qual caso
l'avvio le copia e le applica autonomamente.

L'aggiornamento sostituisce completamente `/opt/moongate` ed elimina la copia che aveva spostato da parte. Niente che
vuoi conservare deve stare lì, motivo per cui la directory radice del server va collocata altrove.

## Rimozione

```sh
sudo rm -rf /opt/moongate /usr/local/bin/moongate /usr/local/bin/mgctl /usr/local/bin/mgboot
sudo rm -f /usr/share/bash-completion/completions/mgctl /usr/local/share/zsh/site-functions/_mgctl \
  /usr/share/zsh/vendor-completions/_mgctl /usr/share/zsh/site-functions/_mgctl \
  /usr/share/fish/vendor_completions.d/mgctl.fish
```

La directory radice del tuo server rimane intatta sia con l'installatore sia con questa riga.

## Opzioni

Lo script legge queste variabili d'ambiente:

| Variabile | Valore predefinito | Scopo |
| --- | --- | --- |
| `MOONGATE_VERSION` | l'ultima versione | Installare una versione specifica, come `0.6.0`; è accettata una `v` iniziale |
| `MOONGATE_RID` | rilevato da `uname -m` | `linux-x64` o `linux-arm64` |
| `MOONGATE_BASE_URL` | i download delle versioni GitHub | Un mirror che contiene gli stessi nomi di file |
| `MOONGATE_INSTALL_DIR` | `/opt/moongate` | Dove va il contenuto dell'archivio |
| `MOONGATE_BIN_DIR` | `/usr/local/bin` | Dove va il collegamento simbolico `moongate` |
| `MOONGATE_BASH_COMPLETION_DIR`, `MOONGATE_ZSH_COMPLETION_DIR`, `MOONGATE_FISH_COMPLETION_DIR` | la directory propria della shell | Dove va il completamento con TAB di mgctl; un'installazione senza root salta le directory di sistema, quindi impostale su directory di tua proprietà, come `~/.local/share/bash-completion/completions` |

Impostare `MOONGATE_INSTALL_DIR` e `MOONGATE_BIN_DIR` su percorsi di tua proprietà consente di installare senza root:

```sh
curl -fsSL https://moongate.sh/install.sh |
  MOONGATE_INSTALL_DIR="$HOME/.local/lib/moongate" MOONGATE_BIN_DIR="$HOME/.local/bin" sh
```

## Leggilo prima di eseguirlo

Passare uno script a una shell tramite pipe esegue qualsiasi contenuto fornito da quell'URL. Il file è
[`scripts/install.sh`](../scripts/install.sh) in questo repository e il sito lo serve
invariato, quindi puoi leggere la copia che stai per eseguire:

```sh
curl -fsSL https://moongate.sh/install.sh | less
```

Per evitare del tutto lo script, scarica l'archivio e il suo checksum dalla pagina delle versioni e
verificalo personalmente:

```sh
sha256sum -c moongate-linux-x64-0.6.0.tar.gz.sha256
tar -xzf moongate-linux-x64-0.6.0.tar.gz
```

## Quando rifiuta di procedere

Ogni rifiuto stampa una riga che inizia con `moongate:`. Fino a `could not install into`,
non è stato installato nulla; i messaggi dei collegamenti simbolici successivi arrivano quando i file sono già al loro posto.

| Messaggio | Significato |
| --- | --- |
| `this installer supports Linux only` | Usa Docker o l'archivio per la tua piattaforma |
| `unsupported architecture '...'` | Le versioni distribuiscono `linux-x64` e `linux-arm64` |
| `musl libc is not supported; the release binary needs glibc` | Alpine e gli altri sistemi musl richiedono l'immagine container |
| `curl or wget is required`, `tar is required`, `sha256sum or shasum is required` | Installa lo strumento indicato |
| `root privileges are required` | Esegui di nuovo con `sudo`, oppure imposta `MOONGATE_INSTALL_DIR` e `MOONGATE_BIN_DIR` |
| `could not resolve the latest release; set MOONGATE_VERSION` | La pagina delle versioni non ha reindirizzato a un tag di versione; fissane uno con `MOONGATE_VERSION` |
| `release v... has no asset for ...`, `release v... has no checksum for ...` | Quella versione non ha un archivio o un file di checksum per questa architettura, oppure il download stesso è fallito; `linux-arm64` esiste dalla versione 0.4.1 in poi |
| `checksum mismatch for ...` | Il download non corrisponde al checksum pubblicato; non è stato installato nulla |
| `the archive could not be extracted`, `the archive does not contain moongate-.../mgserver or moongate-.../Moongate.Server` | L'archivio scaricato è danneggiato o ha una struttura inattesa |
| `could not clear a leftover staging directory beside ...`, `could not create ...`, `could not stage the new files in ...`, `could not make ... executable`, `could not move the current installation aside; ... is untouched`, `could not install into ...` | Il filesystem ha rifiutato un passaggio dell'installazione, per esempio per un disco pieno. Non viene installato nulla di nuovo, un aggiornamento conserva l'installazione precedente e il messaggio indica dove si trova |
| `could not replace .../moongate`, `could not link .../moongate` | I nuovi file sono al loro posto sotto `/opt/moongate`, ma non è stato possibile sostituire il collegamento simbolico `moongate`. Sistema la directory dei binari ed esegui di nuovo la riga, oppure crea il collegamento personalmente con `sudo ln -sfn /opt/moongate/mgserver /usr/local/bin/moongate` |
| `could not replace .../mgctl`, `could not link .../mgctl`, `could not remove obsolete .../mgctl link` | I file del server sono al loro posto; solo il collegamento simbolico `mgctl` non è stato aggiornato o rimosso. Sistema la directory dei binari ed esegui di nuovo la riga, oppure collegalo personalmente con `sudo ln -sfn /opt/moongate/mgctl /usr/local/bin/mgctl` |
