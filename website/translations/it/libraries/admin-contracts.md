<!-- translation: {"sourceHash":"4c7e39d23fca2ee123c8aa9f6163456c87c9f15951667726888d218b15b380af","title":"Moongate.Admin.Contracts"} -->

# Moongate.Admin.Contracts

API di amministrazione gRPC indipendente dal linguaggio (`moongate.admin.v1`).
Contiene client/basi dei servizi C# generati e definizioni grezze sotto
`proto/moongate/admin/v1/`. Questo pacchetto non dipende da server, persistenza o Redis.

Usa i file `.proto` grezzi con qualsiasi compilatore Protocol Buffer/gRPC. Gli import
standard `google/protobuf` sono forniti dalla distribuzione del compilatore.

## Esempio di serializzazione standalone

<!-- nuget-smoke:Program.cs -->

```csharp
using Google.Protobuf;
using Moongate.Admin.Contracts.V1;

var request = new ListAccountsRequest { PageSize = 50 };
var restored = ListAccountsRequest.Parser.ParseFrom(request.ToByteArray());
if (restored.PageSize != 50) throw new InvalidOperationException("Contract round-trip failed.");
Console.WriteLine($"{ListAccountsRequest.Descriptor.File.Package}:portable");
```

Genera client in altri linguaggi usando la directory `proto` come radice degli
include del compilatore. Gli ID degli account sono `uint32`; i timestamp usano
`google.protobuf.Timestamp`. Vedi la [guida all'amministrazione](../../docs/admin-api.md)
e il [client Python](../../samples/admin-python/README.md).
