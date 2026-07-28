local guard = {
    id = "guard",
    default_tick_ms = 1000,
    perception_range = 12,
    hearing_range = 15,
}

function guard.on_speech_heard(ctx, state, event)
    if event == nil or event.speaker_is_player ~= true then
        return nil
    end

    state.seen_players = state.seen_players or {}

    local speaker_id = event.speaker_id
    local known = state.seen_players[speaker_id]

    if known == nil then
        state.seen_players[speaker_id] = {
            first_seen_at = ctx.now_ms,
            last_seen_at = ctx.now_ms,
            conversations = 1,
        }

        ai.say("I haven't seen you before.")
        return
    end

    known.last_seen_at = ctx.now_ms
    known.conversations = known.conversations + 1

    ai.say("Welcome back.")
end

function guard.think()
-- idle: no action
end

return guard
