# C# administration client

This console example references only `Moongate.Admin.Contracts` and `Grpc.Net.Client`. It uses TLS, logs in, creates a test account, lists a bounded page and logs out. It does not retry mutations or print credentials.

Set `MOONGATE_ADMIN_ENDPOINT` (`https://host:2590`), `MOONGATE_ADMIN_USERNAME`, `MOONGATE_ADMIN_PASSWORD` from your secret store. For a private CA, set `MOONGATE_ADMIN_CA` to its public PEM certificate; otherwise system trust applies.

```sh
dotnet run --project samples/Moongate.Admin.Client
```

The created test account remains in Accounts. Its randomly generated password is not displayed.
