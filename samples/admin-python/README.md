# Python administration client

Generate Python stubs from the raw `proto/moongate/admin/v1` tree in the checkout or Contracts release archive. No .NET SDK is required. From the repository root:

```sh
cd samples/admin-python
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
mkdir -p generated
.venv/bin/python -m grpc_tools.protoc -I ../../src/Moongate.Admin.Contracts/proto \
  --python_out=generated --grpc_python_out=generated \
  ../../src/Moongate.Admin.Contracts/proto/moongate/admin/v1/*.proto
PYTHONPATH=generated .venv/bin/python client.py
```

To use `moongate-admin-protos-VERSION.zip` instead, keep this sample's `client.py` and `requirements.txt`. From `samples/admin-python`, extract the archive and replace the `grpc_tools.protoc` command above with:

```sh
unzip /path/to/moongate-admin-protos-VERSION.zip -d admin-protos
.venv/bin/python -m grpc_tools.protoc -I admin-protos/proto \
  --python_out=generated --grpc_python_out=generated \
  admin-protos/proto/moongate/admin/v1/*.proto
PYTHONPATH=generated .venv/bin/python client.py
```

Supply `MOONGATE_ADMIN_ENDPOINT=https://host:2590`, `MOONGATE_ADMIN_USERNAME`, and `MOONGATE_ADMIN_PASSWORD` through your secret store/environment. Set `MOONGATE_ADMIN_CA` to the public PEM trust file for a private CA. With [mgboot certificate setup](../../docs/mgboot.md#generate-an-administration-certificate), use a local copy of the server's public `certificates/admin.crt`; keep `admin.pfx` on the server and use a hostname or IP included in the certificate. Optionally set `MOONGATE_ADMIN_GAME_ENDPOINT` to verify shared sessions on a Game host; its certificate must also be trusted.

The client logs in, creates a test account, finds it with bounded paging, queries server information and logs out. It checks that the token is rejected afterward. The test account remains; its random password and the administration token are never printed. TLS validates hostname and trust chain; there are no automatic mutation retries.

Repository integration check: from the repository root, `bash scripts/verify-admin-protos.sh` uses a temporary virtual environment and disposable server fixtures. Set `MOONGATE_TEST_POSTGRES_CONNECTION_STRING` and `MOONGATE_TEST_REDIS_CONNECTION_STRING` first; see [Verify your changes](../../CONTRIBUTING.md#verify-your-changes) for the test setup.
