-- venture.args: declare what a command takes, and get parsing, clear errors,
-- and a generated --help.
--
--   local args = require("venture.args")
--   local options = args.parse({...}, {
--     name = "greet",
--     summary = "Greet someone.",
--     arguments = { "name" },
--     options = {
--       loud = { short = "l", summary = "Shout it." },
--       times = { short = "t", type = "number", default = 1, summary = "How many times." },
--     },
--   })
--   if not options then return end
--   print(options.name, options.loud, options.times)
--
-- Arguments are taken in order. Each is a name, or a table:
--   { name = "file", optional = true, many = true, summary = "..." }
-- Once one is optional, the rest must be too, and only the last may take
-- many values, which arrive as a list.
--
-- Options are named, as --name value, --name=value, or -s value for one with a
-- short name. Their type is "flag" (on or off; the default), "string", or
-- "number"; a default of a string or number implies that type. Short flags
-- can be combined, as -lv. --help or -h prints the help, and -- ends the
-- options, so later words count as arguments even if they start with -.
--
-- parse returns one table with every argument and option by name. When the
-- user gets something wrong, or asks for --help, it prints that and returns
-- nil and the message. A mistake in the declaration itself is a Lua error,
-- since it's the command's bug, not the user's.

local args = {}

local TYPES = { flag = true, string = true, number = true }

local function declaration_error(message)
  error("venture.args: " .. message, 3)
end

-- Checks a declaration and fills in what it leaves out. Raises on mistakes.
local function prepare(spec)
  if type(spec) ~= "table" then
    declaration_error("the declaration must be a table")
  end
  if type(spec.name) ~= "string" or spec.name == "" then
    declaration_error("the declaration needs a name")
  end

  local prepared = { name = spec.name, summary = spec.summary, arguments = {}, options = {}, shorts = {} }
  local used = {}

  local seen_optional = false
  for index, entry in ipairs(spec.arguments or {}) do
    local argument = type(entry) == "string" and { name = entry } or entry
    if type(argument) ~= "table" or type(argument.name) ~= "string" or argument.name == "" then
      declaration_error("argument " .. index .. " needs a name")
    end
    if used[argument.name] then
      declaration_error('"' .. argument.name .. '" is declared twice')
    end
    if seen_optional and not argument.optional then
      declaration_error('"' .. argument.name .. '" is required but comes after an optional argument')
    end
    if argument.many and index ~= #spec.arguments then
      declaration_error('only the last argument can take many values, not "' .. argument.name .. '"')
    end
    used[argument.name] = true
    seen_optional = seen_optional or argument.optional
    prepared.arguments[index] = {
      name = argument.name,
      optional = argument.optional and true or false,
      many = argument.many and true or false,
      summary = argument.summary,
    }
  end

  local names = {}
  for name in pairs(spec.options or {}) do
    names[#names + 1] = name
  end
  table.sort(names)

  for _, name in ipairs(names) do
    local option = spec.options[name]
    if type(name) ~= "string" or not name:match("^[%a][%w%-]*$") then
      declaration_error('"' .. tostring(name) .. '" isn\'t a valid option name')
    end
    if name == "help" then
      declaration_error("--help is built in")
    end
    if used[name] then
      declaration_error('"' .. name .. '" is declared twice')
    end
    if type(option) ~= "table" then
      declaration_error("option --" .. name .. " must be a table")
    end

    local kind = option.type
    if kind == nil then
      kind = type(option.default) == "number" and "number"
        or type(option.default) == "string" and "string"
        or "flag"
    end
    if not TYPES[kind] then
      declaration_error('option --' .. name .. ' has an unknown type "' .. tostring(kind) .. '"')
    end

    local short = option.short
    if short ~= nil then
      if type(short) ~= "string" or not short:match("^%a$") then
        declaration_error("option --" .. name .. "'s short name must be one letter")
      end
      if short == "h" then
        declaration_error("-h is built in")
      end
      if prepared.shorts[short] then
        declaration_error("-" .. short .. " is used twice")
      end
      prepared.shorts[short] = name
    end

    local default = option.default
    if kind == "flag" then
      default = default and true or false
    end

    used[name] = true
    prepared.options[name] = { name = name, type = kind, short = short, default = default, summary = option.summary }
  end

  prepared.option_names = names
  return prepared
end

local function usage_of(prepared)
  local parts = { prepared.name }
  if #prepared.option_names > 0 then
    parts[#parts + 1] = "[options]"
  end
  for _, argument in ipairs(prepared.arguments) do
    local text = argument.many and (argument.name .. "...") or argument.name
    parts[#parts + 1] = argument.optional and ("[" .. text .. "]") or ("<" .. text .. ">")
  end
  return table.concat(parts, " ")
end

local function option_label(option)
  local label = (option.short and ("-" .. option.short .. ", ") or "    ") .. "--" .. option.name
  if option.type ~= "flag" then
    label = label .. " <" .. option.type .. ">"
  end
  return label
end

local function help_of(prepared)
  local lines = {}
  if prepared.summary then
    lines[#lines + 1] = prepared.summary
  end
  lines[#lines + 1] = "Usage: " .. usage_of(prepared)

  local described = {}
  for _, argument in ipairs(prepared.arguments) do
    if argument.summary then
      described[#described + 1] = { argument.name, argument.summary }
    end
  end

  local options = {}
  for _, name in ipairs(prepared.option_names) do
    local option = prepared.options[name]
    local text = option.summary or ""
    if option.type ~= "flag" and option.default ~= nil then
      text = text .. (text == "" and "" or " ") .. "(default: " .. tostring(option.default) .. ")"
    end
    options[#options + 1] = { option_label(option), text }
  end
  options[#options + 1] = { "-h, --help", "Show this help." }

  local width = 0
  for _, row in ipairs(described) do width = math.max(width, #row[1]) end
  for _, row in ipairs(options) do width = math.max(width, #row[1]) end

  local function add(rows)
    for _, row in ipairs(rows) do
      local line = "  " .. row[1] .. string.rep(" ", width - #row[1]) .. "  " .. row[2]
      lines[#lines + 1] = (line:gsub("%s+$", ""))
    end
  end

  if #described > 0 then
    lines[#lines + 1] = "Arguments:"
    add(described)
  end
  lines[#lines + 1] = "Options:"
  add(options)
  return table.concat(lines, "\n")
end

-- Turns a value from the command line into an option's type.
local function convert(option, text)
  if option.type == "number" then
    local number = tonumber(text)
    if number == nil then
      return nil, "--" .. option.name .. ' expects a number, not "' .. text .. '"'
    end
    return number
  end
  return text
end

-- The work of parse, returning the values, or nil and a user-facing message, or "help".
local function read(argv, prepared)
  local values = {}
  for name, option in pairs(prepared.options) do
    values[name] = option.default
  end

  local positional = {}
  local index = 1
  local only_arguments = false

  while index <= #argv do
    local word = argv[index]
    index = index + 1

    if only_arguments or word == "-" or word:sub(1, 1) ~= "-" or tonumber(word) then
      positional[#positional + 1] = word
    elseif word == "--" then
      only_arguments = true
    elseif word == "--help" or word == "-h" then
      return "help"
    elseif word:sub(1, 2) == "--" then
      local name, attached = word:match("^%-%-([^=]+)=(.*)$")
      name = name or word:sub(3)
      local option = prepared.options[name]
      if not option then
        return nil, "unknown option --" .. name
      end
      if option.type == "flag" then
        if attached then
          return nil, "--" .. name .. " doesn't take a value"
        end
        values[name] = true
      else
        local text = attached
        if text == nil then
          text = argv[index]
          index = index + 1
          if text == nil then
            return nil, "--" .. name .. " needs a value"
          end
        end
        local value, message = convert(option, text)
        if value == nil then
          return nil, message
        end
        values[name] = value
      end
    else
      -- One or more short options: flags can share a dash, and an option that
      -- takes a value takes the rest of the word, or the next word.
      local position = 2
      while position <= #word do
        local letter = word:sub(position, position)
        local name = prepared.shorts[letter]
        if letter == "h" then
          return "help"
        end
        if not name then
          return nil, "unknown option -" .. letter
        end
        local option = prepared.options[name]
        if option.type == "flag" then
          values[name] = true
          position = position + 1
        else
          local text = word:sub(position + 1)
          if text == "" then
            text = argv[index]
            index = index + 1
            if text == nil then
              return nil, "-" .. letter .. " needs a value"
            end
          end
          local value, message = convert(option, text)
          if value == nil then
            return nil, message
          end
          values[name] = value
          break
        end
      end
    end
  end

  local next_word = 1
  for _, argument in ipairs(prepared.arguments) do
    if argument.many then
      local list = {}
      for word_index = next_word, #positional do
        list[#list + 1] = positional[word_index]
      end
      next_word = #positional + 1
      if #list == 0 and not argument.optional then
        return nil, "missing <" .. argument.name .. "...>"
      end
      values[argument.name] = list
    elseif positional[next_word] ~= nil then
      values[argument.name] = positional[next_word]
      next_word = next_word + 1
    elseif not argument.optional then
      return nil, "missing <" .. argument.name .. ">"
    end
  end

  if positional[next_word] ~= nil then
    return nil, 'unexpected argument "' .. positional[next_word] .. '"'
  end

  return values
end

--- Parses argv (usually {...}) against a declaration.
-- Returns a table of values, or nil and a message after printing it.
function args.parse(argv, spec)
  local prepared = prepare(spec)
  if type(argv) ~= "table" then
    declaration_error("parse needs the command's words as a table, such as {...}")
  end
  for index = 1, #argv do
    if type(argv[index]) ~= "string" then
      declaration_error("word " .. index .. " isn't a string")
    end
  end

  local values, message = read(argv, prepared)
  if values == "help" then
    local help = help_of(prepared)
    print(help)
    return nil, help
  end
  if values == nil then
    print(prepared.name .. ": " .. message)
    print("Usage: " .. usage_of(prepared))
    print('Type "' .. prepared.name .. ' --help" for more.')
    return nil, message
  end
  return values
end

--- The usage line, such as "greet [options] <name>".
function args.usage(spec)
  return usage_of(prepare(spec))
end

--- The full --help text.
function args.help(spec)
  return help_of(prepare(spec))
end

return args
