# Template Validation

Use `moongate-template` to validate shard templates from the command line with the same loaders and cross-template checks used during Moongate startup.

This command should be part of the normal workflow whenever you edit shard data in your Moongate root directory, especially:

- `~/moongate/templates/items`
- `~/moongate/templates/mobiles`
- `~/moongate/templates/loot`
- `~/moongate/templates/factions`
- `~/moongate/templates/sell_profiles`
- `~/moongate/data/containers`

## Command

```bash
moongate-template validate --root-directory ~/moongate
```

`--root-directory` must point at the shard root, not directly at `templates/`. The validator reads both `templates/**` and `data/**`.

## Install From NuGet

```bash
dotnet tool install --global Moongate.TemplateValidator
moongate-template validate --root-directory ~/moongate
```

Update an existing global installation:

```bash
dotnet tool update --global Moongate.TemplateValidator
```

## Install From Local Package

```bash
dotnet pack tools/Moongate.TemplateValidator/Moongate.TemplateValidator.csproj -o ./tools/Moongate.TemplateValidator/nupkg
dotnet tool install --tool-path ./artifacts/template-tool --add-source ./tools/Moongate.TemplateValidator/nupkg Moongate.TemplateValidator
./artifacts/template-tool/moongate-template validate --root-directory ~/moongate
```

## What It Validates

- item template inheritance and references
- mobile template inheritance, equipment, and references
- loot table references
- faction references
- sell profile references
- book template references
- container layout references

## Expected Result

Success returns exit code `0` and prints a summary such as:

```text
Template validation completed successfully. ItemTemplates=1660, MobileTemplates=459.
```

Failures return exit code `1` and print the runtime validation errors so broken references or invalid template combinations can be fixed before server startup.

## Workflow Recommendation

Run the validator immediately after changing shard templates and before pushing or committing related work. This keeps the external shard root aligned with the runtime startup rules.
