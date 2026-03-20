# PluginTemplate

This project was generated from the `Moongate.Templates` package.

## Build

```bash
dotnet build
```

## Package References

This plugin starts with:

- `Moongate.Plugin.Abstractions` `__MOONGATE_PACKAGE_VERSION__`
- `Moongate.Server.Abstractions` `__MOONGATE_PACKAGE_VERSION__`

If you created the template with `--with-persistence`, the project also includes:

- `Moongate.Persistence` `__MOONGATE_PACKAGE_VERSION__`
- `Moongate.UO.Data` `__MOONGATE_PACKAGE_VERSION__`

## Packaging

The starter scripts publish the plugin, assemble the runtime plugin directory, and generate a zip:

```bash
bash scripts/pack-plugin.sh
```

PowerShell:

```powershell
pwsh ./scripts/pack-plugin.ps1
```

The output is written under:

- `artifacts/__PLUGIN_ID__/`
- `artifacts/__PLUGIN_ID__.zip`

Copy the folder to the runtime plugin location:

```text
plugins/__PLUGIN_ID__/
  manifest.json
  bin/
  data/
  scripts/
  assets/
```
