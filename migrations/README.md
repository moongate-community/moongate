# Core migrations

Place reviewed SQL in `auth/NNNN_description.sql` or `world/NNNN_description.sql`.
The auth catalog contains the account ID sequence, the accounts table with its
unique username index, and the administration API access column. The world catalog has the mobiles and items tables. A player character's character-list
slot is unique per account, and a character the player deleted keeps its row, without a slot
and with the time of the request, until it is removed. A mobile keeps the direction it faces. Items on the ground are indexed by map for the startup load. An item in a container has its Enhanced Client grid slot. A mobile is flagged hidden or frozen, and has its hunger. The one-row `world.state` table keeps the props scripts set for the whole shard. An item is on the ground, in a
container item or worn by a mobile, checked by the database, and deleting a container
or a mobile deletes what it holds.
Plugin SQL ships inside each plugin bundle with a stable migration manifest.

Never edit an applied migration. Generate a draft against the previous schema,
review it, and use the separate migration runner to apply it.
See [the persistence guide](../docs/persistence.md).
