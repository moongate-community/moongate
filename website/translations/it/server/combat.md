<!-- translation: {"sourceHash":"1db4783ea6281b4b7d12d85e0b44ac594412f8f83a28db822b490ed9db6bdf43","title":"Combattimento"} -->

# Combattimento

Un giocatore combatte un NPC a mani nude, come nel combattimento classico (pre-AOS) di ModernUO; l'NPC risponde e
muore quando i suoi punti ferita finiscono. Questa è la prima parte: armi e armature degli oggetti, parata ed eventi di combattimento di Lua arrivano dopo.

## Iniziare un combattimento

Un giocatore clicca un mobile, e il client invia la richiesta di attacco (`0x05`), oppure uno script chiama
`combat.attack(attacker, target)`. Il combattimento inizia quando:

- entrambi sono nel mondo, sulla stessa mappa, e il bersaglio non è l'attaccante, è vivo e non è invulnerabile
  (`notoriety = "invulnerable"`);
- il bersaglio non è nascosto all'attaccante, è entro il raggio visivo (`ultima.world.view_range`) e in linea
  di vista.

Un giocatore entra in modalità guerra e gli viene detto contro chi combatte (`0xAA`). A una richiesta rifiutata si risponde con un bersaglio
azzerato (`0xAA` con zero), così il client non lascia il bersaglio evidenziato.

I venditori (ogni template che eredita `basevendor`), i banchieri e le guardie sono `invulnerable`, con il
nome giallo, come in ModernUO: non possono essere attaccati. Mostri e animali sono grigi o rossi, non blu.

Un giocatore che attacca un innocente (un nome blu) che non lo sta già combattendo diventa un
[criminale](server-configuration.md), e arrivano le guardie. Attaccare chiunque altro, un mostro, un criminale o uno
che sta combattendo il giocatore, non è un crimine.

La pace (modalità guerra disattivata) termina il combattimento di un giocatore. Ogni combattimento termina quando il bersaglio lascia il mondo, muore o va su
un'altra mappa, dopo `combatant_seconds` (60) senza un colpo, oppure con `combat.stop`.

## Il colpo

Ogni decimo di secondo un combattente il cui ritardo è terminato, e il cui bersaglio è entro `max_range` caselle (1) e
15 unità di altezza, sferra un colpo. Il primo colpo è immediato; il ritardo dopo ciascuno è

```text
15000 / ((stamina + 100) * speed) / global_attack_speed      seconds, speed 30 for fists
```

quindi 2,5 secondi a 100 di stamina. Un colpo:

1. rivela un attaccante nascosto;
2. costa a un giocatore `attack_stamina` di stamina (0 per impostazione predefinita; UOX3 ne toglie 2);
3. viene comunicato al suo giocatore (`0x2F`) e riproduce l'animazione del colpo del corpo (il pugno di un umano, il primo
   attacco di un mostro o di un animale);
4. tira per colpire con il Wrestling di entrambi, `(attacker + 50) / ((defender + 50) * 2)`, il che fa anche crescere il
   Wrestling di un giocatore;
5. se manca riproduce il suono del colpo mancato (`0x239`) e nient'altro;
6. se colpisce riproduce il suono del colpo a segno (il suono `attack` del template di un NPC, altrimenti lo `0x135` dei pugni) e il
   suono `hurt` del bersaglio, ne riproduce l'animazione del dolore e infligge il danno.

## Il danno

- La base è l'arma che un giocatore impugna (un numero dal suo `damage_min` al suo `damage_max`), i dadi del template di
  un NPC (`damage`), oppure da 1 a 8 per i pugni.
- Aumentata dalla tattica dell'attaccante (`+ (tactics - 50)%`), dalla forza e dall'anatomia (`strength/5%`, `anatomy/5%`, e
  un altro 10% con anatomia a 100). Tattica e anatomia vengono provate a ogni colpo a segno e fanno crescere un giocatore.
- Dimezzato quando il bersaglio è un giocatore o l'attaccante è un NPC; un giocatore che colpisce un NPC lo infligge tutto.
  `npc_damage_rate` divide ciò che un NPC fa a un giocatore.
- L'armatura ne toglie la sua parte. Un **giocatore** viene colpito in una parte del corpo, scelta come la sceglie ModernUO (collo 7%,
  mani 7%, braccia 14%, testa 15%, gambe 22%, petto 35%), e il pezzo che vi indossa toglie da metà a tutto il suo
  `armor_rating`; una parte senza armatura non toglie nulla. Un **NPC** ha un solo numero, l'`Armor` del suo template, e ne viene tolta
  la quota di una zona, da metà a tutta. Viene inflitto almeno 1.
- Il danno è mostrato sopra il bersaglio ai giocatori nel combattimento (`0x0B`, `display_damage_numbers`) e la
  barra della salute si muove.
- Un colpo che fa danno lascia del **sangue** a terra: un pezzo sotto chi è colpito e da uno a `blood_pieces`
  intorno, a una casella di distanza, con una delle sette grafiche del sangue di ModernUO (`0x1645`, da `0x122A` a
  `0x122F`), del colore della creatura. Sono oggetti a terra dei template `blood_splash_*` e spariscono dopo
  `blood_seconds` (5), come in ModernUO e Source-X; gli oggetti a terra si controllano ogni 5 secondi, quindi un pezzo
  può durare altrettanto di più. Una creatura il cui template dice `blood_hue = -1` non sanguina: i non morti e i
  golem. Un giocatore sanguina di rosso. Pugni, spade e frecce sanguinano allo stesso modo; per ora un incantesimo
  non fa sangue.

La finestra di stato di un giocatore mostra il danno dei suoi pugni, da `1` a `8` con gli stessi bonus di tattica, forza e
anatomia (il minimo non scende mai sotto 1); lo stato di un NPC non ne mostra.

Un mobile senza più punti ferita muore come quando un game master [lo uccide](death.md), con l'attaccante come uccisore
e il suo cadavere: un NPC lascia il mondo, un [giocatore](death.md#death-of-a-player) resta come fantasma. Un fantasma non combatte,
e nessuno lo combatte.

## L'NPC risponde

Un NPC che viene colpito, o mancato, e non combatte nessuno, combatte chi lo attacca, al proprio ritmo. Uno che ne combatte
un altro continua con quello. Un giocatore colpito non risponde da solo: clicca. I mostri di
[`monster.lua`](scripting/shipped-scripts.md#monsterlua) vanno verso un giocatore e, accanto a lui, chiamano `combat.attack` su di lui.

## Impostazioni

`[ultima.combat]`, vedi [Configurazione del server](server-configuration.md): `global_attack_speed`, `attack_stamina`,
`npc_damage_rate`, `max_range`, `combatant_seconds`, `display_damage_numbers`, `blood_enabled`, `blood_pieces` e
`blood_seconds`.

## Lua

Il modulo `combat`: `combat.attack(attacker, target)`, `combat.stop(mobile)` e `combat.target(mobile)`.

## Armi e armature

Ciò che un giocatore tiene in mano viene letto dai template degli oggetti sui livelli a una mano e a due mani: il
primo con un `damage_max` è l'arma. Dà la velocità del colpo, il danno, la skill della probabilità di colpire
(swordsmanship per una spada, un'ascia o un'arma in asta, mace fighting per una mazza, fencing per una lancia o un pugnale) e
i suoni e il colpo del suo tipo:

| Tipo | A segno, mancato | Colpo (una mano, due mani) |
| --- | --- | --- |
| `sword` | `0x23B`, `0x23A` | fendente (9, 13) |
| `axe` | `0x232`, `0x23A` | fendente (9, 13) |
| `pole_arm` | `0x237`, `0x238` | fendente (13) |
| `mace` | `0x233`, `0x239` | botta (11, 12) |
| `fencing` | `0x23B`, `0x238` | affondo (10, 14) |

Con un'arma senza tipo, che UOX3 non elenca, si combatte con Wrestling e suona come i pugni. La skill per colpire del
difensore è quella della sua arma, Wrestling quando non ne impugna. Un arco, una balestra e un'arma da lancio vengono letti e
**non** usati ancora per combattere: un giocatore così combatte a mani nude. Un NPC combatte con il suo template, qualunque cosa indossi.

Il valore di armatura dell'intero giocatore, mostrato dalla finestra di stato, è l'armatura di ogni parte pesata con la quota
dei colpi che riceve (arrotondata), e il danno mostrato lì è quello dell'arma, con i bonus. I numeri sono quelli delle ere
di UOX3, convertiti da `mgctl convert uox` (vedi [Migrare da UOX3](uox3-migration.md)); una grafica semplice eredita i numeri
LBR, quelli classici di ModernUO.

## Arcieri

Un NPC che impugna un **arco** o una **balestra** tira invece di combattere accanto al bersaglio, come fanno gli arcieri di ModernUO e
di UOX3. La gittata è quella dell'arma: 10 caselle per un arco, 8 per una balestra. Deve vedere il bersaglio: senza
linea di vista mantiene il combattimento ma non tira, e il suo [script delle creature](scripting/shipped-scripts.md#commoncreaturelua)
lo porta dove la ha. Le distanze si contano in quadrati, la maggiore delle due differenze lungo X e Y, come le contano gli script e la linea di vista: un vicino in diagonale è a una casella. La linea di vista va da occhio a occhio. Un tiro è un colpo come gli altri: la velocità dell'arma e la stamina dell'NPC danno il ritardo,
il tiro per colpire usa la skill Archery contro ciò con cui il bersaglio si difende, il danno è quello dei dadi del template
(non dell'arco), e la freccia `0x0F42`, o il quadrello `0x1BFE`, vola dal tiratore al bersaglio, a segno o mancato.
Un corpo umano riproduce l'azione di tiro della sua arma; un corpo di mostro riproduce il suo attacco. Il suono è l'attacco proprio della
creatura, altrimenti quello dell'arco, `0x234` a segno e `0x238` se manca. Le munizioni di un NPC non vengono mai contate, e non deve restare fermo.

Un **giocatore** che impugna un arco o una balestra tira allo stesso modo, con il danno dell'arma (da 8 a 41 per un arco semplice, che
la finestra di stato mostra), e con queste differenze, come in ModernUO e UOX3:

- Ogni tiro consuma **una freccia** (arco) o **un quadrello** (balestra), trovati nello zaino o in una borsa al suo interno. Senza,
  non vola nulla e il colpo è perso: il suo ritardo si paga comunque, e non viene mostrato alcun messaggio, come in ModernUO.
- Dei tiri, a segno o mancati, il **40 percento** lascia una freccia o un quadrello a terra ai piedi del bersaglio, da
  raccogliere.
- Il giocatore deve essere **rimasto fermo per un secondo**: un passo, non una rotazione, entro `archery_stand_still_seconds`
  ([`[ultima.combat]`](server-configuration.md)) dal tiro lo ritarda. 0 non richiede nulla.
- La skill Archery è quella tirata, e quella che cresce; un arco nelle mani del bersaglio è ciò con cui si difende.

## Non ancora

Parata (uno scudo non conta ancora nulla), una faretra (le munizioni si prendono dallo zaino), mosse speciali, durabilità (`max_hits` è conservato, non usato), la
forza richiesta da un'arma o da un'armatura (`strength_required` è conservato, non usato), il bonus di lumberjacking delle asce, liste
di aggressori oltre il bersaglio, bende ed eventi di combattimento di Lua (`attack`, `hit`, `miss`,
`get_hit`).

## Vedi anche

- [Morte e resurrezione](death.md)
- [Skill](skills.md)
- [Configurazione del server](server-configuration.md)
