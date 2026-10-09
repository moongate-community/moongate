<!-- translation: {"sourceHash":"5cfe13fc791ed522d2a942e2f419992ae01f36e6c4532c83a6d6f5d3d0822591","title":"hide"} -->

# hide

Ti nasconde in una nuvola di fumo.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `hide` | No | Sì | GameMaster | Game |

```text
.hide
```

Solo in gioco. Sei nascosto: i giocatori non ti vedono, e nemmeno lo staff di livello più basso del tuo; una nuvola di
fumo con il suo suono segna il punto, e leggi `You are hidden: players cannot see you.`. I tuoi passi non ti mostrano,
come non lo fanno mai quelli di un membro dello staff; nemmeno parlare. Se sei già nascosto, leggi
`You are already that way.` e non succede altro.

È lo stesso nascondersi dell'abilità Hiding, mantenuto finché non fai `unhide`. Un game master che fruga non ha bisogno
di abilità: vedi [Snooping](../scripting/shipped-scripts.md#snoopinglua).

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`unhide`](unhide.md)
