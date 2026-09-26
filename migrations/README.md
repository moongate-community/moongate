# Core migrations

Place reviewed SQL in `auth/NNNN_description.sql` or `world/NNNN_description.sql`.
The auth catalog contains the account ID sequence, the accounts table with its
unique username index, and the administration API access column. The world catalog has no core tables yet.
Plugin SQL ships inside each plugin bundle with a stable migration manifest.

Never edit an applied migration. Generate a draft against the previous schema,
review it, and use the separate migration runner to apply it.
See [the persistence guide](../docs/persistence.md).
