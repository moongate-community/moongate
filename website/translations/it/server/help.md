<!-- translation: {"sourceHash":"ce0c2f5d734e74f45539439974fdad1c22232f0414bf9239e4642616bbf3505d","title":"Aiuto"} -->

# Aiuto

Il pulsante Help del paperdoll apre un piccolo menu. Non aspetta un game master: i tre pulsanti
rispondono subito al giocatore. Una coda di richieste per i game master è il passo successivo, vedi
[Non ancora](#not-yet).

```text
Help
[>] I am stuck
[>] Useful commands
[>] Server rules
```

Un clic destro chiude il menu. Anche un fantasma può aprirlo, ed è quando serve di più. Il pacchetto è
lo 0x9B (vedi [Pacchetti](packets.md)); il menu è il gump `help_menu`
(`templates/gumps/help_menu.xml`, script `scripts/gumps/help_menu.lua`).

## Sono bloccato

Il pulsante porta alla città di partenza più vicina un personaggio che non riesce a uscire da una
trappola, da una casa o da una grotta.

1. Al personaggio viene detto `Stand still for 5 seconds and you will be taken to Britain.`
2. Se non si muove per tutta l'attesa, compare in quella città e gli viene detto `You have been taken to
   Britain.` Se si muove non succede nulla: `You moved: you stay where you are.`
3. La città è quella di [`data/starting_cities.toml`](data-files/starting-cities.md) più vicina al
   personaggio sulla mappa in cui si trova. Se su quella mappa non c'è nessuna città, è la prima del file.

Il pulsante rifiuta, con un testo, quando:

- il personaggio è in [prigione](jail.md);
- il personaggio sta combattendo;
- ha già chiesto e l'attesa non è finita: `You already asked to be moved: stand still.`;
- ha chiesto meno tempo fa della pausa: `You can ask to be moved again in 7 minutes.` La
  pausa è conservata con il personaggio, quindi un nuovo accesso non la azzera, e una richiesta che non
  ha spostato nessuno non la consuma.

Prigione e combattimento vengono controllati di nuovo alla fine dell'attesa. Un personaggio che esce
dal gioco durante l'attesa non viene spostato. Se la mappa della città non è caricata il personaggio resta
dove è, gli viene detto `There is no
city to take you to.` e non consuma la pausa. Ogni spostamento viene registrato nel log a livello
Information con il giocatore, dove era e la città. Game master e superiori non hanno pausa.

## Comandi utili

Esegue [`.help`](commands/help.md) come il giocatore: i comandi che il suo account può usare.

## Regole del server

Dice le regole del server in un messaggio di sistema, il messaggio 30200. Cambia il testo in
`data/messages/<language>/moongate.toml` (vedi [Localizzazione](localization.md)).

## Impostazioni

```toml
[ultima.help]
stuck_wait_seconds = 5        # The seconds a character must stand still before "I am stuck" moves it.
stuck_cooldown_minutes = 10   # The minutes before a player can use "I am stuck" again; 0 allows it at once.
```

`stuck_wait_seconds` va da 1 a 60 e `stuck_cooldown_minutes` da 0 a 1440. Un valore fuori
intervallo ferma il server all'avvio e l'errore indica l'impostazione.

## Per gli script

Il modulo Lua `help` dà a uno script ciò che usa il menu:

| Funzione | Cosa restituisce |
| --- | --- |
| `help.settings()` | `{ wait_seconds, cooldown_minutes }` |
| `help.nearest_city(player)` | `{ town, x, y, z, map }` della città di partenza più vicina, oppure nil |

I testi del menu sono i messaggi da 30192 a 30204, in tutte le lingue distribuite.

## Non ancora

- Chiamare un game master: una richiesta con un genere e un testo, conservata in una coda che lo staff
  smaltisce, con una risposta al giocatore.
- Una voce di menu per le segnalazioni di molestie.
