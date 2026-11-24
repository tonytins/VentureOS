-- Calculator: adds, subtracts, multiplies, and divides, as a pocket calculator
-- does. Each operator works on the running total, left to right, so
-- 2 + 3 × 4 = is 20.

local window = venture.ui.open("main.uix")

local MAX_DIGITS = 12

local entry = "0"   -- what the display shows
local total = nil   -- the value an operator applies to
local pending = nil -- the operator waiting for its second value
local fresh = true  -- whether the next digit starts a new number
local failed = false

-- Whole numbers without a decimal point, others with up to 10 significant digits.
local function format(number)
  if number ~= number or number == math.huge or number == -math.huge then return nil end
  if number == math.floor(number) and math.abs(number) < 1e12 then
    return string.format("%d", number)
  end
  return string.format("%.10g", number)
end

local function show() window:setText("display", entry) end

local function clear()
  entry, total, pending, fresh, failed = "0", nil, nil, true, false
  show()
end

local function fail()
  entry, total, pending, fresh, failed = "Error", nil, nil, true, true
  show()
end

local function apply(left, operator, right)
  if operator == "+" then return left + right end
  if operator == "-" then return left - right end
  if operator == "*" then return left * right end
  if right == 0 then return nil end
  return left / right
end

-- Works out what's pending, and shows it; false when it can't, such as dividing by zero.
local function settle()
  local result = format(apply(total, pending, tonumber(entry)) or 0 / 0)
  if not result then
    fail()
    return false
  end
  entry, total = result, tonumber(result)
  return true
end

local function digit(character)
  if failed then clear() end
  if fresh then
    entry, fresh = (character == "." and "0." or character), false
  elseif character == "." and entry:find(".", 1, true) then
    return
  elseif #entry:gsub("[%-%.]", "") >= MAX_DIGITS then
    return
  elseif entry == "0" and character ~= "." then
    entry = character
  else
    entry = entry .. character
  end
  show()
end

local function operator(symbol)
  if failed then return end
  if pending and not fresh then
    if not settle() then return end
  elseif not pending then
    total = tonumber(entry)
  end
  pending, fresh = symbol, true
  show()
end

local function equals()
  if failed or not pending then return end
  if settle() then
    pending, fresh = nil, true
    show()
  end
end

local function sign()
  if failed or entry == "0" then return end
  entry = entry:sub(1, 1) == "-" and entry:sub(2) or ("-" .. entry)
  show()
end

local function percent()
  if failed then return end
  entry = format(tonumber(entry) / 100)
  show()
end

for number = 0, 9 do
  window:on("d" .. number, function() digit(tostring(number)) end)
end
window:on("point", function() digit(".") end)
window:on("add", function() operator("+") end)
window:on("subtract", function() operator("-") end)
window:on("multiply", function() operator("*") end)
window:on("divide", function() operator("/") end)
window:on("equals", equals)
window:on("clear", clear)
window:on("sign", sign)
window:on("percent", percent)
