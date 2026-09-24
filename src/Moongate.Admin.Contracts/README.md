# Moongate.Admin.Contracts

Language-neutral gRPC administration API (`moongate.admin.v1`). Contains generated C# clients/service bases and raw definitions under `proto/moongate/admin/v1/`. This package has no server, persistence or Redis dependency.

Use the raw `.proto` files with any Protocol Buffer/gRPC compiler. Standard `google/protobuf` imports are provided by the compiler distribution.

## Standalone serialization example

<!-- nuget-smoke:Program.cs -->
```csharp
using Google.Protobuf;
using Moongate.Admin.Contracts.V1;

var request = new ListAccountsRequest { PageSize = 50 };
var restored = ListAccountsRequest.Parser.ParseFrom(request.ToByteArray());
if (restored.PageSize != 50) throw new InvalidOperationException("Contract round-trip failed.");
Console.WriteLine($"{ListAccountsRequest.Descriptor.File.Package}:portable");
```

Generate other-language clients using the `proto` directory as the compiler include root. Account IDs are `uint32`; timestamps use `google.protobuf.Timestamp`. See the [administration guide](../../docs/admin-api.md) and [Python client](../../samples/admin-python/README.md).
