local statemachine = require("libs.statemachine")
local tick = require("common.tick")

test_state_brain = {}

local IDLE_DURATION_MS = 1500
local WANDER_DURATION_MS = 1000
local SPEAK_DURATION_MS = 1500
local LOOP_DELAY_MS = 250

local MESSAGES = {
    "Testing state machine brain.",
    "Idle, wander, speak.",
    "State transition complete.",
}

local state_by_npc = {}

local function get_state(npc_id)
    local state = state_by_npc[npc_id]
    if state ~= nil then
        return state
    end

    local machine = statemachine.create({
        initial = "idle",
        events = {
            { name = "start_wander", from = "idle", to = "wander" },
            { name = "start_speak", from = "wander", to = "speak" },
            { name = "finish_speak", from = "speak", to = "idle" },
        },
    })

    state = {
        machine = machine,
        cadence = tick.state({
            advance = IDLE_DURATION_MS,
        }, time.now_ms()),
    }

    state_by_npc[npc_id] = state
    return state
end

local function enter_state(state, duration_ms)
    tick.reset(state.cadence, "advance", time.now_ms(), duration_ms)
end

function test_state_brain.on_think(npc_id)
    while true do
        local npc = mobile.get(npc_id)

        if npc ~= nil then
            local state = get_state(npc_id)
            local machine = state.machine
            local now = time.now_ms()

            tick.run(state.cadence, "advance", now, function()
                if machine:is("idle") then
                    if machine:start_wander() then
                        enter_state(state, WANDER_DURATION_MS)
                        npc:move(random.direction())
                    end

                    return
                end

                if machine:is("wander") then
                    if machine:start_speak() then
                        enter_state(state, SPEAK_DURATION_MS)
                        local message = random.element(MESSAGES)
                        if message ~= nil then
                            npc:say(message)
                        end
                    end

                    return
                end

                if machine:is("speak") and machine:finish_speak() then
                    enter_state(state, IDLE_DURATION_MS)
                end
            end)
        end

        coroutine.yield(LOOP_DELAY_MS)
    end
end
