# Docs Workspace

This folder contains Moongate v2 documentation sources.

## Main entry points

- `docs/index.md`: main docs home
- `docs/toc.yml`: DocFX navigation
- `docs/docfx.json`: DocFX build configuration

## Content layout

- `docs/articles/getting-started/`: onboarding and setup
- `docs/articles/architecture/`: server architecture and runtime model
- `docs/articles/scripting/`: Lua runtime and modules
- `docs/articles/persistence/`: snapshot/journal persistence
- `docs/articles/networking/`: packets and protocol notes
- `docs/articles/operations/`: runbooks and stress testing

## Local build

```bash
cd docs
dotnet tool update -g docfx
docfx docfx.json
docfx serve _site --port 8080
```

Open `http://localhost:8080`.

## Rule of thumb

Keep `README.md` concise and onboarding-focused.
Keep detailed technical material under `docs/articles/**`.
