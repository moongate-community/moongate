<!-- translation: {"sourceHash":"0e88eeedc1680e55cd260df711e1644a9a3d9413967e603e21b661982842ab67","title":"Pacchetti NuGet"} -->

# Librerie NuGet e verifica dei pacchetti

Esegui il controllo completo dei pacchetti dalla radice del repository:

```shell
bash scripts/verify-packages.sh
```

Richiede SDK .NET 10.0.100 o successivo con supporto .NET 10, Bash, un checkout Git
e accesso a nuget.org per le dipendenze di terze parti. I tool C# di verifica usano
app .NET basate su file e la libreria standard; non servono installazioni aggiuntive.
L'esempio di rendering nativo viene esercitato su Linux in CI.

## Pacchetti

Tutti i pacchetti hanno come destinazione `net10.0` e condividono la versione in
`Directory.Build.props`, gestita da release-please. Ogni pacchetto include il proprio
README inglese, il logo Moongate originale, documentazione XML e un pacchetto di simboli abbinato.

| Pacchetto e README | Scopo | Dipendenze Moongate dirette |
|---|---|---|
| [Moongate.Admin.Contracts](../src/Moongate.Admin.Contracts/README.md) | Contratti gRPC versionati e definizioni `.proto` portabili | Nessuna |
| [Moongate.Core](../src/Moongate.Core/README.md) | Primitive condivise, geometria, configurazione e utilità | Nessuna |
| [Moongate.Network](../src/Moongate.Network/README.md) | Trasporto TCP standalone, framing e pipeline | Nessuna |
| [Moongate.Network.Packets](../src/Moongate.Network.Packets/README.md) | Definizioni dei pacchetti UO e serializzazione basata su span | Core |
| [Moongate.Persistence](../src/Moongate.Persistence/README.md) | Moduli PostgreSQL asincroni, transazioni, operazioni sullo schema e accesso tipizzato alle entità | Core, Persistence.Migrations |
| [Moongate.Persistence.Migrations](../src/Moongate.Persistence.Migrations/README.md) | Cataloghi SQL versionati e validazione della cronologia immutabile delle migrazioni | Nessuna |
| [Moongate.Scripting](../src/Moongate.Scripting/README.md) | Runtime Lua 5.2 incorporato, moduli associati tramite attributi, pianificazione delle coroutine e definizioni per editor | Core, Server.Core |
| [Moongate.Server.Core](../src/Moongate.Server.Core/README.md) | Contratti di server e plugin, eventi e registrazioni | Core, Network, Network.Packets |
| [Moongate.Ultima](../src/Moongate.Ultima/README.md) | Lettori dei dati client UO e utilità di rendering | Nessuna |

`Moongate.Server`, `Moongate.Ctl` e la libreria che esegue, `Moongate.MigrationRunner`,
sono eseguibili distribuiti tramite artefatti di rilascio
e immagini container. Non producono pacchetti libreria. `Moongate.Server.Admin` e
`Moongate.Server.Ultima` sono moduli incorporati distribuiti con il server, anch'essi
non impacchettabili. Test e fixture dei plugin sono esclusi dal packaging.

## Cosa controlla il comando

1. Compila e impacchetta la soluzione in Release in una nuova directory `artifacts/nuget.*`.
2. Controlla esattamente nove file `.nupkg` e nove `.snupkg`: metadati, dipendenze,
   byte del README, hash del logo originale, contenuto DLL/XML, identità Portable PDB
   e SourceLink che punta al commit del repository registrato nel pacchetto.
3. Estrae gli esempi C# contrassegnati da ogni README in nove app console temporanee
   fuori dal repository. Ogni app fa riferimento direttamente a un pacchetto Moongate;
   il verificatore della persistenza usa Npgsql per creare il proprio database isolato.
4. Ripristina, compila ed esegue le app, controllandone l'output. Questo esercita
   geometria, ciclo di vita TCP, codifica/decodifica dei pacchetti, persistenza PostgreSQL,
   bus degli eventi, esecuzione dei moduli Lua incorporati e caricamento nativo SkiaSharp.
   Test separati della soluzione verificano lease dei realm basati su Redis e ticket
   di handoff del login monouso.

Il consumer della persistenza richiede `MOONGATE_TEST_POSTGRES_CONNECTION_STRING`
come connessione Npgsql amministrativa. Crea un database univoco
`moongate_test_nuget_<uuid>`, vi esegue l'esempio README ed elimina solo quel database
generato. Una configurazione mancante fallisce con un messaggio che indica come
intervenire; non viene mai saltata silenziosamente.

L'attuale dipendenza `FreeSql.Provider.PostgreSQL` 3.5.311 risolve Npgsql 5.0.18.
Mantieni questo vincolo riconosciuto del provider anziché sostituire silenziosamente
Npgsql con un'altra versione principale. Un aggiornamento provider/driver deve
superare i test reali del consumer PostgreSQL e di compatibilità della soluzione descritti qui.

Le fixture PostgreSQL della soluzione usano lo stesso contratto di connessione
quando è impostato `MOONGATE_TEST_POSTGRES_CONNECTION_STRING`, e analogamente
`MOONGATE_TEST_REDIS_CONNECTION_STRING` per Redis; senza di essi, i test della
soluzione avviano container PostgreSQL e Redis tramite Docker. Vedi i
[prerequisiti dei test di integrazione](../CONTRIBUTING.md#verify-your-changes).
Esegui i progetti di test in serie perché i loro host condividono questi servizi:

```sh
dotnet test Moongate.slnx -c Release -m:1
```

Ogni fixture crea un database univoco `moongate_test_<uuid>` ed elimina solo quel
database. Non usare mai una connessione Accounts o Realm operativa per questa variabile.

Le app consumer usano una nuova cache NuGet temporanea a ogni invocazione. Il mapping
delle sorgenti dei pacchetti limita `Moongate.*` al feed locale generato e risolve
le dipendenze esterne da nuget.org. I pacchetti globali esistenti non possono far
sembrare funzionante un pacchetto locale difettoso. Non servono file client UO,
server di gioco esterni o porte di ascolto fisse.

## Output e risoluzione dei problemi

Le esecuzioni riuscite stampano una riga `PASS` per libreria sia per la validazione
dell'archivio sia del consumer, seguita dalla directory di output dei pacchetti.
Gli archivi generati restano lì per l'ispezione e sono ignorati da Git. Gli spazi di
lavoro consumer vengono rimossi dopo il successo. In caso di errore, il tool stampa
l'operazione fallita e conserva lo spazio di lavoro temporaneo per la diagnosi.

Per ripetere un controllo su una directory di output esistente, passa esplicitamente il percorso:

```shell
dotnet run --file scripts/VerifyNuGetPackages.cs -- "$PWD" /path/to/package-directory
dotnet run --file scripts/VerifyNuGetConsumers.cs -- "$PWD" /path/to/package-directory
```

Un pacchetto di simboli vuoto indica output PDB mancante. I nove progetti libreria
impostano `DebugType=portable` in Release nei propri file `.csproj`, prima che MSBuild
calcoli gli elementi di output dei simboli. Impostarlo dopo in `Directory.Build.targets`
non basta. I metadati condivisi di README, icona e pacchetto di simboli sono configurati
in quel file targets. Il server conserva le informazioni di debug incorporate.

Un errore di rendering nativo va riprodotto sulla piattaforma di deployment con il
suo runtime nativo SkiaSharp. La CI Linux non certifica l'esecuzione Windows o macOS.

## CI e pubblicazione

La configurazione Release di `.github/workflows/ci.yml` esegue lo stesso comando di
verifica dopo i test della soluzione. Il controllo esistente degli avvisi di terze
parti rimane abilitato. Questi controlli non pubblicano nulla né richiedono una chiave API NuGet.

La pubblicazione è responsabilità separata del flusso di rilascio esistente.
Eseguire lo script di verifica non crea un rilascio né invia pacchetti a un feed.
La possibilità di impacchettamento è esplicita: il valore predefinito condiviso è
`IsPackable=false`, e solo i nove progetti libreria lo abilitano esplicitamente.
Le loro voci `ProjectReference` esistenti diventano dipendenze NuGet anziché copie
incluse degli assembly di altri progetti.

Quando modifichi un esempio, mantieni il suo commento HTML `nuget-smoke` direttamente
sopra il blocco di codice C#, così il controllo consumer continua a compilare il codice documentato.
