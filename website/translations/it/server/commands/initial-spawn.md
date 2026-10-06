<!-- translation: {"sourceHash":"138a0ed1bb02c2e27e022763521dbabaa07cd1e586fc97146168384661ab90c3","title":"initial_spawn"} -->

# initial_spawn

Riempie ogni regione di spawn del mondo fino al suo `max` al successivo controllo di spawn.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `initial_spawn` | Sì | Sì | Administrator | Game |

```text
.initial_spawn
```

Non accetta argomenti. Contrassegna ogni regione di spawn per riempirsi subito e sposta il suo prossimo spawn a
ora, poi indica quante regioni e quanti NPC e oggetti sono coinvolti:

```text
Filling 4440 spawn regions: 29633 to spawn. The spawn messages show the progress.
```

Gli NPC arrivano al successivo controllo del timer `npc_spawn`, entro 10 secondi; ogni regione aggiunge
ciò che manca al suo `max`, indipendentemente dal suo `call` e da `[ultima.spawns] initial_fill`. I messaggi di spawn allo staff
e il log del server mostrano poi quanto è pieno il mondo. Una regione che non trova alcun punto
riprova un minuto dopo, continuando a riempirsi. Una regione che ha collocato solo alcuni dei suoi NPC ha finito
il riempimento: aggiunge il resto tramite il suo `call` ai tempi consueti. Usalo su un nuovo mondo o dopo aver rimosso molti NPC, invece di attendere il
riempimento graduale. Vedi [Spawn degli NPC](../spawns.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`spawns`](spawns.md)
- [`decorate`](decorate.md)
