# Messages

`data/messages/<lang>.toml` holds the texts the server sends, one file per
language. Every `*.toml` file in the directory `data/messages/<lang>/` is merged
with it, so a language can be split into several files. `ILocalizationService`
reads them at runtime. See
[Localization](../localization.md) for the format, the English fallback and the
validation rules.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
