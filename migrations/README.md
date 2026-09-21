# Core migrations

Place reviewed SQL in `auth/NNNN_description.sql` or `world/NNNN_description.sql`.
No core persisted entities exist yet, so both catalogs initially contain no SQL.
Plugin SQL ships inside each plugin bundle with a stable migration manifest.

Never edit an applied migration. Generate a draft against the previous schema,
review it, and use the separate migration runner to apply it.
See [the persistence guide](../docs/persistence.md).
