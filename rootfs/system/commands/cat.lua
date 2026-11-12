-- Print the contents of files.
-- Usage: cat <file...>
-- Needs: files
local args = require("venture.args")

local options = args.parse({...}, {
  name = "cat",
  summary = "Print the contents of files.",
  arguments = { { name = "file", many = true } },
})
if not options then return end

for _, file in ipairs(options.file) do
  local text, problem = venture.files.read(file)
  if text then
    io.write(text)
    -- End on a new line, so the prompt doesn't follow the last line of a file without one.
    if #text > 0 and text:sub(-1) ~= "\n" then io.write("\n") end
  else
    print("cat: " .. problem)
  end
end
