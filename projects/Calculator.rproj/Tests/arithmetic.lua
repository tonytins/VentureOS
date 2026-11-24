-- The keys, pressed as a person would, and what the display shows after.

local keys = {
  ["+"] = "add", ["−"] = "subtract", ["×"] = "multiply", ["÷"] = "divide", ["="] = "equals",
  ["."] = "point", ["±"] = "sign", ["%"] = "percent", ["C"] = "clear",
}

local function press(app, presses)
  for key in presses:gmatch("%S+") do
    app:press(keys[key] or ("d" .. key))
  end
end

test.case("Opens on 0", function(app)
  test.equal(app:text("display"), "0")
  test.equal(#app:printed(), 0, "nothing printed")
end)

local sums = {
  { "2 + 3 =", "5" },
  { "2 + 3 × 4 =", "20" },
  { "7 ÷ 2 =", "3.5" },
  { "1 ÷ 3 =", "0.3333333333" },
  { "5 − 8 =", "-3" },
  { "1 . 5 × 2 =", "3" },
  { ". 5", "0.5" },
  { "1 . . 5", "1.5" },
  { "0 0 7", "7" },
  { "9 ±", "-9" },
  { "9 ± ±", "9" },
  { "5 0 %", "0.5" },
  { "2 + 3 + 4 =", "9" },
  { "2 + × 3 =", "6" },
  { "5 + =", "10" },
  { "5 ÷ 0 =", "Error" },
  { "5 ÷ 0 = 4", "4" },
  { "1 2 3 C", "0" },
  { "1 2 3 4 5 6 7 8 9 0 1 2 3", "123456789012" },
}

for _, sum in ipairs(sums) do
  test.case(sum[1] .. " shows " .. sum[2], function(app)
    press(app, sum[1])
    test.equal(app:text("display"), sum[2])
  end)
end
