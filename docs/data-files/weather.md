# Weather

`weather.toml` holds the weather profiles, named by climate. Maps and regions pick
one by name:

```toml
[[weather]]
name = "desert"
rain_chance = 1
snow_chance = 0
storm_chance = 0
snow_threshold = 0
min_temperature = 10
max_temperature = 30
cold_chance = 0
cold_temperature = 0
heat_chance = 80
heat_temperature = 35
rain_temperature_drop = 5
storm_temperature_drop = 10
```

| Field | Meaning |
| --- | --- |
| `name` | The name maps and regions use. |
| `rain_chance`, `snow_chance`, `storm_chance` | Chance (%) of that weather in the next hour, checked storm first, then snow, then rain. |
| `snow_threshold` | No snow at this temperature or above. |
| `min_temperature`, `max_temperature` | The range of a normal day. |
| `cold_chance`, `cold_temperature` | Chance (%) of a cold day, between `min_temperature` and `cold_temperature`. |
| `heat_chance`, `heat_temperature` | Chance (%) of a hot day, between `max_temperature` and `heat_temperature`. |
| `rain_temperature_drop`, `storm_temperature_drop` | How much rain or a storm lowers the temperature. |

The shipped profiles are `none`, `desert`, `tropical`, `temperate`, `highland`,
`stormy`, `mild`, `snowy`, `cool` and `rainy`. `none` never changes the weather.
The fields describe how weather is meant to work; no weather system reads them yet.

## Validation at startup

The server stops when:

- `weather.toml` does not exist or has no `[[weather]]` entries;
- a profile has no name, or a name is used twice (names are case-sensitive);
- a chance is outside 0 to 100;
- `min_temperature` is above `max_temperature`.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
