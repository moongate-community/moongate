# Conventions

The repository's [CODE_CONVENTION.md](https://github.com/moongate-community/moongate/blob/main/CODE_CONVENTION.md) is authoritative. This page is a working summary, not a replacement for it.

## Structure and types

- Match namespaces to folder paths exactly and organize folders by domain first. Use the prescribed namespace buckets such as `Types`, `Data`, `Interfaces`, `Services`, `Internal`, and `Subscribers` for their defined purposes.
- Put at most one primary class, record, or enum in a `.cs` file; match the file and type names and use file-scoped namespaces.
- Do not use primary constructors or expression-bodied constructors. Constructors must have a body.
- Order members as constants, private readonly fields, other fields, properties, constructors, public methods, protected methods, private methods, and disposal methods. Prefix private readonly fields with `_`, and keep disposal methods last.
- Put contracts only in `Interfaces` namespaces. Prefix interface names with `I` and add XML documentation to every interface.
- Put enums in the domain's `Types` namespace and include the domain in the enum name.

## Implementation practices

- Use `""` rather than `string.Empty`.
- Use Serilog statically with an inline `private readonly` logger from `Log.ForContext<T>()`; do not inject `ILogger<T>`. Use static structured-log templates rather than interpolated strings, and alias `Serilog.ILogger` when necessary.
- Name async methods with `Async`, accept a `CancellationToken` on public I/O-bound async methods, and use nullable reference types consistently.
- Prefer guard clauses, do not silently swallow exceptions, and expose read-only collection interfaces when callers should not mutate a collection.
- Keep files focused, remove dead code, avoid untracked TODOs and magic numbers, and keep analyzer warnings under control.

## Tests

- Place tests under `tests/Moongate.Tests/<Domain>/<Subdomain>` with the matching namespace.
- Name files and classes `<SubjectName>Tests` and methods `Method_Scenario_ExpectedResult`.
- Keep each test focused on one behavior. Put reusable fakes, builders, and helpers in `tests/Moongate.Tests/Support`.

## Commits

Use Conventional Commits such as `feat:`, `fix:`, `refactor:`, `test:`, and `docs:`. Add a subsystem scope when appropriate, for example `fix(socket):` or `test(eventbus):`. Keep each commit scoped to the affected subsystem, and do not add `Co-Authored-By: Claude` trailers.
