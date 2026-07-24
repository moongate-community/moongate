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

        return brain.say("Non ti avevo mai visto prima.")
    end

    known.last_seen_at = ctx.now_ms
    known.conversations = known.conversations + 1

    return brain.say("Bentornato.")
end

function guard.think()
    return brain.idle()
end

return guard
