return {
    id = "vega",
    default_tick_ms = 1500,
    perception_range = 10,
    hearing_range = 12,
    think = function()
        return brain.patrol()
    end,
}
