# pages

Opens the queue of the requests for the game masters: the open and taken ones, the oldest first, ten a
page. A row opens the request, where the game master can go to the player, take it, answer it or close it.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `pages` | No | Yes | GameMaster | Game |

```text
.pages
```

In game only. The players send the requests from the Help button of the paperdoll, with
*Call a game master*; see [Help](../help.md) for what a request holds, how the answer reaches the
player and the settings of the queue.

```text
Help requests
[>] #1 Gino, Bug, 3 min, open
[>] #2 Pina, Harassment, now, taken by Gino
```

The rank is checked again by every button, so a game master demoted while the gump is open can do
nothing. A request closed meanwhile by another game master says `Request 1 is already closed.`
