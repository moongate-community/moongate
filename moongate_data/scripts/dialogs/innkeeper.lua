local dialogue = require("common.dialogue")

return dialogue.conversation("innkeeper", {
    start = "start",

    topics = {
        room = { "room", "stanza", "letto", "sleep" },
        rumor = { "rumor", "rumors", "gossip", "news", "voce" },
        food = { "food", "cibo", "mangiare", "ale", "beer" },
    },

    topic_routes = {
        room = "room_offer",
        rumor = "rumors",
        food = "food_offer",
    },

    nodes = {
        start = dialogue.node {
            text = "Benvenuto alla Locanda del Cervo Rosso. Cosa ti serve?",
            options = {
                dialogue.option { text = "Una stanza", goto_ = "room_offer" },
                dialogue.option { text = "Hai sentito voci?", goto_ = "rumors" },
                dialogue.option { text = "Da mangiare", goto_ = "food_offer" },
                dialogue.option { text = "Nulla, grazie", goto_ = "bye" },
            }
        },

        room_offer = dialogue.node {
            text = "Una stanza costa 15 monete d'oro.",
            options = {
                dialogue.option {
                    text = "Accetto",
                    condition = function(ctx)
                        return ctx:has_item("gold_coin", 15)
                    end,
                    effects = function(ctx)
                        ctx:remove_item("gold_coin", 15)
                        ctx:set_memory_flag("has_rented_room", true)
                        ctx:add_memory_number("rooms_rented", 1)
                        ctx:set_memory_text("last_service", "room")
                    end,
                    goto_ = "room_done"
                },
                dialogue.option { text = "No grazie", goto_ = "bye" },
            }
        },

        room_done = dialogue.node {
            text = "La stanza al piano di sopra e' tua per la notte.",
            options = {
                dialogue.option { text = "Grazie", goto_ = "bye" },
            }
        },

        rumors = dialogue.node {
            text = "Dicono che vecchie miniere a nord siano di nuovo abitate.",
            options = {
                dialogue.option {
                    text = "Interessante",
                    effects = function(ctx)
                        ctx:set_memory_flag("heard_mine_rumor", true)
                        ctx:set_memory_text("last_topic", "rumor")
                    end,
                    goto_ = "bye"
                },
            }
        },

        food_offer = dialogue.node {
            text = "Abbiamo stufato caldo e birra fresca.",
            options = {
                dialogue.option {
                    text = "Apri il negozio",
                    effects = function(ctx)
                        ctx:set_memory_text("last_service", "vendor")
                        ctx:say("Vediamo cosa posso servirti.")
                    end,
                    goto_ = "bye"
                },
                dialogue.option { text = "Magari dopo", goto_ = "bye" },
            }
        },

        bye = dialogue.node {
            text = "Buona permanenza.",
            options = {}
        }
    }
})
