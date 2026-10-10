<!-- translation: {"sourceHash":"c1917db48b6332feea1b98ee1b899309d5b63efb24771e38b92240a2916ce71b","title":"add_spell"} -->

# add_spell

Scrive un incantesimo, quelli di un cerchio o tutti e 64 nel libro degli incantesimi che selezioni.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `add_spell <key \| number \| circle N \| all>`, poi seleziona un libro o un mobile | No | Sì | GameMaster | Game |

```text
.add_spell magic_arrow
.add_spell 37
.add_spell circle 3
.add_spell all
```

Solo in gioco. Gli incantesimi vengono prima controllati; poi si apre un cursore di selezione. Scegli un libro degli
incantesimi, in uno zaino o a terra, oppure un personaggio o un NPC: riceve gli incantesimi nel libro che indossa, o
altrimenti nel primo che ha nello zaino (non in una borsa dentro lo zaino), la stessa ricerca che fa un lancio. Il comando
risponde quanti incantesimi erano nuovi: `Spells added to the spellbook: 4 new, 12 in the book now.`

- Un incantesimo si indica con la sua chiave (`magic_arrow`, maiuscole e minuscole non contano) o con il suo numero, da 1
  a 64, come li conta il client, quelli di [`spells.toml`](data-files/spells.md). `circle N` sono gli otto incantesimi del
  cerchio N, da 1 a 8, e `all` sono i 64.
- Gli incantesimi vengono dal catalogo: uno che non ha ancora uno script viene aggiunto lo stesso, e il libro lo
  contiene, anche se non si può lanciare.
- Un incantesimo che il libro contiene già non viene contato. Il libro viene mostrato di nuovo al suo proprietario, quindi
  un libro aperto riceve subito gli incantesimi.
- Un incantesimo sconosciuto risponde `Unknown spell: <word>`, un cerchio fuori da 1 a 8 risponde
  `Unknown circle: <word>. A circle is a number from 1 to 8.`, e nessuno dei due apre un cursore.
- Un mobile senza libro degli incantesimi risponde `Bran carries no spellbook.`; qualsiasi altro oggetto risponde
  `That is not a spellbook, a character or an NPC.`; annullare non cambia nulla.

## Vedi anche

- [Tutti i comandi](../commands.md)
- [`add_reagents`](add_reagents.md)
- [Magery](../magery.md)
