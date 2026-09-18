![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network

Standalone asynchronous TCP transport with framing, middleware, and per-connection pipelines for .NET applications.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Network
```

## Features

- Asynchronous TCP server and client APIs.
- Access to the bound server endpoint, including an automatically assigned port.
- Configurable framing, transport codecs, and middleware.
- Per-connection pipelines for application-specific connection handling.

## Example

Start a loopback listener on an available port and stop it cleanly:

<!-- nuget-smoke:Program.cs -->
```csharp
using System.Net;
using Moongate.Network.Server;

await using var server = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0));
await server.StartAsync(CancellationToken.None);

if (server.Endpoint.Port == 0)
{
    throw new InvalidOperationException("The listener did not bind to a port.");
}

await server.StopAsync(CancellationToken.None);
Console.WriteLine("TCP listener started and stopped.");
```

## Dependencies and scope

This package depends on Serilog and has no dependency on other Moongate packages.

TCP is a byte stream. Configure framing for your application protocol; transport reads do not define game-packet boundaries. This library does not automatically decode Ultima Online packets or dispatch work into a game loop. Use `Moongate.Network.Packets` for UO packet definitions and serialization.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
