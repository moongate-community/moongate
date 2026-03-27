# Moongate Template Validator

Validate shard templates from the command line using the same loaders and cross-template validation rules used by Moongate runtime startup.

## Usage

```bash
moongate-template validate --root-directory ~/moongate
```

Run the validator after changing shard templates in your Moongate root, especially `templates/items`, `templates/mobiles`,
`templates/loot`, `templates/factions`, `templates/sell_profiles`, and `data/containers`.

Each validation run prints the validator version and the target root directory before the validation summary.

## Install From NuGet

```bash
dotnet tool install --global Moongate.TemplateValidator
moongate-template validate --root-directory ~/moongate
```

## Install From Local Package

```bash
dotnet pack tools/Moongate.TemplateValidator/Moongate.TemplateValidator.csproj -o ./tools/Moongate.TemplateValidator/nupkg
dotnet tool install --tool-path ./artifacts/template-tool --add-source ./tools/Moongate.TemplateValidator/nupkg Moongate.TemplateValidator
./artifacts/template-tool/moongate-template validate --root-directory ~/moongate
```
