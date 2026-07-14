# Build and test

Run validation from the repository root. The following sequence is the verified Release workflow:

```bash
dotnet restore Moongate.slnx
dotnet build Moongate.slnx --configuration Release --no-restore
dotnet test Moongate.slnx --configuration Release --no-build
```

The solution commands cover all seven source projects and `tests/Moongate.Tests`. Restore first; `--no-restore` then prevents the build from resolving dependencies again, and `--no-build` makes the tests exercise the binaries produced by that Release build.

The test suite uses xUnit. Its Ultima-format tests create synthetic fixtures in temporary directories and serialize tests that change process-wide Ultima state. No proprietary Ultima Online client installation was required by the verified test run.

## Documentation

Install the locked Node dependencies and build the VitePress site:

```bash
npm ci
npm run docs:build
```

The documentation build renders the site and checks internal links because VitePress is configured with `ignoreDeadLinks: false`.

For local editing, an optional development server is available:

```bash
npm run docs:dev
```

Before submitting a change, run the commands relevant to it. For changes that can affect both code and documentation, run the complete sequence above.
