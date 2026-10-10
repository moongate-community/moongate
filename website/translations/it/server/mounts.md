<!-- translation: {"sourceHash":"615039aad5fa8c2651a75346d59f4014afd492263e176e50c3446c4d69b19689","title":"Cavalcature"} -->

# Cavalcature

Un giocatore cavalca un cavallo, un lama o uno struzzo addomesticato: cammina e corre il doppio più
veloce. È la prima parte delle cavalcature; la stalla, la bola e le cavalcature eteree non sono ancora
state realizzate.

## Come cavalcare

1. Procurati una creatura tua. Un game master la assegna con [`tame`](commands/tame.md); la skill di
   addomesticamento non esiste ancora. Una creatura senza proprietario non è di nessuno da cavalcare,
   nemmeno di un game master.
2. Stai a non più di una casella da lei, sullo stesso livello, e fai doppio clic su di lei. Il proprietario
   si siede in sella; un game master può montare una creatura che appartiene a qualcun altro.
3. Cammina o corri. Chi è in sella fa un passo ogni 200 ms camminando e ogni 100 ms correndo, chi è a piedi
   ogni 400 e 200. Corre il cavallo, non chi lo cavalca: correre non costa stamina al cavaliere, e un
   cavaliere sovraccarico si stanca come se camminasse.
4. Fai doppio clic su te stesso per scendere, oppure muori: il cavallo torna in piedi dove eri tu.

Il client mostra un suo testo quando una cavalcatura viene rifiutata: stai già cavalcando, la creatura è a
più di una casella, oppure non è di nessuno o è di qualcun altro.

Un cavaliere morto non può montare. Il pulsante paperdoll del client apre comunque il tuo paperdoll mentre
cavalchi.

## Che cosa succede al cavallo

Il cavallo non resta vivo fuori dalla mappa: diventa un dato sull'oggetto cavalcatura che il cavaliere
indossa sul layer mount (25). L'oggetto conserva il template del cavallo e il suo proprietario, viene
salvato insieme al cavaliere, e dopo un riavvio o un logout non resta nulla indietro. Scendere rimuove
subito l'oggetto e ricrea il cavallo, dal suo template, sulla casella del cavaliere, con il suo
proprietario. Il cavallo riceve un nuovo serial e non conserva né i punti ferita, né il colore, né ciò che
portava.

La riga dell'oggetto cavalcatura viene cancellata prima che il cavallo sia creato, quindi un crash fra i
due momenti lascia il cavaliere a piedi e il cavallo nel mondo, mai entrambi. Se il cavallo non si riesce a
creare si riprova tre volte, a un secondo di distanza, registrando ogni errore; dopo la terza volta è
perso, e la riga di log dice quale template e dove.

L'oggetto cavalcatura non si può sollevare dal cavaliere. Una creatura che sta morendo non si può
cavalcare, e nemmeno un cavaliere con l'inventario riservato, per esempio mentre si ritira un allegato di
un libro.

## Rendere cavalcabile una creatura

Dai al suo template mobile il tag `mount_item`, l'id di un template di oggetto il cui layer è `mount`:

```toml
[[mobile]]
id = "horse"
body = 228
[mobile.tags]
mount_item = "horse4"
```

I figli di un template ereditano il tag. Un valore vuoto, `mount_item = ""`, rende un figlio non
cavalcabile, come per ora le cavalcature eteree, gli incubi e le altre creature di
`templates/mobiles/mounts.toml`.

## Che cosa non può fare un cavaliere

Un cavaliere non può estrarre minerale con un piccone, pescare con una canna né usare la skill Stealth:
ognuna di queste azioni viene rifiutata con il testo del client e non parte nulla. L'ascia e la skill
Hiding non sono toccate. Uno script chiede `mobile.is_mounted(serial)`.

## Teletrasporti che rifiutano un cavaliere

Un oggetto teletrasporto con la prop `deny_mounted` impostata a `true` (come testo in un file di
decorazione: `deny_mounted = "true"`) dice al cavaliere di scendere prima e lo lascia dove si trova; non
partono né fumo né suono. Chi è a piedi passa. Nessun teletrasporto distribuito porta ancora il flag.

```toml
[[decoration]]
type = "Teleporter"
item_id = 0x1BC3
props = { point_dest = [5690, 569, 25], deny_mounted = "true" }
locations = [[5827, 593, 0]]
```

## La stalla

I maestri di animali delle città, compresi quelli degli zingari (`script_id = "stablemaster"`), tengono una
stalla. Restano venditori come prima: il negozio e le lezioni rimangono.

1. Stai a non più di 12 caselle e di' *stable*, oppure scegli *Stable* nel menu contestuale del maestro.
   Leggi il messaggio e ottieni un cursore: scegli un tuo animale, a non più di una casella da te.
2. L'animale lascia il mondo e il suo template entra nella tua stalla; la tariffa viene prelevata dal tuo
   zaino e poi dalla banca (30 monete d'oro di default). Il maestro risponde con il testo del client: il
   tuo animale è in stalla, oppure non puoi metterlo in stalla, non è tuo, hai troppi animali nella stalla,
   o non puoi pagare.
3. Di' *claim* per vedere l'elenco dei tuoi animali in stalla, un pulsante ciascuno; un pulsante ricrea
   quell'animale accanto a te. *Claim All* nel menu li riprende tutti.

Si può mettere in stalla solo un animale cavalcabile e tuo, non uno che sta morendo. Un animale ritirato
viene ricreato dal suo template, con te come proprietario, e non conserva né i punti ferita, né il colore,
né ciò che portava; la creazione viene tentata tre volte, e un animale che non si riesce a creare torna
nella tua stalla. La stalla è la prop `stabled` del tuo personaggio (gli id dei template, uniti da `;`),
salvata insieme a lui. Il limite e la tariffa sono in [`[ultima.stable]`](server-configuration.md).

## Cavalcature eteree

Una statuetta di una cavalcatura eterea (un cavallo, un lama, uno struzzo, un kirin, un unicorno, un
ridgeback, un drago di palude o uno scarabeo, `templates/items/misc/ethereal-statues.toml`) permette al
proprietario di cavalcare senza una creatura. Fai doppio clic mentre è nel tuo zaino: sparisce e ti siedi
sulla cavalcatura eterea. Leggi il testo del client quando non è nello zaino, o quando stai già
cavalcando. Scendendo, o morendo, la statuetta torna nel tuo zaino, anche oltre il suo limite di oggetti
(il suo posto era stato liberato quando hai montato), oppure a terra dove ti trovi se non hai uno zaino.
Torna con il suo colore e il suo nome ma con un nuovo serial. Non c'è attesa per evocarla e nessuno slot
da seguace. Un game master ne crea una con `.add ethereal_horse_statue`. La statuetta è un template con
`script_id = "ethereal_mount"` e il tag `mount_item`, il template dell'oggetto cavalcatura che viene
indossato.

## Combattere dalla sella

Un cavaliere che attacca esegue le azioni di una cavalcatura: una mano, due mani, arco o balestra.
L'animazione che indica un colpo subito è quella consueta. Gli id sono quelli della tabella animazioni del
client; non sono stati provati con un client.

## Non ancora realizzato

La bola e l'abilità di disarcionare delle armi.
