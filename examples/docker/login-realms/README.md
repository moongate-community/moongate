# Login and two game instances

Build and run three isolated Moongate processes from the local checkout.
**Server modes do not yet select different startup services. Account sharing,
realm discovery and login handoff are not implemented.**

1. In this directory, copy `.env.example` to `.env`.
2. Set `UO_DATA_PATH` to your absolute Ultima Online client directory outside the repository.
3. Run `docker compose up --build -d`.

See [the full guide](../../../docs/docker-login-realms.md) for ports, storage,
per-service lifecycle, internal APIs and troubleshooting.
