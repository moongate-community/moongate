# Repository Guidelines

## Scope and Precedence
- This file applies to the entire repository unless a deeper `AGENTS.md` overrides it.
- User instructions always win.
- A more specific `AGENTS.md` wins over this file for its subtree.
- `ui/AGENTS.md` is authoritative for files under `ui/`.
- `CODE_CONVENTION.md` is mandatory reading and remains the detailed source for C# and test conventions.
- This file intentionally embeds the repository owner's operating rules so contributors do not need any home-directory configuration to work correctly here.

## Mandatory Reading Before Changes
- Read `CODE_CONVENTION.md` before touching C# code or tests.
- Read `README.md` before non-trivial work so commands, runtime layout, and architecture stay aligned.
- Read `ui/AGENTS.md` before touching frontend code under `ui/`.

## Repository Map
- `src/`: production .NET projects and shared libraries.
- `tests/Moongate.Tests/`: automated tests mirroring production domains.
- `moongate_data/`: runtime data, Lua scripts, templates, world data, and web assets.
- `ui/`: Vite/React admin UI and player portal.
- `docs/`: user-facing and developer-facing documentation.
- `scripts/`: repository automation and tooling scripts.
- `benchmarks/`: BenchmarkDotNet projects and comparison tooling.
- `tools/`: standalone tools such as stress clients.
- `stack/`: local observability and container stack files.

## Working Principles
- Keep architecture KISS.
- Treat architecture quality and runtime performance as first-class constraints.
- Preserve existing domain boundaries and naming patterns.
- Prefer focused changes over wide refactors.
- Do not restructure unrelated areas while solving a local task.
- Keep runtime behavior, tests, and docs aligned.

## AOT, Architecture, and Performance
- This is an AOT-oriented project. Code must remain NativeAOT-friendly.
- Prefer explicit registrations, static wiring, and predictable runtime behavior over reflection-heavy or dynamic magic.
- Any reflection-heavy, runtime code generation, or serializer-dependent change must be clearly justified and verified against AOT constraints.
- Keep hot paths allocation-aware, especially in networking, packet handling, game-loop, persistence, and scripting boundaries.
- Choose designs that keep the architecture easy to reason about under load and easy to debug when performance regresses.

## Generated, Runtime, and Vendor Content
- Do not manually edit generated or vendor-managed output unless the user explicitly asks.
- Treat these as generated or local-only by default:
  - `docs/_site/`
  - `ui/dist/`
  - `ui/node_modules/`
  - `BenchmarkDotNet.Artifacts/`
  - `artifacts/`
  - runtime/cache/log/save/database folders under `moongate_data/`
  - runtime NPC memory files under `moongate_data/templates/npc_memories/*.txt` except `.gitkeep`

## C# Conventions Summary
- Follow `CODE_CONVENTION.md` in full.
- Keep namespace and folder path aligned.
- Organize code by domain first, not by technical suffix alone.
- Put enums in domain-specific `Types` namespaces and include the owning domain in the namespace when relevant (for example `...Types.Tcp`).
- Put DTOs and simple data carriers in `Data`.
- Put internal-only implementation data in `Data.Internal` or `Internal`, depending on the surrounding project pattern.
- Keep one primary type (`class`, `record`, or `enum`) per `.cs` file.
- Separate classes by domain responsibility and keep files aligned with that domain.
- Place interfaces under `Interfaces` namespaces only.
- Every interface must have XML docs (`///`).
- Within classes, keep `private readonly` fields first, then properties, then constructors.
- `Dispose` and `DisposeAsync` methods must be last.
- Do not use C# primary constructors.
- Do not use expression-bodied constructors.
- Constructors must always use block bodies (`{ }`).
- Keep files small, explicit, and easy to reason about under AOT constraints.

## Test Conventions Summary
- Organize tests by domain/component, mirroring production structure.
- Do not place new test files directly in the root of `tests/Moongate.Tests/`.
- Keep test namespaces aligned with folder paths.
- Preferred layout: `tests/<Project>/<Domain>/<Subdomain>/<Subject>Tests.cs` with namespace `<Project>.Tests.<Domain>.<Subdomain>`.
- Use one main subject per file.
- Name files and classes as `<SubjectName>Tests`.
- Keep integration, contract, and performance tests in dedicated projects or dedicated folders such as `Integration`, `Contract`, or `Performance`.
- Put shared helpers, fakes, and builders under `Support` or `TestSupport`.
- Every new test must follow the same taxonomy. If the domain folder does not exist yet, create it instead of falling back to a generic location.

## Workflow Rules
- Use Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, and similar).
- Never add `Co-Authored-By: Claude` to commit messages.
- Never commit planning or scratch workflow documents from `docs/plans/`, `docs/superpowers/`, or similar agent-only planning folders unless the user explicitly asks.
- This includes plan documents and workflow artifacts under `docs/plans/`, `docs/superpowers/`, and nested agent-generated files below those trees.
- When behavior changes, update the relevant docs if the repo already documents that area.

## Special User Commands
- `/vai`: inspect every modified file first. If there are no modified files, stage the untracked files. Then create the commit without asking for confirmation. Use a Conventional Commit subject and include a detailed body covering all relevant `feat`, `fix`, `refactor`, `test`, or `docs` items instead of a single generic line.
- `/comment`: add XML documentation comments (`///`) to interfaces.

## Validation Before Completion
- Run the narrowest meaningful validation for the area you changed before claiming completion.
- Backend and shared code changes should normally include targeted `dotnet test` coverage.
- Frontend changes must follow the validation gates in `ui/AGENTS.md`.
- If you could not run verification, say so explicitly.

## Repo-Specific Notes
- Changes under `moongate_data/` often need matching updates in Lua scripts, templates, tests, or docs.
- Treat asset names, template names, and script references as contract points. Keep them synchronized.
- NPC memory files under `moongate_data/templates/npc_memories/` are runtime-local state and must never be committed. Keep only `.gitkeep` tracked.
- Keep documentation grounded in current runtime behavior, not intended future behavior.
