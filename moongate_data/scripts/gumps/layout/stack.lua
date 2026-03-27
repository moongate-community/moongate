local stack = {}

local cursor_methods = {}

function cursor_methods:peek()
  return self.current_y
end

function cursor_methods:add(height, gap)
  local y = self.current_y

  self.current_y = self.current_y + (tonumber(height) or 0) + (tonumber(gap) or 0)

  return y
end

function cursor_methods:advance(amount)
  self.current_y = self.current_y + (tonumber(amount) or 0)

  return self.current_y
end

function stack.cursor(start_y)
  return setmetatable({
    current_y = tonumber(start_y) or 0
  }, {
    __index = cursor_methods
  })
end

return stack
