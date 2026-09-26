# Contributing to Moongate

Contributions are welcome: bug reports, fixes, features, tests, documentation,
and examples. This guide explains how to prepare a contribution for review.

## Before you start

Search the [existing issues](https://github.com/moongate-community/moongate/issues)
and [pull requests](https://github.com/moongate-community/moongate/pulls) before
starting. Every feature starts with an issue that describes it in detail, as
[CODE_CONVENTION.md §13.1](CODE_CONVENTION.md) requires; the same goes for a change
to public APIs, packet behavior or persistence formats. Bug fixes and documentation
corrections can go directly into a pull request.

For a bug report, include the Moongate version or commit, operating system,
reproduction steps, expected and actual behavior, and relevant logs. Include the
client version for network issues. Remove credentials and personal data from
anything you share. Feature requests should explain the use case and intended
behavior, not just the proposed implementation.

## Set up your checkout

You need Git and the **.NET 10 SDK** to build the solution. Package verification
also requires Bash and access to nuget.org. For website work, use the Node version
in [website/.nvmrc](website/.nvmrc); Node is not needed for the C# build.

Fork the repository on GitHub, then create a branch from the current `develop`:

```sh
git clone https://github.com/YOUR-USERNAME/moongate.git
cd moongate
git remote add upstream https://github.com/moongate-community/moongate.git
git fetch upstream
git switch -c feature/short-description upstream/develop
```

Use a descriptive branch name, such as `fix/packet-length` or `docs/plugin-guide`.
For running a server, see the [Docker guide](docs/docker.md), including the
required Ultima Online client files and first-start configuration.

## Follow the project conventions

Read [CODE_CONVENTION.md](CODE_CONVENTION.md) and use the repository's
[.editorconfig](.editorconfig). They define C# style, namespaces, type placement,
test organization, and public interface documentation.

Keep changes focused on one problem. Follow the surrounding code and avoid
unrelated formatting or refactoring. Write documentation, public XML comments,
commit messages, and pull request descriptions in English. Discuss breaking
changes explicitly and explain their effect on library consumers and plugins.

Use Conventional Commits, for example:

```text
fix(network): reject invalid packet lengths
feat(persistence): add a collection query option
docs(plugins): clarify bundle deployment
```

## Verify your changes

Solution tests need isolated PostgreSQL and Redis servers. With Docker running, the
tests start them on their own with [Testcontainers](https://dotnet.testcontainers.org/):
one `postgres:17-alpine` and one `redis:7-alpine` (with `maxmemory-policy noeviction`)
per test process, removed when the run ends. The first run downloads the images.

To use servers of your own instead, as CI does, set these environment variables
through your secret provider; a set variable always wins over the container:

| Variable | Test dependency |
| --- | --- |
| `MOONGATE_TEST_POSTGRES_CONNECTION_STRING` | An administrative Npgsql connection to a disposable PostgreSQL server; the role must be able to create and drop test databases |
| `MOONGATE_TEST_REDIS_CONNECTION_STRING` | A StackExchange.Redis connection string for a disposable Redis 7+ server configured with `maxmemory-policy noeviction` |

Do not point these variables at a running shard's databases or Redis instance.
Without the variables and without Docker, the integration tests fail; they do not
silently skip. Run test projects serially with `-m:1`, as CI does, because their
hosts share the database services.

Run these commands from the repository root to match the solution checks in CI:

```sh
dotnet restore Moongate.slnx
dotnet build Moongate.slnx -c Release --no-restore
dotnet test Moongate.slnx -c Release --no-build -m:1
```

For opt-in concurrent database load tests, see [Stress-test PostgreSQL persistence](docs/persistence-stress.md).

Add or update tests for changed behavior. For bug fixes, include a regression test
that demonstrates the failure. Follow the test layout in `CODE_CONVENTION.md` and
keep assertions focused on observable behavior. Prose-only corrections do not
need new C# tests.

CI also verifies the portable administration client, NuGet packages, runnable README
examples, and third-party notices. The administration check uses the same test
connections and requires Python 3 with `venv` support and access to install the
dependencies in `samples/admin-python/requirements.txt`:

```sh
bash scripts/verify-admin-protos.sh
bash scripts/verify-packages.sh
./scripts/third-party-notices.sh
git diff --exit-code -- THIRD-PARTY-NOTICES.md
```

See [NuGet package verification](docs/nuget-packaging.md) for prerequisites and
troubleshooting. If a dependency change updates the notices, review and include
that update in your commit so the CI comparison is clean.

For documentation or website changes, run:

```sh
npm --prefix website ci
npm --prefix website test
npm --prefix website run build
```

The build checks local links, images, and heading fragments. Edit the original
Markdown sources rather than generated website files. See
[Writing documentation](docs/documentation.md) for preview commands and page
registration. Library README examples retain their `nuget-smoke` markers so
package verification can compile and run them.

## Submit a pull request

1. Commit your focused change and push the branch to your fork.
2. Open a pull request against **`moongate-community/moongate:develop`**.
3. Explain the problem, resulting behavior, relevant issue, and checks you ran.
   Call out compatibility changes and any checks you could not run.
4. Address review feedback and resolve CI failures before merge. Update the
   documentation when behavior or configuration changes.

Keep discussions respectful and focused on the change. Maintainers review and
merge contributions; opening a pull request does not require publishing packages
or creating a release.

The project's license is available in [LICENSE](LICENSE). Keep existing copyright
and license notices intact; dependency attribution is recorded in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
