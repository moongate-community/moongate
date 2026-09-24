# Python administration client

Generate Python stubs from the raw `proto/moongate/admin/v1` tree shipped in the Contracts package/release artifact. No .NET SDK is required to use this client.

```sh
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
mkdir generated
.venv/bin/python -m grpc_tools.protoc -I ../../src/Moongate.Admin.Contracts/proto \
  --python_out=generated --grpc_python_out=generated \
  ../../src/Moongate.Admin.Contracts/proto/moongate/admin/v1/*.proto
PYTHONPATH=generated .venv/bin/python client.py
```

Supply `MOONGATE_ADMIN_ENDPOINT=https://host:2590`, `MOONGATE_ADMIN_USERNAME`, and `MOONGATE_ADMIN_PASSWORD` through your secret store/environment. Set `MOONGATE_ADMIN_CA` for a private CA and optionally `MOONGATE_ADMIN_GAME_ENDPOINT` to verify shared sessions on a Game host.

The client logs in, creates a test account, finds it with bounded paging, queries server information and logs out. It checks that the token is rejected afterward. The test account remains; its random password and the administration token are never printed. TLS validates hostname and trust chain; there are no automatic mutation retries.

Repository integration check: `bash scripts/verify-admin-protos.sh` uses a temporary virtual environment and real disposable server fixtures. Set the standard PostgreSQL/Redis integration-test environment variables first.
