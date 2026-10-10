<!-- translation: {"sourceHash":"ea2a81bb4cefb8f9e2482fedb31cc7214139bf508404de3660a3f78d2f8dcc86","title":"Taming"} -->

# Taming

`taming.toml` elenca le creature che un giocatore può addomesticare con la skill [Animal Taming](../animal-taming.md).
Una creatura senza voce non può essere addomesticata. Senza il file non si può addomesticare nulla.

```toml
[[creature]]
template = "horse"
min_skill = 29.1
slots = 1
food = ["fruit", "grain"]
```

| Campo | Significato |
| --- | --- |
| `[[creature]]` | Uno per ogni creatura che può essere addomesticata. |
| `template` | L'id del suo template di mobile. Deve esistere, ed essere presente una sola volta. |
| `min_skill` | L'Animal Taming che richiede, in punti, da -50 a 120. Un tentativo ha una probabilità da 0,1 sotto a 49,9 sopra. Gli animali piccoli richiedono meno di niente. |
| `slots` | Per quanti follower conta, da 1 a 10; 1 se omesso. |
| `food` | I tipi di cibo che mangia una volta addomesticata, tra `meat`, `fruit`, `grain`, `fish` e `eggs`; `["meat"]` se omesso, `[]` per una che non ne mangia nessuno. Gli oggetti di ogni tipo sono in [`pet_food.toml`](pet-food.md). |

Un valore errato ferma il server all'avvio, indicando la creatura.

Il file è generato dalle classi delle creature di un altro emulatore (`Tamable`, `MinTameSkill`, `ControlSlots`, `FavoriteFood`):

```sh
cd tools/convert
uv run moongate-convert modernuo-taming --source <ModernUO>/Projects/UOContent \
  --templates ../../moongate_root/templates/mobiles --destination ../../moongate_root/data
```

Una classe senza template in questo server viene saltata e contata; i tre mantelli del cavallo (`brownhorse`, `grayhorse`,
`darkhorse`) prendono la skill del cavallo.
