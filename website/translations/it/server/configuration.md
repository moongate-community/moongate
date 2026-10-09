<!-- translation: {"sourceHash":"f4eb50634f57ab726fbe8aea72debab9ff860fd008ac40a6ab35e7df317e1a03","title":"Configurazione"} -->

# Configurazione del server

L'host carica `<root>/config/moongate.toml` con `ConfigHelper.Load`. Se il file manca,
lo crea insieme alla directory padre usando i valori predefiniti. I file esistenti
vengono deserializzati e validati senza riscriverli; le proprietà omesse mantengono
i valori predefiniti del modello. TOML non valido, impostazioni non valide ed errori
del filesystem impediscono l'avvio. Le modifiche hanno effetto all'avvio successivo;
non è previsto un ricaricamento della configurazione.

Il testo di benvenuto mostrato quando un personaggio entra nel mondo si trova in un
[file `data/motd.toml` separato](motd.md), con proprie regole per variabili e validazione.

## Configurazione predefinita completa

Le chiavi TOML usano `snake_case`. Mantieni `mode` prima della prima intestazione di tabella.

Il server gestisce `mode` e le sezioni `[shard]`, `[network]`, `[redis]`,
`[persistence]`, `[realm_directory]`, `[world_save]`, `[sql_backup]`, `[diagnostics]` e
`[scripting]`. Le altre appartengono ai plugin: `[ultima]` al plugin Ultima e
`[admin_api]` al plugin Administration. Una nuova directory radice scritta da `mgctl` contiene
le sezioni del server (e `[admin_api]` quando configura un certificato); al primo avvio
ogni plugin aggiunge in fondo al file la propria sezione mancante, con i valori predefiniti.
Vedi [Aggiungere una sezione di configurazione](plugins.md#add-a-config-section).

```toml
mode = "standalone" # Runs login and game services together.

[shard]
shard_name = "Moongate"

[network]
login_port = 2593
game_port = 2595
listen_address = "0.0.0.0"
enable_ping_server = true # Answers UDP pings on ping_port.
ping_port = 12000

[network.encryption]
mode = "Disabled"
client_version = ""

[redis]
connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
handoff_secret = "$MOONGATE_HANDOFF_SECRET"

# Administration plugin.
[admin_api]
enabled = false
# Use "*" or "0.0.0.0" for all IPv4 interfaces (TLS required).
listen_address = "127.0.0.1"
port = 2590
session_lifetime_minutes = 30
max_receive_message_bytes = 65536
max_concurrent_calls = 64
allow_insecure_loopback = false
certificate_path = ""
certificate_password = ""

# Ultima plugin.
[ultima]
ultima_path = "ChangeMe" # Replace with your client data directory.

[ultima.localization]
language = "eng" # Reads <root>/data/messages/eng.toml and eng/*.toml.

[ultima.line_of_sight]
max_distance = 25 # Farthest cells along X or Y a point can see.

[ultima.world]
view_range = 18 # How far players see mobiles and items, in cells along X or Y.
seconds_per_uo_minute = 5 # Real seconds a game minute lasts: 5 makes a game day last 2 real hours.
day_light = 0 # Light level of the day, from 0 (brightest) to 31.
night_light = 12 # Light level of the night, from 0 (brightest) to 31.
dungeon_light = 26 # Light level inside a dungeon region, from 0 (brightest) to 31.
jail_light = 9 # Light level inside a jail region, from 0 (brightest) to 31.
lamp_post_light = 6 # Light level from which the town lamp posts are lit, from 0 to 31.
season_rotation = false # true: the maps change season every days_per_season game days.
days_per_season = 12 # Game days a season lasts when they rotate, from 1 to 365.
pathfinding_range = 38 # How far apart the two ends of a path search may be, in tiles, from 8 to 64.
pathfinding_max_nodes = 1000 # Places a path search looks at before it gives up, from 50 to 20000.

[ultima.items]
backpack_template = "0x0e75_backpack" # Item template of the backpack of new characters and spawned NPCs.
gold_template = "0x0eed_gold_coin"    # Item template of gold coins.

[ultima.starting_items]
best_skills = 3                       # How many of the highest skills pick skill sets.

[ultima.characters]
max_per_account = 7                   # Characters an account may hold: 1, 5, 6 or 7.
deletion_delay_hours = 24             # Hours before a deleted character may be removed.

[ultima.npcs]
think_interval_ms = 500               # Milliseconds between two thinks of an NPC near a player.
sense_range = 8                       # Cells within which an NPC's script senses another mobile.

[ultima.regeneration]
hits_seconds = 11.0                   # Seconds between two hit points coming back.
stamina_seconds = 7.0                 # Seconds between two points of stamina coming back.
mana_seconds = 7.0                    # The same for mana, with no intelligence and no Meditation.
hunger_enabled = true                 # Players get hungry; starving, they get no hit points back.
thirst_enabled = true                 # Players get thirsty; parched, they get no stamina back.
hunger_minutes = 5                    # Minutes between two points of hunger, and of thirst, lost by a player.
fatigue_enabled = true                # Running and moving overloaded cost a player stamina.

[ultima.skills]
total_cap = 700                       # Points the skills of a player add up to at most.
gain_enabled = true                   # A skill, and a stat, may rise when a skill is tried.
stat_max = 100                        # The most a player's strength, dexterity or intelligence reaches by use.
stat_cap = 225                        # The most the three stats of a player add up to.
stat_gain_minutes = 10                # Minutes a stat waits after it was tried before it is tried again.

[ultima.combat]
global_attack_speed = 1.0             # Divides the delay between swings: 2 swings twice as often.
attack_stamina = 0                    # Stamina a swing costs a player; 0 costs nothing.
npc_damage_rate = 1.0                 # Divides the damage an NPC does to a player.
max_range = 1                         # Tiles a melee swing reaches.
combatant_seconds = 60                # Seconds a fighter keeps its target without swinging.
display_damage_numbers = true         # The damage shows over the one hit.
blood_enabled = true                  # A hit that does damage leaves blood on the ground.
blood_pieces = 2                      # Most pieces around the one under the victim, from 0 to 8.
blood_seconds = 5                     # Seconds a piece lies on the ground, from 1 to 60.
archery_stand_still_seconds = 1.0     # Seconds a player must have stood still before it shoots; 0 for none.

[ultima.crime]
criminal_seconds = 120                # How long a mobile stays a criminal after its last criminal act.
guards_enabled = true                 # Saying "guards" in a guarded region brings a guard beside a criminal.
guard_template = "guard"              # The mobile template of a guard that is called.
archer_guard_template = "archerguard" # The one called in Ilshenar and Malas.
guard_seconds = 40                    # How long a called guard stays.

[ultima.murder]
short_term_hours = 8                  # Hours before a short-term murder is forgotten.
long_term_hours = 40                  # Hours before a reported kill is forgotten.
report_delay_seconds = 4              # Seconds after a death before the victim is asked to report.
recently_reported_minutes = 10        # Minutes before the same victim can report the same killer again.
aggressor_seconds = 120               # Seconds an attack on an innocent keeps the attacker reportable.

[ultima.spawns]
initial_fill = true                   # The first spawn of each region after the start fills it to its max.

[ultima.jail]
fine_gold = 500                       # Gold coins taken from a prisoner when its jail sentence ends; 0 takes nothing.
max_days = 30                         # The longest sentence the jail gump accepts, in real days.

[ultima.help]
stuck_wait_seconds = 5                # The seconds a character must stand still before "I am stuck" moves it.
stuck_cooldown_minutes = 10           # The minutes before a player can use "I am stuck" again; 0 allows it at once.
page_cooldown_seconds = 60           # The seconds between two requests of one player to the game masters; 0 allows it at once.
page_history_days = 30                # The days a closed request is kept before it is deleted at startup.

[ultima.schedule]
time_zone = ""                        # The IANA time zone of the hours in data/schedule.toml, such as Europe/Rome; empty is the zone of the system.

[ultima.bulletin_boards]
expire_days = 7                       # A thread of a bulletin board goes this many days after its last reply; 0 keeps it.
max_messages = 50                     # The messages a board holds; its oldest thread goes when it is full.
thread_seconds = 120                  # The wait between two new threads of one character on a board.
reply_seconds = 30                    # The wait between two posts of one character on a board.

[ultima.bank]
max_items = 125                       # Items a bank box holds, bags included; 0 for no limit.
max_withdraw = 60000                  # Coins a banker hands out at one time.
min_check = 5000                      # The smallest bank check a banker writes.
max_check = 1000000                   # The largest.

[ultima.stable]
max_pets = 10                         # Pets a player may leave with the stablemasters, from 1 to 50.
fee = 30                              # Gold a pet costs when it is stabled, from the backpack and then the bank; 0 makes it free.

[persistence]
auto_sync_schema = false
auto_apply_migrations = false         # true: a start adds the bundled core SQL and applies what is pending.

[persistence.accounts]
connection_string = "postgres://moongate:moongate@localhost:5432/auth"

[persistence.realm]
connection_string = "postgres://moongate:moongate@localhost:5432/world"

[realm_directory]
realm_id = ""
name = ""
server_index = 0
advertised_address = ""
advertised_port = 0
minimum_account_type = "regular"
heartbeat_interval_seconds = 5
lease_duration_seconds = 15
max_realms = 128

[world_save]
enabled = true # Enables periodic saves; manual/final saves remain available.
interval_seconds = 300

[sql_backup]
enabled = false # Scheduled SQL backups; the sql_backup command works either way.
interval_minutes = 1440
directory = "backups" # Relative to the server root.
keep = 5 # Copies kept for each database.

[diagnostics]
enabled = true
interval_seconds = 5
log_metrics = false

[scripting]
bootstrap_file = "init.lua" # Relative to <root>/scripts.
max_instructions_per_resume = 150000
max_instructions_per_chunk = 10000000
hook_interval = 1000
write_definitions = true
max_string_length = 16777216
```

Solo i database del ruolo attivo devono già esistere e accettare connessioni:
Accounts per `login`, Realm per `game` ed entrambi per `standalone`.
I valori predefiniti usano le credenziali di sviluppo locali `moongate` / `moongate`;
un file di configurazione esistente non viene riscritto. Per il deployment, imposta
ogni `connection_string` su un riferimento d'ambiente fornito dal gestore dei segreti,
come `$MOONGATE_ACCOUNTS_DATABASE` o `$MOONGATE_REALM_DATABASE`. Esportare queste variabili
non sostituisce automaticamente un URI letterale nel file TOML.

`MoongatePersistenceService` apre ogni database attivo ed esegue `SELECT 1`. Anche il
servizio Redis verifica la connessione prima di accettare client. Ogni connessione
PostgreSQL riuscita registra `Postgres connection successful` con destinazione ed endpoint,
senza credenziali. Un errore di connessione o ping genera un'eccezione e impedisce
l'avvio degli altri servizi. Moongate non crea i database mancanti; i controlli dello
schema e delle migrazioni vengono eseguiti dopo quelli delle connessioni.
Vedi [Persistenza PostgreSQL](persistence.md).

## Impostazioni e validazione

| Impostazione | Significato e limiti |
| --- | --- |
| `mode` | `login`, `game` o `standalone`; il valore predefinito è standalone. Login esegue autenticazione degli account, listener dei pacchetti di login e directory dei realm; Game esegue i servizi del mondo e pubblica il proprio realm su Redis; Standalone esegue entrambi i ruoli e pubblica il realm locale su Redis. Vedi [Tipi di valore TOML](toml-types.md#enums). |
| `shard.shard_name` | Metadati del nome visualizzato dello shard; usato come nome nell'elenco standalone quando rientra nel limite ASCII di 32 caratteri del protocollo. Altrimenti il nome dell'elenco locale è `Moongate`. |
| `network.login_port` | Porta del listener TCP di login; valore predefinito 2593. Usata nelle modalità login e standalone. |
| `network.game_port` | Porta del listener TCP di gioco; valore predefinito 2595. Usata nelle modalità game e standalone. Standalone rifiuta porte di login e gioco uguali. |
| `network.listen_address` | Indirizzo IP letterale, non nome host DNS. `0.0.0.0` fa enumerare all'host gli indirizzi unicast locali e creare un endpoint per ogni ruolo attivo su ogni indirizzo, compresi gli indirizzi IPv6; non è un singolo listener wildcard. Standalone avvia quindi due listener per indirizzo. Usa un IP specifico per limitare il binding. |
| `network.enable_ping_server` | Valore predefinito true. Avvia il server ping UDP in ogni modalità: rimanda al mittente ogni datagramma di massimo 64 byte, così un client può misurare la latenza dello shard; un datagramma più grande non riceve risposta, né uno proveniente dalla porta ping stessa (l'eco di un altro server ping). Esegue il binding di `network.ping_port` sugli indirizzi di `network.listen_address`. Se il binding di un indirizzo o una porta fallisce, viene registrato un avviso e il server si avvia comunque. Non influisce sul gestore dei pacchetti ping UO sulla connessione di gioco. |
| `network.ping_port` | Porta UDP del server ping; valore predefinito 12000, la porta usata da ModernUO. Deve essere compresa tra 1 e 65535 quando il server ping è abilitato. |
| `network.encryption.mode` | `Disabled` (predefinito), `Optional` o `Required`; si applica a entrambi i listener UO. Vedi [Cifratura del client UO](#uo-client-encryption). |
| `network.encryption.client_version` | Versione grezza del protocollo POL usata per derivare le chiavi di login e scegliere la famiglia di cifratura del gioco. Obbligatoria per `Optional` e `Required`; ignorata con `Disabled`. Vuota per impostazione predefinita. |
| `ultima.ultima_path` | Directory dei dati client esistente e leggibile. Si applicano l'espansione dei percorsi e dell'ambiente; i percorsi relativi usano la directory di lavoro del processo. Deve contenere `tiledata.mul`, i file delle mappe e degli statici di ogni mappa in `data/maps.toml`, e `MultiCollection.uop` oppure `multi.idx` con `multi.mul`; il server interrompe l'avvio se ne manca uno. |
| `persistence.auto_sync_schema` | Valore predefinito false. L'avvio normale controlla la cronologia SQL versionata; quando è false fallisce anche se le entità registrate richiedono DDL. Genera e revisiona l'SQL, quindi applicalo con il runner separato delle migrazioni. Abilitalo solo come comodità esplicita per lo sviluppo. |
| `persistence.auto_apply_migrations` | Valore predefinito false. Quando è true, prima del controllo dello schema l'avvio esegue per le migrazioni ciò che fanno `mgctl init` e `mgctl migrate apply`: aggiunge alla directory delle migrazioni l'SQL core distribuito con questa versione e assente nella radice, senza sostituire nulla, poi applica l'SQL revisionato in sospeso dei database usati dal processo, compreso quello dei bundle dei plugin in `plugins/`. Non scrive mai SQL dalle entità. Il ruolo database della connessione deve poter modificare lo schema, e `mgctl` deve trovarsi accanto al server, come in ogni distribuzione. Un file della radice con il numero di uno distribuito ma nome o contenuto diverso interrompe l'avvio. È incompatibile con `auto_sync_schema`. Vedi [Applicare all'avvio](persistence-migrations.md#apply-at-startup). |
| `persistence.accounts.connection_string` | URI PostgreSQL Accounts/login oppure riferimento d'ambiente `$NAME` / `${NAME}`. Risolto solo quando le entità registrate usano Accounts. |
| `persistence.realm.connection_string` | URI PostgreSQL di questo realm oppure riferimento d'ambiente `$NAME` / `${NAME}`. Risolto solo quando le entità registrate usano Realm. |
| `redis.connection_string` | Endpoint e password Redis condivisi in formato StackExchange.Redis, oppure riferimento d'ambiente `$NAME` / `${NAME}`. Obbligatorio per ogni ruolo runtime. |
| `redis.handoff_secret` | Segreto di prova separato e comune al cluster oppure riferimento d'ambiente. Obbligatorio per ogni ruolo runtime; almeno 32 byte dopo la codifica UTF-8. Non riutilizzare mai la password Redis. |
| `realm_directory.realm_id` | ID stabile di un realm di gioco e del namespace dei suoi lease/ticket Redis. Standalone usa `local` per impostazione predefinita; assegna un ID distinto a ogni realm eseguito indipendentemente. |
| `realm_directory.name`, `server_index` | Nome ASCII nell'elenco (massimo 32 caratteri) e indice univoco (0–65535). Standalone usa il nome dello shard e indice zero; imposta un indice distinto per ogni realm che condivide Redis. |
| `realm_directory.advertised_address`, `advertised_port` | Indirizzo IPv4 letterale e porta rivolti ai client. Obbligatori in modalità game; standalone usa loopback e `network.game_port`. `0xA8` trasporta l'indirizzo; `0x8C` la porta del realm selezionato. |
| `realm_directory.minimum_account_type` | Livello minimo dell'account per vedere il realm; `regular`, `game_master` o `administrator`. Vedi [Tipi di valore TOML](toml-types.md#enums). |
| `realm_directory.heartbeat_interval_seconds`, `lease_duration_seconds`, `max_realms` | Valori predefiniti 5, 15 e 128. La durata del lease deve superare due heartbeat; la directory basata su Redis limita i realm a 128. |
| `world_save.enabled` | Avvia il salvataggio automatico periodico quando è true. Non disabilita i salvataggi espliciti o il salvataggio finale di arresto quando applicabile. |
| `world_save.interval_seconds` | Secondi interi positivi, validati anche quando il salvataggio automatico è disabilitato. |
| `sql_backup.enabled` | Esegue un backup SQL secondo la pianificazione quando è true. Il comando `sql_backup` funziona in entrambi i casi. Vedi [Backup dei database](persistence-operations.md#database-backups). |
| `sql_backup.interval_minutes` | Minuti tra due backup pianificati, da 1 a 71582; valore predefinito 1440. Il primo viene eseguito dopo un intero intervallo dall'avvio. Validato anche quando disabilitato. |
| `sql_backup.directory` | Destinazione dei file; valore predefinito `backups`. Un percorso relativo viene risolto rispetto alla radice del server; le variabili d'ambiente vengono espanse. |
| `sql_backup.keep` | File conservati per ogni database, almeno 1; valore predefinito 5. |
| `diagnostics.enabled` | Avvia il raccoglitore diagnostico periodico quando è true. |
| `diagnostics.interval_seconds` | Secondi interi positivi; devono rientrare nell'intervallo del timer (massimo 4.294.967 secondi). |
| `diagnostics.log_metrics` | Registra periodicamente le metriche raccolte quando è true. |
| `scripting.bootstrap_file` | Percorso non vuoto risolto nella radice degli script; deve rispettare i vincoli dei percorsi degli script. Un file mancante genera un avviso, un errore di esecuzione interrompe l'avvio. |
| `scripting.max_instructions_per_resume` | Budget positivo di istruzioni per una ripresa di coroutine. |
| `scripting.max_instructions_per_chunk` | Budget positivo di istruzioni per l'esecuzione di un chunk al livello principale. |
| `scripting.hook_interval` | Intervallo positivo per il controllo delle istruzioni, non superiore a nessuno dei due budget. |
| `scripting.write_definitions` | Genera `definitions.lua` e `.luarc.json` per il supporto dell'editor. |
| `scripting.max_string_length` | Lunghezza massima positiva del risultato imposta da `string.rep`, misurata in caratteri UTF-16; non è un limite globale della memoria Lua. |
| `ultima.localization.language` | Codice composto da lettere ASCII che identifica il file dei testi `data/messages/<language>.toml` e la directory `data/messages/<language>/` dei file toml; valore predefinito `eng`. Distribuiti: `eng`, `ita`, `ger`, `fre`, `spa`, `por`, `pol`, `cze`. Anche i testi inglesi devono esistere: un messaggio mancante nella lingua scelta usa l'inglese. Usato nelle modalità game e standalone. Vedi [Localizzazione](localization.md). |
| `ultima.line_of_sight.max_distance` | Da 1 a 255; valore predefinito 25. La massima distanza visibile da un punto lungo X o Y, come ModernUO; i punti più lontani non sono mai visibili. Usato nelle modalità game e standalone. |
| `ultima.world.view_range` | Da 5 a 24; valore predefinito 18, come ModernUO e POL. Distanza a cui i giocatori vedono mobile e oggetti a terra lungo X o Y; viene restituita alla richiesta client `0xC8`. Usato nelle modalità game e standalone. |
| `ultima.world.seconds_per_uo_minute` | Da 1 a 3600; valore predefinito 5, come ModernUO: un giorno di gioco dura 2 ore reali. L'ora viene conteggiata dall'inizio del mondo di ModernUO e non richiede salvataggio; ogni mappa è avanti di 320 minuti di gioco rispetto alla precedente (Felucca, Trammel, Ilshenar, Malas, Tokuno, Ter Mur), e l'ora avanza di un minuto ogni 16 tile verso est. |
| `ultima.world.day_light`, `ultima.world.night_light` | Da 0 (massima luminosità) a 31; valori predefiniti 0 e 12, come ModernUO. Notte dalle 00:00 alle 03:59, luminosità crescente fino alle 06:00, giorno fino alle 21:59, luminosità decrescente fino a mezzanotte. La luce viene inviata ai giocatori al login e, quando cambia, ogni 5 secondi; `.globallight` la sovrascrive per tutti. |
| `ultima.world.dungeon_light`, `ultima.world.jail_light` | Da 0 (massima luminosità) a 31; valori predefiniti 26 e 9, come ModernUO. Luce all'interno di una regione di tipo `dungeon` o `jail`, indipendentemente dall'ora; inviata appena un giocatore entra o esce. `.globallight` prevale comunque, così un game master può vedere. |
| `ultima.world.lamp_post_light` | Da 0 a 31; valore predefinito 6. Ogni 30 secondi i lampioni cittadini posizionati da `.decorate` (tipi da `LampPost1` a `LampPost3`) vengono accesi dove la luce dell'ora del giorno (o `.globallight`) è almeno a questo livello, e spenti dove è inferiore; con il valore notturno predefinito 12 corrisponde circa alle 23:00–05:00 di gioco. Le regioni non contano. |
| `ultima.world.season_rotation`, `ultima.world.days_per_season` | `false` e 12 (da 1 a 365). Quando abilitato, ogni mappa la cui stagione in `maps.toml` non è `desolation` attraversa primavera, estate, autunno, inverno partendo da quella stagione, cambiando ogni `days_per_season` giorni di gioco del proprio orologio; una regione con la propria `season` la mantiene. I giocatori ricevono la nuova stagione entro un minuto. Vedi [Stagioni](data-files/maps.md#seasons). |
| `ultima.world.pathfinding_range`, `ultima.world.pathfinding_max_nodes` | 38 (da 8 a 64) e 1000 (da 50 a 20000), come ModernUO. Limiti di una [ricerca di percorso](world-queries.md#pathfinding): distanza massima tra i due estremi lungo X o Y, e numero di posizioni esaminate prima di rinunciare. Una ricerca che rinuncia costa circa un millisecondo ogni cento posizioni |
| `ultima.items.backpack_template`, `ultima.items.gold_template` | ID dei template degli oggetti; valori predefiniti `0x0e75_backpack` e `0x0eed_gold_coin`. Usati per lo zaino dei nuovi personaggi e degli NPC generati e per l'oro degli NPC generati; l'oro iniziale dei nuovi personaggi è un oggetto dell'insieme comune in [`starting_items.toml`](data-files/starting-items.md). Entrambi devono esistere in `templates/items/`, e il template dell'oro deve essere impilabile, altrimenti il server di gioco interrompe l'avvio. Vedi [Oggetti iniziali](data-files/starting-items.md). |
| `ultima.starting_items.best_skills` | Almeno 1; valore predefinito 3, come UOX3 (quattro con le sue abilità iniziali estese). Numero delle abilità più alte di un nuovo personaggio che selezionano gli insiemi di abilità. |
| `ultima.characters.max_per_account` | 1, 5, 6 o 7, il numero di slot visualizzabili dal client; valore predefinito 7. Numero di personaggi che un account può contenere. L'elenco dei personaggi al login di gioco mostra questo numero di slot, e la creazione oltre il limite viene rifiutata con popup e disconnessione. Un nuovo personaggio occupa lo slot scelto dal client se libero, altrimenti il primo libero. Riducendo il valore si mantengono i personaggi esistenti: quelli oltre il nuovo limite vengono elencati nei primi slot liberi, mentre i restanti rimangono memorizzati ma nascosti. |
| `ultima.characters.deletion_delay_hours` | Almeno 1; valore predefinito 24. Quando un giocatore elimina un personaggio (pacchetto `0x83`), questo viene solo contrassegnato: lascia l'elenco, libera lo slot e non conta più per `max_per_account`, e lo staff può ripristinarlo con `character restore` (nel primo slot libero). Dopo questo numero di ore diventa eliminabile; il job che lo rimuove non è ancora implementato. |
| `ultima.npcs.think_interval_ms` | Da 50 a 60000; valore predefinito 500, la velocità passiva di ModernUO. Frequenza con cui un NPC vicino a un giocatore pensa. Solo gli NPC nei settori 5×5 attorno a un giocatore hanno un timer di pensiero; gli altri dormono senza costo. Vedi [Tick degli NPC](game-loop-and-timers.md#npc-tick). |
| `ultima.npcs.sense_range` | Da 1 a 24; valore predefinito 8. Distanza, in celle lungo X o Y, entro cui un altro mobile deve arrivare perché lo script mobile di un NPC lo percepisca con `on_mobile_in_range`. Vedi [Script dei mobile](scripting/mobile-scripts.md). |
| `ultima.regeneration.hits_seconds`, `stamina_seconds`, `mana_seconds` | Ciascuno da 0,1 a 3600; valori predefiniti 11, 7 e 7, i ritmi classici di ModernUO. Secondi tra due punti recuperati, un punto alla volta. Il mana indica il ritmo più lento: intelligenza e Meditation lo riducono secondo la curva classica di ModernUO, fino a circa un decimo. I giocatori nel mondo vengono controllati ogni secondo e un NPC mentre pensa, quindi uno che dorme lontano da tutti i giocatori non rigenera; un ritmo più rapido del controllo restituisce insieme i punti dell'intero intervallo, fino a cinque. Le proprietà del mobile `regen.hits`, `regen.mana` e `regen.stamina` (secondi) sostituiscono il ritmo per quel mobile. |
| `ultima.regeneration.hunger_enabled`, `hunger_minutes` | Valori predefiniti `true` e 5 (da 1 a 1440), il decadimento del cibo di ModernUO. Ogni `hunger_minutes`, conteggiati per ogni giocatore da quando entra nel mondo (un timer controlla i giocatori ogni minuto), un giocatore nel mondo perde un punto di fame, da 20 (sazio) a 0; game master e amministratori non li perdono e non soffrono mai la fame. A 5 il giocatore legge che ha fame, a 0 che sta morendo di fame, e a 0 i punti vita non si rigenerano. Disabilitata, la fame non diminuisce né blocca nulla. Gli NPC non hanno mai fame. |
| `ultima.regeneration.thirst_enabled` | Valore predefinito `true`. La sete viene conteggiata come la fame, da 20 (dissetato) a 0 e con lo stesso ritmo (`hunger_minutes`): un giocatore nel mondo perde insieme un punto di entrambe, lo staff nessuno. A 5 legge che ha sete, a 0 che è disidratato, e a 0 la stamina non si rigenera, come UOX3. Disabilitata, la sete non diminuisce né blocca nulla. Gli NPC non hanno mai sete. |
| `ultima.regeneration.fatigue_enabled` | Valore predefinito `true`. Costo del movimento per un giocatore, con i valori di ModernUO. Un giocatore porta ciò che indossa e tutto il contenuto, esclusa la banca, e può portare 40 stone più 3,5 per punto di forza; la barra di stato mostra entrambi. Oltre il limite, ogni passo costa 5 punti di stamina più uno ogni 25 stone in eccesso, il doppio correndo; senza stamina non si muove e legge il messaggio client "troppo affaticato per muoverti, perché stai trasportando troppo peso". Correre costa un punto ogni 16 passi, e un punto per passo sotto un decimo della stamina; senza stamina un giocatore cammina ma non corre. Sollevare non viene mai rifiutato: un giocatore che posa qualcosa mentre è sovraccarico viene avvertito. Staff e NPC non pagano nulla. Disabilitata, un passo non costa nulla e non viene mai rifiutato. |
| `ultima.skills.total_cap`, `ultima.skills.gain_enabled` | 700 (da 1 a 100000) e `true`. Somma massima dei punti delle [abilità](skills.md) di un giocatore, il 700,0 di ModernUO: più il totale è vicino al limite, più spesso un incremento riduce un'altra abilità con blocco impostato in diminuzione, e al limite un'abilità sale solo quando un'altra potrebbe scendere. Un totale già superiore a un limite abbassato viene lasciato invariato. Con `gain_enabled = false` nessuna abilità sale: i controlli continuano a riuscire o fallire. Gli NPC non migliorano mai. |
| `ultima.skills.stat_max`, `ultima.skills.stat_cap`, `ultima.skills.stat_gain_minutes` | 100 (da 1 a 65535), 225 (da 30 a 100000) e 10 (da 0 a 1440; 0 prova una statistica a ogni abilità riuscita). [Incremento delle statistiche](skills.md#the-stats) classico di ModernUO: massimo raggiungibile con l'uso da una statistica, somma massima delle tre, e minuti tra due tentativi della stessa statistica. Oltre un `stat_cap` o `stat_max` abbassato, una statistica resta invariata, ma una bloccata in diminuzione cede comunque un punto ogni volta che un'altra viene provata. `gain_enabled = false` blocca anche questi incrementi. Gli NPC non migliorano mai. |
| `ultima.combat.global_attack_speed`, `npc_damage_rate` | 1,0 ciascuno, maggiori di 0 e massimo 100. Fattore che divide il ritardo tra due [colpi](combat.md) (2 colpisce il doppio, 0,5 la metà) e numero che divide il danno inflitto da un NPC a un giocatore (2 lo dimezza). |
| `ultima.combat.attack_stamina` | 0 (da 0 a 100). Stamina consumata da un colpo di un giocatore; ModernUO non ne consuma, UOX3 ne consuma 2. Gli NPC non pagano nulla. |
| `ultima.combat.max_range`, `combatant_seconds` | 1 (da 1 a 24) e 60 (da 1 a 3600). Tile raggiunti da un colpo in mischia, e durata del mantenimento del bersaglio senza colpire, come il minuto di ModernUO. |
| `ultima.combat.archery_stand_still_seconds` | `1.0` (da 0 a 60). I secondi in cui un giocatore che impugna un arco o una balestra deve essere rimasto fermo, dal suo ultimo passo, prima di poter sparare; un cambio di direzione non conta. 0 non chiede nulla. Vedi [Arcieri](combat.md#archers) |
| `ultima.combat.display_damage_numbers` | `true`. Il danno del colpo viene mostrato sopra chi lo riceve (pacchetto `0x0B`), ai giocatori nel combattimento. |
| `ultima.combat.blood_enabled`, `blood_pieces`, `blood_seconds` | `true`, 2 (da 0 a 8) e 5 (da 1 a 60). Un colpo che fa danno lascia un pezzo di [sangue](combat.md#the-damage) sotto chi è colpito e da uno a `blood_pieces` intorno; ogni pezzo sparisce dopo `blood_seconds`, che il controllo degli oggetti a terra ogni 5 secondi può allungare. |
| `ultima.crime.criminal_seconds` | Valore predefinito 120 (da 1 a 86400), i due minuti di ModernUO e UOX3. Un mobile che compie un atto criminale resta criminale per questa durata, con il nome grigio per chi lo vede; un altro atto riavvia il tempo, e un assassino resta rosso. Il tempo viene salvato con il personaggio, quindi uscire dal mondo non lo azzera; un NPC torna innocente dopo un riavvio. Un giocatore legge il messaggio client "Hai commesso un atto criminale!!" quando diventa criminale. Nulla rende ancora criminali automaticamente: lo fanno gli script con `mobile.set_criminal`, lo staff con `set criminal`. |
| `ultima.murder.*` | I conteggi degli omicidi, come in ModernUO. `short_term_hours` (predefinito 8) e `long_term_hours` (predefinito 40), da 1 a 8760: un omicidio a breve termine e un'uccisione segnalata vengono dimenticati dopo questo tempo, uno alla volta, in tempo reale, comprese le ore passate fuori dal mondo (ModernUO conta il tempo online), mentre il giocatore è nel mondo o quando ci ritorna. `report_delay_seconds` (predefinito 4), `recently_reported_minutes` (predefinito 10) e `aggressor_seconds` (predefinito 120), da 1 a 86400: quanto tempo dopo la morte a un giocatore viene chiesto di segnalare chi lo ha attaccato, quanto prima che possa segnalare di nuovo lo stesso assassino, e per quanto tempo un attacco a un innocente mantiene segnalabile chi l'ha compiuto. Vedi [Conteggi degli omicidi](death.md#murder-counts). |
| `ultima.crime.archer_guard_template` | Predefinito `archerguard`. Il template di mobile di una guardia chiamata a **Ilshenar** e **Malas**, come il `DefaultGuardType` di ModernUO: una guardia con un arco, che spara a ciò che attacca dalla portata dell'arco invece di colpirlo da accanto (vedi [guard.lua](scripting/shipped-scripts.md#guardlua)). Altrove arriva la guardia di `guard_template`. |
| `ultima.crime.guards_enabled`, `guard_template`, `guard_seconds` | Valori predefiniti `true`, `guard` e 40 (da 1 a 86400). Un giocatore che dice "guards" in una regione sorvegliata, usando la parola chiave del client in qualsiasi lingua o la parola semplice, chiama le guardie: per ogni criminale entro 14 tile che si trova a sua volta in una regione sorvegliata, escluso lo staff, un NPC di `guard_template` appare accanto a lui, su un tile libero a un passo (sul suo se nessuno è libero), con effetto e suono di teletrasporto, e pronuncia la sua frase (messaggio 30138); un criminale riceve una guardia per volta. La guardia scompare allo stesso modo dopo `guard_seconds`. Una guardia uccide ciò che attacca con un colpo, e una guardia arciere gli spara. ModernUO è più restrittivo sui bersagli: lì solo chi ha commesso il crimine in quella città negli ultimi 15 secondi, e un assassino; qui chiunque sia criminale. Una guardia chiamata porta la proprietà `guard.summoned`: una lasciata nel mondo da un server arrestato viene rimossa all'avvio successivo. Disabilitato, non arriva nessuno. |
| `ultima.spawns.initial_fill` | Valore predefinito `true`. La prima generazione di ogni regione di spawn dopo l'avvio la riempie immediatamente fino a `max`, così un mondo vuoto si popola in circa 10 minuti; `false` mantiene il comportamento di UOX3, dove anche la prima generazione porta solo `call` NPC. Usato nelle modalità game e standalone. Vedi [Spawn degli NPC](spawns.md#how-spawning-works). |
| `ultima.jail.fine_gold`, `ultima.jail.max_days` | Valori predefiniti 500 (da 0 a 1.000.000.000) e 30 (da 1 a 3650). Oro prelevato da un prigioniero alla fine della pena, prima dallo zaino poi dalla banca, e pena massima accettata da [`.jail`](commands/jail.md), in giorni reali; vedi [Prigione](jail.md). |
| `ultima.help.stuck_wait_seconds`, `ultima.help.stuck_cooldown_minutes` | Valori predefiniti 5 (da 1 a 60) e 10 (da 0 a 1440). L'attesa e la pausa del pulsante «Sono bloccato» del gump di [aiuto](help.md). |
| `ultima.help.page_cooldown_seconds`, `ultima.help.page_history_days` | Valori predefiniti 60 (da 0 a 3600) e 30 (da 1 a 3650). I secondi che un giocatore aspetta tra due richieste ai game master e i giorni per cui una richiesta chiusa viene conservata prima che l'avvio la cancelli; vedi [Aiuto](help.md). |
| `ultima.schedule.time_zone` | Vuoto (il fuso del sistema) oppure un id IANA come `Europe/Rome`. Un id sconosciuto ferma l'avvio. Un container Docker è in UTC a meno che abbia `TZ` o questa impostazione; su Linux i fusi richiedono il pacchetto tzdata. Vedi [Calendario](schedule.md). |
| `ultima.bulletin_boards.expire_days`, `max_messages`, `thread_seconds`, `reply_seconds` | Valori predefiniti 7 (da 0 a 3650; 0 conserva per sempre le discussioni), 50 (da 1 a 200), 120 e 30 (da 0 a 86400). Durata di una discussione su una [bacheca](bulletin-boards.md) dall'ultima risposta, numero di messaggi contenuti, e attesa di un personaggio tra due nuove discussioni e tra due interventi sulla stessa bacheca. |
| `ultima.bank.max_items`, `max_withdraw`, `min_check`, `max_check` | Valori predefiniti 125 (da 0 a 10000; 0 senza limite), 60000 (da 1 a 60000), 5000 e 1.000.000 (`min_check` da 1 a `max_check`, `max_check` fino a 2.000.000.000). Oggetti contenuti in una [cassetta bancaria](bank.md), compresi quelli nelle borse; monete consegnate da un banchiere per un *prelievo*; valore degli assegni emessi dal banchiere. |
| `ultima.stable.max_pets`, `ultima.stable.fee` | Valori predefiniti 10 (da 1 a 50) e 30 (da 0 a 100000). I animali che un giocatore può lasciare a un addestratore, e quanto costa ciascuno quando viene messo in stalla: vedi la [stalla](mounts.md#the-stable). |

Le impostazioni di gioco si trovano sotto `[ultima]` come sottotabelle (`[ultima.world]`,
`[ultima.characters]`, ...). L'oro iniziale non è un'impostazione: è un oggetto
dell'insieme comune in [`starting_items.toml`](data-files/starting-items.md).

Redis è obbligatorio al runtime in tutte e tre le modalità, compresa standalone.
`redis.connection_string` è una stringa di configurazione StackExchange.Redis o un
riferimento d'ambiente risolto all'avvio; l'esempio Docker usa `redis:6379,password=...`
sul proprio bridge privato. `redis.handoff_secret` è un segreto indipendente comune
al cluster, fornito anch'esso tramite riferimento d'ambiente. Assegna gli stessi valori
al login e a ogni processo game. L'esempio Docker li legge da segreti Compose separati;
lascia i valori effettivi fuori dal TOML e dal repository. Un errore di connessione
Redis impedisce l'avvio. Un'interruzione Redis successiva blocca nuovi elenchi dei realm
e handoff mentre le sessioni di gioco esistenti continuano; i ticket in sospeso si
perdono al riavvio di Redis e i processi game ripubblicano i propri lease. Configura
Redis con `maxmemory-policy noeviction`.

I limiti delle code del game loop, la risoluzione della timer wheel e i limiti di
dispatch dei pacchetti usano oggetti di opzioni C# anziché altre sezioni TOML.
Vedi [Game loop e timer](game-loop-and-timers.md), [Pacchetti](packets.md) e
[Topologia Docker](docker-login-realms.md).

## Sezioni dei plugin

Alcune sezioni di `config/moongate.toml` appartengono ai plugin anziché al server:
`[ultima]` (con le sue sottotabelle) al plugin Ultima, `[admin_api]` al plugin
Administration, e qualsiasi sezione aggiunta da un [plugin su disco](plugins.md#add-a-config-section).
Si modificano come qualsiasi altra sezione; cambiano il lettore e il momento della lettura.

1. Il server legge il file e mantiene le proprie sezioni, ignorando il resto.
2. Ogni plugin, durante la registrazione, legge la propria sezione e controlla i valori.
   Un valore non consentito interrompe l'avvio con un errore che indica l'impostazione,
   riportato come causa di `Plugin '<id>' failed during registration.`
3. Un plugin senza sezione usa i propri valori predefiniti e li aggiunge in fondo al
   file. Le righe già presenti, commenti inclusi, non vengono modificate. Modifica i
   valori aggiunti e riavvia per cambiarli. Se il file è di sola lettura, il server
   registra un avviso ed esegue con i valori predefiniti.

Quindi una nuova radice parte solo con le sezioni del server, e dopo il primo avvio
il file elenca ogni impostazione di ogni plugin installato. Rimuovere un plugin lascia
la sua sezione nel file; il server la ignora. Due proprietari non possono condividere
un nome: un plugin che rivendica `network`, o un nome già preso da un altro plugin,
interrompe l'avvio.

## Riga di comando e directory radice

Esamina l'eseguibile installato con `--help`. Da un checkout:

```sh
dotnet run --project src/Moongate.Server -c Release -- --help
dotnet run --project src/Moongate.Server -c Release -- \
  --root-directory /absolute/path/to/moongate-data --pid-file-name moongate.pid
```

| Opzione | Valore predefinito | Comportamento attuale |
| --- | --- | --- |
| `--root-directory <path>` | Non impostato | Sostituisce `MOONGATE_ROOT`; senza entrambi il server rifiuta l'avvio |
| `--pid-file-name <name>` | `moongate.pid` | Nome del file PID nella radice scelta; usa un semplice nome file |
| `--log-level <level>` | `Information` | Interpretato negli argomenti del server, ma attualmente non applicato alla politica dei livelli Serilog |
| `--log-to-file` | `true` | Il logging su file è abilitato; il parser generato accetta solo la presenza del flag |
| `--log-packets` | `false` | Imposta l'argomento a true; attualmente nessun utilizzatore per il tracciamento dei pacchetti |
| `--show-header` | `true` | Mostra il banner di avvio; flag di presenza |
| `--persistence-schema <mode>` | `None` | `preview` stampa una bozza DDL PostgreSQL; `generate` scrive un file bozza. La vecchia modalità `apply` rimanda a `mgctl migrate apply` |
| `--migration-target <target>` | Non impostato | Obbligatorio per `generate`: `auth` o `world` |
| `--migration-output <path>` | Non impostato | Obbligatorio per `generate`: nuovo file `NNNN_description.sql`; rifiuta la sovrascrittura |
| `--initialize-root` | `false` | Prepara offline configurazione/directory/migrazioni incluse, senza avviare il server |
| `--generate-admin-certificate` | `false` | Con `--initialize-root`, crea o riutilizza l'identità TLS di amministrazione e abilita `[admin_api]` |
| `--admin-certificate-hosts <names>` | Non impostato | SAN DNS/IP separati da virgole oltre a localhost; richiede la generazione del certificato |
| `--version` | — | Stampa la versione dell'eseguibile |
| `-h`, `--help` | — | Stampa l'utilizzo |

Anche se l'help mostra `<bool>` per le opzioni predefinite a true, la CLI attuale
non accetta `--show-header false`, `--show-header=false` o le forme corrispondenti
per il logging su file. Non esiste ancora un'opzione CLI per disabilitare queste due opzioni.

La precedenza della radice è **riga di comando → `MOONGATE_ROOT`**. Se nessuno è impostato,
la radice sarebbe la directory del binario e il server rifiuta l'avvio con codice di
uscita 2, indicando `--root-directory`. Lo stesso rifiuto si applica se uno dei due
si risolve in quella directory, perché un aggiornamento la sostituisce interamente
ed eliminerebbe anche `config/`, `logs/`, `save/` e `world-saves/`.
La radice scelta espande i riferimenti home/ambiente e diventa un percorso assoluto;
una radice relativa parte dalla directory di lavoro. Preferisci percorsi assoluti
espliciti nei gestori dei servizi e nei container. Docker imposta `MOONGATE_ROOT=/data`
per impostazione predefinita.

Il comando dello schema carica le registrazioni di persistenza dei plugin ma non
acquisisce il normale PID, non avvia listener/servizi e non genera file runtime.
Arresta il runtime interessato prima di applicare DDL revisionato.
Vedi [Primo avvio](getting-started.md) per proprietà del PID, log e risoluzione dei problemi,
[Persistenza PostgreSQL](persistence.md) per connessioni, schema e semantica del salvataggio
del mondo, e [Scripting Lua](scripting/runtime.md#budgets-and-sandbox) per budget e limiti della sandbox.

L'SQL versionato viene applicato da `mgctl migrate status|apply --target auth|world`,
un processo autonomo. Negli artefatti rilasciati la radice predefinita è la sua directory,
quella del server; `--root-directory` e `MOONGATE_ROOT` la sostituiscono.
Vedi [Generare, revisionare e applicare](persistence-migrations.md#generate-review-and-apply).

## Endpoint di amministrazione

`[admin_api]` configura il plugin gRPC incorporato. È disabilitato per impostazione
predefinita e usa TLS del server sulla porta 2590 quando abilitato. I percorsi dei
certificati si risolvono rispetto alla radice; i riferimenti d'ambiente delle password
vengono risolti solo per gli endpoint abilitati. Usa [Configurazione certificati di mgctl](mgctl.md#generate-an-administration-certificate)
per generare un PFX senza password e abilitare offline l'endpoint. Vedi
[API di amministrazione](admin-api.md) per ruoli, permessi, creazione del primo amministratore e tutti i limiti.

## Cifratura del client UO

`MoongateServerConfig.Network.Encryption` configura entrambi i listener UO. `Mode` è
l'enum `NetworkEncryptionMode`: `Disabled` (predefinito), `Optional` o `Required`.

| Modalità | Connessioni accettate |
| --- | --- |
| `Disabled` | Protocollo esistente in chiaro; non viene installato middleware di cifratura. |
| `Optional` | Pacchetti di login validi in chiaro, oppure pacchetti decifrati correttamente con il profilo configurato. |
| `Required` | Rifiuta i pacchetti di login in chiaro; richiede un pacchetto valido dopo la decifratura con il profilo configurato. |

Le modalità abilitate richiedono una versione esplicita del client POL e selezionano
un profilo cifrato per configurazione; non provano più versioni. Le chiavi di login
vengono derivate dalla versione, mentre la cifratura di gioco viene selezionata come
OldBlowfish, Blowfish12536, Blowfish, BlowfishTwofish o Twofish. Le versioni nella stessa
famiglia Twofish moderna condividono il cifrario del gioco, quindi questa impostazione
non è un controllo esatto della versione client sul listener game. È supportata la
versione speciale `2.0.0x`. Versioni vuote o malformate e gli alias senza cifratura
`none`, `ignition` e `uorice` impediscono l'avvio nelle modalità `Optional` e `Required`.

`Optional` controlla prima il pacchetto iniziale di login con il normale parser dei
pacchetti. Se non è valido in chiaro, il server tenta la decifratura e valida il
risultato; una validazione fallita rifiuta la connessione. Non torna al testo in chiaro
dopo una decifratura fallita. Per un seed di login legacy di quattro byte, la cifratura
abilitata fornisce al gestore di login la versione configurata perché il client non
ha inviato una versione insieme a quel seed.

Per Enhanced Client 67.0.117.0, usa:

```toml
[network.encryption]
mode = "Optional"
client_version = "67.0.117.0"
```

Usa la **versione del protocollo**, incluso l'offset di 60 della versione principale
di Enhanced Client. L'eseguibile Enhanced Client esaminato, con versione file `4.0.117.0`,
usa le chiavi della versione di protocollo `67.0.117.0`; usare `4.0.117.0` o `7.0.117.0`
produce chiavi di login diverse. Mantieni abilitata la cifratura originale del client
quando provi questa configurazione. Un launcher che rimuove la cifratura richiede
`Optional` o `Disabled`. Cambiare l'impostazione richiede un riavvio; l'avvio riporta,
per esempio:

```text
Client encryption: Optional; client 67.0.117.0; login XOR; game Twofish / MD5-XOR
```

Per processi login e game separati, configura la stessa politica e lo stesso profilo
su ciascuno e riavvia entrambi dopo le modifiche. Vedi [Rete](network.md) per dettagli
su handshake e trasporto.
La funzionalità offre interoperabilità con il protocollo POL, non TLS o trasporto
autenticato. Non implementa la vecchia negoziazione AES/E3 di Kingdom Reborn.
I test automatici confrontano vettori originali POL C++ ed esercitano framing TCP
reale e compressione. È stato eseguito un login interattivo con Enhanced Client 4.0.117
(`67.0.117.0`); un'altra build richiede ancora una prova dal client.
Vedi [Enhanced Client](enhanced-client.md) per ciò che funziona dopo il login.
