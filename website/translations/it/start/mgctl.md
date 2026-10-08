<!-- translation: {"sourceHash":"5215286c9869353f672a8186af9f4a6404aa0801d0719b84353f526da309814d","title":"mgctl, lo strumento Moongate"} -->

# mgctl, lo strumento Moongate

`mgctl` è l'unico strumento distribuito accanto a `mgserver`, il server, negli archivi delle versioni e nelle immagini
Docker; l'installatore Linux lo collega come comando `mgctl`. Le versioni fino a 0.11 chiamavano il server `Moongate.Server`. Le versioni da 0.7 a 0.11 chiamavano lo strumento
`mgboot`, che preparava solo la directory radice, e distribuivano le migrazioni e il convertitore come altri due
eseguibili, `migration-runner/Moongate.MigrationRunner` e `mg-uoxconv`; in quel caso, leggi
`mgboot <root>` al posto di `mgctl init <root>`.

| Comando | Cosa fa |
| --- | --- |
| `mgctl init <root>` | Prepara la directory radice del server; questa pagina. `mgctl <root>` fa lo stesso |
| `mgctl migrate status\|apply --target auth\|world` | Elenca o applica l'SQL versionato; vedi [Migrazioni della persistenza](persistence-migrations.md) |
| `mgctl convert uox ...` | Converte i contenuti `.dfn` di UOX3 in TOML; vedi [Migrare da UOX3](uox3-migration.md) |
| `mgctl convert modernuo-spawns\|modernuo-signs\|modernuo-teleporters\|modernuo-locations\|modernuo-chests ...` | Converte spawner, insegne, teletrasporti, luoghi con nome e casse del tesoro di ModernUO; vedi [Migrare da UOX3](uox3-migration.md#signs-of-modernuo) |
| `mgctl convert modernuo-books --source <folder> --destination <folder>` | Importa testi statici dei libri; vedi [Importare i testi dei libri](book-content-import.md) |
| `mgctl convert modernuo-vendors --source <folder> --items <folder> --mobiles <folder> --destination <folder>` | Converte i negozi dei venditori di ModernUO; vedi [Negozi](data-files/shops.md#convert-modernuos-shops) |
| `mgctl convert modernuo-guildmasters --source <folder> --items <folder> --mobiles <folder> --npc-lists <folder>` | Converte i maestri di gilda di ModernUO in template mobile e liste di PNG; vedi [Maestri di gilda](skills.md#guildmasters) |
| `mgctl completion bash\|zsh\|fish` | Stampa lo script che completa mgctl con TAB; vedi [Completamento con TAB](#tab-completion) |

`mgctl --help` elenca i comandi e `mgctl <command> --help` le opzioni di uno di essi.

## Completamento con TAB

`mgctl completion <shell>` stampa uno script di completamento per bash, zsh o fish. Con esso, TAB
completa i comandi (`mgctl mi` → `migrate`), la seconda parola di `migrate` e `convert`,
le opzioni del comando e ciò che le segue: directory dopo `--root-directory` e
simili, file dopo `--source`, `auth` o `world` dopo `--target`, e una directory per
la radice di `init`.

L'[installatore Linux](installation.md) colloca gli script dove le shell li cercano, quindi
una nuova shell completa mgctl senza altre operazioni. Altrove, caricalo personalmente:

```sh
eval "$(mgctl completion bash)"      # bash: add the line to ~/.bashrc
source <(mgctl completion zsh)       # zsh: add the line to ~/.zshrc, after compinit
mgctl completion fish | source       # fish: or save it as ~/.config/fish/completions/mgctl.fish
```

La riga bash usa `eval` perché bash 3.2 di macOS non carica nulla da
`source <(...)`; lì, un percorso con uno spazio viene completato senza le relative virgolette.

Lo script fish è generato dallo stesso elenco di comandi degli altri due ma, a differenza
di essi, non viene esercitato dai test.

## Preparare la directory radice del server

`mgctl init` prepara una directory dati Moongate senza avviare il server né connettersi
a PostgreSQL. È il passo 1 della [sequenza del primo avvio](getting-started.md#first-start).

## Uso

```sh
mgctl init /absolute/path/to/moongate-data
```

La directory radice è l'unico argomento posizionale obbligatorio. I percorsi relativi vengono
risolti dalla directory di lavoro corrente. Racchiudi tra virgolette i percorsi contenenti spazi:

```sh
mgctl init "/srv/my realm"
mgctl init --help
```

`mgctl <root>`, senza `init`, è la forma precedente e funziona ancora per una radice scritta come
percorso (`/srv/moongate`, `./data`) o che indica una directory esistente; un nuovo nome semplice come
`mgctl data` viene rifiutato, così un comando scritto male non diventa mai una directory radice. Una riga di comando
che non indica alcun comando, come `mgctl migrate` da solo, termina con codice 2.

Su Windows, usa `mgctl.exe init C:\MoongateData` dalla distribuzione estratta.
Mantieni insieme `mgctl` e `mgserver` della stessa versione.
Quando prepara una directory radice, `mgctl` mostra la stessa intestazione Moongate, versione e nome in codice
del server, seguiti da `Root setup`. L'output di aiuto e versione omette l'intestazione, così come
`mgctl init <root> --no-header`: `scripts/run_server.sh` lo usa perché il server che avvia
subito dopo mostra già l'intestazione.

## Generare un certificato di amministrazione

```sh
mgctl init /srv/moongate --generate-admin-certificate \
  --admin-certificate-hosts "login.example.test,192.0.2.10"
```

La CLI usa ConsoleAppFramework. `--generate-admin-certificate` è un flag opzionale
attivato dalla sua presenza. `--admin-certificate-hosts` è un'unica stringa separata da virgole e
richiede quel flag. Ometti l'opzione degli host per lo sviluppo locale: ogni certificato
include automaticamente `localhost`, `127.0.0.1` e `::1`. Le voci aggiuntive devono
essere nomi DNS o indirizzi IP, senza schemi URL, porte o indirizzi jolly.
Usa i nomi a cui si connettono i client; `*`, `0.0.0.0` e `::` sono indirizzi di ascolto,
non identità del certificato.

L'operazione viene eseguita offline e crea un certificato server RSA autofirmato valido
per un anno. Scrive questi file sotto la directory radice:

| File | Contenuto |
| --- | --- |
| `certificates/admin.pfx` | Certificato server e chiave privata; nessuna password; lettura/scrittura solo per il proprietario su Unix |
| `certificates/admin.crt` | Certificato PEM pubblico da considerare attendibile nei client di amministrazione |

Aggiorna queste quattro impostazioni in `config/moongate.toml`:

```toml
[admin_api]
enabled = true
allow_insecure_loopback = false
certificate_path = "certificates/admin.pfx"
certificate_password = ""
```

La porta esistente, l'indirizzo di ascolto, le altre impostazioni e i commenti vengono conservati.
Una sezione mancante viene aggiunta; le definizioni `admin_api` esistenti inline o con chiavi puntate devono
prima essere convertite in una sezione `[admin_api]` esplicita. Un percorso di certificato personalizzato
non viene mai sostituito implicitamente: cancella esplicitamente quell'impostazione solo se intendi
cambiare identità.

Nessuna porta viene aperta durante la configurazione. L'API parte al successivo avvio normale del server.
L'indirizzo di ascolto predefinito rimane `127.0.0.1:2590`; per l'accesso dalla rete privata, configura
un'interfaccia raggiungibile o `listen_address = "*"` e includi il DNS/IP effettivamente usato dai client
negli host del certificato. Vedi [API di amministrazione](admin-api.md).

Eseguire di nuovo lo stesso comando riusa una coppia di certificati corrispondenti e non scaduti. Fallisce
senza sovrascrivere la coppia se uno dei file è mancante, non valido, scaduto,
non corrispondente o privo di un nome richiesto. Non esiste rinnovo o rotazione implicita.
Per una sostituzione deliberata, arresta il server, sposta entrambi i file di certificato esistenti
in una posizione di backup protetta, esegui di nuovo la generazione e aggiorna la fiducia dei client prima
di riavviare. Un PFX senza password contiene comunque la chiave privata: conservalo sul server
e limita l'accesso. Su Windows, usa un'ACL appropriata per l'account di servizio.
Distribuisci solo `admin.crt`; i client devono verificare attendibilità e nome host.

## Cosa crea

| Percorso sotto la directory radice | Scopo |
| --- | --- |
| `config/moongate.toml` | Valori predefiniti attuali del server serializzati come TOML snake_case; le sezioni dei plugin come `[ultima]` vengono aggiunte al primo avvio del server |
| `logs/`, `plugins/` | Directory standard del server |
| `migrations/auth/` | I file SQL auth principali inclusi nella distribuzione |
| `migrations/world/` | I file SQL World principali inclusi nella distribuzione: le tabelle di mobile, oggetti e stato del mondo |
| `data/` | I file di dati dello shard inclusi nella distribuzione: mappe, regioni, razze, abilità, messaggi e il resto; vedi [File di dati dello shard](data-files.md) |
| `templates/` | I template di oggetti, bottino e mobile inclusi nella distribuzione; vedi [Template](templates.md) |
| `scripts/` | Gli script di esempio per [mobile](scripting/mobile-scripts.md) e [oggetti](scripting/item-scripts.md) inclusi nella distribuzione, `mobiles/wander.lua` e `items/potion.lua`; il motore scrive qui `definitions.lua` e `.luarc.json` all'avvio |
| `.mgctl.lock` | File conservato per impedire inizializzazioni simultanee |

La nuova configurazione imposta `persistence.migrations_directory` al percorso assoluto `migrations`
dentro questa directory radice. Anche il normale avvio del server usa `<root>/migrations` per impostazione predefinita
quando l'impostazione è assente; mantenere il percorso esplicito fa usare a
`mgctl migrate` lo stesso catalogo. Gli altri valori rimangono quelli predefiniti del server: generazione automatica delle migrazioni
e sincronizzazione automatica dello schema sono disabilitate. Le credenziali Redis di runtime
devono essere fornite prima di avviare il server.
La directory radice non richiede accesso ai database né file del client Ultima Online per essere preparata.

File di dati, template e script vengono copiati solo se mancanti, quindi un file modificato
rimane com'è. Esegui di nuovo `mgctl init` dopo un aggiornamento per aggiungere i file introdotti da una nuova
versione; un file esistente nella directory radice non viene mai sostituito, quindi confrontalo con
quello accanto al nuovo binario `mgserver` (`data/`, `templates/`, `scripts/`) per integrare
le modifiche a monte. Non viene rimosso nulla: un template rinominato o spostato da una versione
rimane nella directory radice accanto alla sua nuova copia e il server si arresta all'avvio per
l'id duplicato, quindi elimina il file obsoleto. Un file distribuito che hai eliminato ritorna alla
successiva esecuzione; svuotalo invece per mantenerlo escluso.

La preparazione delle migrazioni di base copia l'SQL versionato distribuito con Moongate.
Non genera nuovo SQL dalle entità, non carica plugin, non crea database e non applica
migrazioni. Usa gli [strumenti di persistenza](persistence-migrations.md#automatic-development-migrations)
per le modifiche alle entità dopo l'inizializzazione.

## Directory esistenti

Eseguire di nuovo il comando senza opzioni di certificato conserva la configurazione e i file esistenti. Le
migrazioni distribuite mancanti vengono aggiunte solo quando il catalogo esistente è compatibile. Una
migrazione esistente con lo stesso numero di sequenza ma un nome di file o
checksum diverso interrompe l'inizializzazione prima che vengano scritti la configurazione o i file SQL.
L'errore identifica il file in conflitto. Non eliminare la cronologia delle migrazioni per
aggirarlo; riconcilia il catalogo o scegli una nuova directory radice.

Senza `--generate-admin-certificate`, una configurazione esistente non viene mai riscritta, compresa l'impostazione della directory delle migrazioni.
Controlla personalmente l'impostazione se punta fuori da questa directory radice. Se sposti la directory radice,
aggiorna di conseguenza il percorso assoluto in una configurazione appena generata.

## Passi successivi

Prosegui con [Avviare un server Moongate](getting-started.md#first-start): modifica la
configurazione generata, crea i database PostgreSQL, applica l'SQL distribuito con
`mgctl migrate apply` e avvia il server. Per lo sviluppo, puoi invece abilitare
`persistence.auto_generate_migrations` dopo aver preparato i database; vedi il
[tutorial sulle entità](persistence-entity-tutorial.md).

## Docker

Per un'immagine di una versione:

```sh
docker volume create moongate-data
docker run --rm --entrypoint /app/mgctl \
  --mount type=volume,source=moongate-data,target=/data \
  ghcr.io/moongate-community/moongate:latest init /data
```

Le immagini fino a 0.11.0 hanno invece `/app/mgboot`: usa `--entrypoint /app/mgboot` e passa
solo `/data`. L'inizializzazione termina dopo aver preparato la directory radice montata; non apre alcun listener
TCP.

## Compilare dai sorgenti

Pubblica server e strumento nella stessa directory:

```sh
dotnet publish src/Moongate.Server -c Release -r linux-x64 -o dist/moongate
dotnet publish src/Moongate.Ctl -c Release -r linux-x64 -o dist/moongate
./dist/moongate/mgctl init /absolute/path/to/moongate-data
```

Cambia l'identificatore di runtime per la tua piattaforma. Il server espone inoltre la stessa
operazione offline per lo sviluppo dai sorgenti:

```sh
dotnet run --project src/Moongate.Server -- --initialize-root --root-directory /absolute/path/to/moongate-data
```

Le stesse opzioni di certificato funzionano con l'operazione offline del server:

```sh
dotnet run --project src/Moongate.Server -- --initialize-root \
  --root-directory /srv/moongate --generate-admin-certificate \
  --admin-certificate-hosts "login.example.test,192.0.2.10"
```

Il server rifiuta le opzioni di certificato al di fuori di `--initialize-root`.

`MOONGATE_SERVER_EXECUTABLE` può sostituire l'eseguibile del server adiacente per i
test locali degli strumenti. Deve riferirsi a un eseguibile compatibile della stessa versione dei sorgenti.
