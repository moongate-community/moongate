<!-- translation: {"sourceHash":"ecfec58c795979cdf3cf1db63d98843d9fa4d15c6bf92e63b8a6e92dfd56656e","title":"Incantesimi"} -->

# Incantesimi

`spells.toml` elenca i 64 incantesimi di [Magery](../magery.md): come si chiama ciascuno, il suo cerchio, le parole di
potere, i reagenti, cosa chiede il cursore di mira, i suoi flag, il suo suono e la sua grafica, e la pergamena che lo
contiene. Cosa fa un incantesimo è il suo script, `scripts/spells/<key>.lua`; il mana, il ritardo e la finestra di
abilità vengono dal cerchio, nel server. Senza il file nessun incantesimo può essere lanciato.

```toml
[[spell]]
id = 5
key = "magic_arrow"
name = "Magic Arrow"
circle = 1
mantra = "In Por Ylem"
action = 17
reagents = [{ template = "0x0f8c_sulfurous_ash", amount = 1 }]
target = "mobile"
harmful = true
resistable = true
reflectable = true
sound = 0x01E5
effect = 0
effect_duration = 0
projectile = 0x36E4
projectile_speed = 5
prompt = "Select target for magic arrow."
scroll = "0x1f32_magic_arrow_scroll"
enabled = true
```

| Campo | Significato |
| --- | --- |
| `[[spell]]` | Uno per incantesimo, nell'ordine del libro del client. |
| `id` | Il numero del client, da 1 a 64: il bit del libro e ciò che nomina una richiesta di lancio. Una sola volta. |
| `key` | Il nome del suo script e dell'incantesimo negli script: lettere minuscole, cifre e trattini bassi. Una sola volta. |
| `name` | Il nome che leggono i giocatori. |
| `circle` | Da 1 a 8. |
| `mantra` | Le parole di potere dette sopra la testa del lanciatore. |
| `action` | L'animazione di un lanciatore umano: 16 o 17. |
| `reagents` | I template degli oggetti e quanti di ciascuno un lancio prende dallo zaino (una pergamena porta i suoi). Ognuno deve essere un template di oggetto. |
| `target` | Cosa chiede il cursore: `none`, `mobile`, `item` o `location`. |
| `harmful` | L'incantesimo ferisce o maledice: il suo cursore è quello dannoso. |
| `resistable` | Resisting Spells può indebolirlo. |
| `reflectable` | Magic Reflection può rimandarlo indietro. |
| `cast_delay_scale` | Quante volte il ritardo di lancio del suo cerchio impiega l'incantesimo, sopra 0 e al massimo 10; 1 se omesso. Blade Spirits e Summon Creature hanno 4, perché il gioco classico li rallentava. |
| `sound` | Il suono dell'incantesimo, 0 per nessuno. |
| `effect`, `effect_duration` | La grafica riprodotta sul bersaglio e per quanto, 0 per nessuna. |
| `projectile`, `projectile_speed` | La grafica che vola dal lanciatore al bersaglio e quanto veloce, 0 per nessuna. |
| `prompt` | Il testo del cursore di mira, vuoto per un incantesimo senza bersaglio. |
| `scroll` | Il template della pergamena che contiene l'incantesimo; la sua grafica è ciò che distingue una pergamena dall'altra. |
| `enabled` | `false` toglie l'incantesimo dal gioco. |

Un valore errato blocca il server all'avvio, nominando l'incantesimo. Un incantesimo senza `scripts/spells/<key>.lua` si
carica e dice di essere disabilitato quando viene lanciato.

Il file è generato dal `spells.dfn` di UOX3:

```sh
cd tools/convert
uv run moongate-convert uox-spells --source <UOX3>/data/dfndata/spells \
  --items ../../moongate_root/templates/items --destination ../../moongate_root/data
```

La pergamena di ogni incantesimo è il template canonico della sua grafica e ogni reagente è un template canonico, quindi la
conversione fallisce quando uno manca. Il bersaglio degli incantesimi ad area e di quelli che prendono una runa lo
imposta il convertitore, non i flag di UOX3, che li segnano come un personaggio.
