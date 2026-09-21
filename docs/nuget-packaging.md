# NuGet libraries and package verification

Run the complete package check from the repository root:

```shell
bash scripts/verify-packages.sh
```

Requires .NET SDK 10.0.100 or later with .NET 10 support, Bash, a Git checkout,
and access to nuget.org for third-party dependencies. The C# verification tools
use .NET file-based apps and the standard library; no additional tool installation
is required. The native rendering example is exercised on Linux in CI.

## Packages

All packages target `net10.0` and share the version in `Directory.Build.props`,
managed by release-please. Each package includes its own English README, the
original Moongate logo, XML documentation, and a companion symbol package.

| Package and README | Purpose | Direct Moongate dependencies |
|---|---|---|
| [Moongate.Api](../src/Moongate.Api/README.md) | Typed MessagePack request/reply over mutual TLS with local authorization | Network |
| [Moongate.Core](../src/Moongate.Core/README.md) | Shared primitives, geometry, configuration, and utilities | None |
| [Moongate.Network](../src/Moongate.Network/README.md) | Standalone TCP transport, framing, and pipelines | None |
| [Moongate.Network.Packets](../src/Moongate.Network.Packets/README.md) | UO packet definitions and span-based serialization | Core |
| [Moongate.Persistence](../src/Moongate.Persistence/README.md) | Asynchronous PostgreSQL modules, transactions, schema operations, and typed entity access | Core, Persistence.Migrations |
| [Moongate.Persistence.Migrations](../src/Moongate.Persistence.Migrations/README.md) | Versioned SQL catalogs and immutable migration history validation | None |
| [Moongate.Scripting](../src/Moongate.Scripting/README.md) | Embedded Lua 5.2 runtime, attribute-bound modules, coroutine scheduling and editor definitions | Core, Server.Core |
| [Moongate.Server.Core](../src/Moongate.Server.Core/README.md) | Server and plugin contracts, events, and registrations | Api, Core, Network, Network.Packets |
| [Moongate.Ultima](../src/Moongate.Ultima/README.md) | UO client data readers and rendering utilities | None |

`Moongate.Server` and `Moongate.MigrationRunner` are executables distributed through release artifacts and
container images. They do not produce library packages. Tests and plugin fixtures
are also excluded from packing.

## What the command checks

1. Builds and packs the solution in Release into a new `artifacts/nuget.*` directory.
2. Checks exactly nine `.nupkg` and nine `.snupkg` files: metadata, dependencies,
   README bytes, original logo hash, DLL/XML payload, Portable PDB identity, and
   SourceLink pointing to the repository commit recorded in the package.
3. Extracts the marked C# examples from each README into nine temporary console apps
   outside the repository. Each app references one Moongate package directly;
   the persistence verifier references Npgsql to provision its isolated database,
   and the API example references MessagePack to generate its serializers.
4. Restores, builds, and runs those apps, checking their output. This exercises
   geometry, TCP lifecycle, packet encoding/decoding, PostgreSQL persistence,
   the event bus, typed API registration, and native SkiaSharp loading. Separate solution tests also start independent .NET processes to verify mutual TLS, local authorization, direct game access with login offline, and no replay after a lost response.

The persistence consumer requires `MOONGATE_TEST_POSTGRES_CONNECTION_STRING` as
an administrative Npgsql connection. It creates a unique
`moongate_test_nuget_<uuid>` database, runs the README example there, and drops
only that generated database. Missing configuration fails with an actionable
message; it never silently skips.

The current `FreeSql.Provider.PostgreSQL` 3.5.311 dependency resolves Npgsql
5.0.18. Keep that acknowledged provider constraint rather than silently
overriding Npgsql to another major. A provider/driver upgrade must pass the real
PostgreSQL package consumer and solution compatibility tests described here.

The solution's PostgreSQL fixtures use the same contract. Point it only at an
isolated test server whose admin role may create/drop databases:

```sh
MOONGATE_TEST_POSTGRES_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=...;Pooling=false' \
  dotnet test Moongate.slnx -c Release
```

Each fixture creates a unique `moongate_test_<uuid>` database and drops only that
database. Never use an operator Accounts or Realm connection for this variable.

Consumer apps use a new temporary NuGet cache for each invocation. Package source
mapping restricts `Moongate.*` to the generated local feed and resolves external
dependencies from nuget.org. Existing global packages cannot make a broken local
package appear to work. No UO client files, external game servers, or fixed
listening ports are needed.

## Output and troubleshooting

Successful runs print a `PASS` line per library for both archive and consumer
validation, followed by the package output directory. The generated archives remain
in that directory for inspection and are ignored by Git. Consumer workspaces are
removed after success. On failure, the tool prints the failing operation and retains
its temporary workspace for diagnosis.

To rerun a check against an existing output directory, pass its path explicitly:

```shell
dotnet run --file scripts/VerifyNuGetPackages.cs -- "$PWD" /path/to/package-directory
dotnet run --file scripts/VerifyNuGetConsumers.cs -- "$PWD" /path/to/package-directory
```

An empty symbol package indicates missing PDB output. The nine library projects set
Release `DebugType=portable` in their `.csproj` files, before MSBuild computes symbol
output items. Setting it later in `Directory.Build.targets` is insufficient. Shared
README, icon, and symbol-pack metadata are configured in that targets file. The
server retains embedded debug information.

A native rendering failure should be reproduced on the deployment platform with
its SkiaSharp native runtime. Linux CI does not certify Windows or macOS execution.

## CI and publishing

The Release configuration of `.github/workflows/ci.yml` runs the same verification
command after the solution tests. The existing third-party notices check remains
enabled. These checks do not publish anything or require a NuGet API key.

Publication is a separate responsibility of the existing release workflow.
Running the verification script does not create a release or push packages to a feed.
Packability is opt-in: the shared default is `IsPackable=false`, and only the nine
library projects explicitly enable it. Their existing `ProjectReference` entries
become NuGet dependencies instead of bundled copies of other project assemblies.

When changing an example, keep its `nuget-smoke` HTML comment directly above the
C# code fence so the consumer check continues to compile the documented code.
