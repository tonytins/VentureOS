-- Write a line of text to a file.
-- Usage: write [-a] <file> [text...]
-- Needs: files
local args = require("venture.args")

local options = args.parse({...}, {
  name = "write",
  summary = "Write a line of text to a file, replacing what was there.",
  arguments = { "file", { name = "text", optional = true, many = true } },
  options = { append = { short = "a", summary = "Add to the end of the file instead." } },
})
if not options then return end

local ok, problem = venture.files.write(options.file, table.concat(options.text, " ") .. "\n", options.append)
if not ok then print("write: " .. problem) end
