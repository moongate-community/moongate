return {
    id = "orion",
    default_tick_ms = 1500,
    perception_range = 10,
    hearing_range = 12,
    think = function()
        ai.patrol()
    end,
}
