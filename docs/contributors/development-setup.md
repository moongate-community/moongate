# Development setup

Moongate development uses the .NET and Node.js toolchains. No particular IDE is required.

## Prerequisites

- .NET SDK `10.0.301`. The repository's `global.json` requires that feature band, allows newer patches in the same band, and does not allow prerelease SDKs.
- Node.js and npm. The documentation workflow currently uses Node.js `24.10.0`; using that version locally matches the repository automation.
- Git.

An editor or IDE with C# support can be useful, but the verified workflow uses only repository commands and does not depend on a specific product.

## Get the dependencies

From the repository root, restore the .NET solution and install the locked documentation dependencies:

```bash
dotnet restore Moongate.slnx
npm ci
```

`dotnet restore` resolves dependencies for all seven source projects and the test project in `Moongate.slnx`. `npm ci` installs the exact versions recorded in `package-lock.json`; use it instead of creating a new dependency resolution for routine setup.

Continue with [Build and test](build-and-test.md) to validate the environment.
