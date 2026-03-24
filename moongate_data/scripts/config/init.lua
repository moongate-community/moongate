reputation_titles_config = require("config.reputation_titles_default")

local ok, override = pcall(require, "config.reputation_titles")
if ok and type(override) == "table" then
    reputation_titles_config = override
end
