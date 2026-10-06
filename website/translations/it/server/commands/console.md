<!-- translation: {"sourceHash":"abc2688e42433aef7ef72e0e53e03081ce9c1283d73f40e25f5fc6172c1f2b32","title":"console"} -->

# console

Blocca di nuovo l'input della console, come all'avvio.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `console lock` | Sì | No | — | Ogni ruolo |

```text
console lock
```

La console parte bloccata: i tasti digitati per sbaglio non raggiungono alcun comando finché non viene premuto `*`. Una volta
sbloccata resta aperta; `console lock` la blocca di nuovo e indica il tasto che la sblocca:

```text
Console locked. Press '*' to unlock.
```

Mentre è bloccata, il primo tasto premuto diverso da `*` registra un avviso, una sola volta fino allo
sblocco successivo:

```text
Console input is locked. Press '*' to unlock.
```

Qualsiasi cosa diversa da `console lock` mostra l'uso.

## Vedi anche

- [Tutti i comandi](../commands.md)
