using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// Runs the shipped <c>venture.args</c> module from <c>src/Venture.OS/system/lib</c>, loaded with <c>require</c>.
/// </summary>
public sealed class ArgsLibraryTests
{
    private static readonly string LibraryDirectory = Path.Combine(AppContext.BaseDirectory, "system", "lib");

    // A declaration most tests share, and a helper that prints what parse returned, one value per line, sorted.
    private const string Greet = """
        local args = require("venture.args")
        local spec = {
          name = "greet",
          summary = "Greet someone.",
          arguments = { "name", { name = "rest", optional = true, many = true, summary = "More names." } },
          options = {
            loud = { short = "l", summary = "Shout it." },
            quiet = { short = "q" },
            times = { short = "t", type = "number", default = 1, summary = "How many times." },
            greeting = { default = "Hello" },
          },
        }
        local function show(words)
          local values = args.parse(words, spec)
          if not values then print("nil") return end
          local keys = {}
          for key in pairs(values) do keys[#keys + 1] = key end
          table.sort(keys)
          for _, key in ipairs(keys) do
            local value = values[key]
            if type(value) == "table" then value = "{" .. table.concat(value, ",") .. "}" end
            print(key .. "=" .. tostring(value))
          end
        end
        """;

    private readonly RecordingOutput output = new();

    [Fact]
    public void LoadsThroughRequire()
    {
        Run("print(type(require('venture.args').parse))");

        Assert.Equal(["function"], output.Lines);
    }

    [Fact]
    public void FillsInDefaults()
    {
        Show("'Tony'");

        Assert.Equal(["greeting=Hello", "loud=false", "name=Tony", "quiet=false", "rest={}", "times=1"], output.Lines);
    }

    [Theory]
    [InlineData("'Tony', '--loud', '--times', '3'")]
    [InlineData("'--times=3', 'Tony', '--loud'")]
    [InlineData("'-l', '-t', '3', 'Tony'")]
    [InlineData("'-lt3', 'Tony'")]
    [InlineData("'Tony', '-lt', '3'")]
    public void ReadsLongShortAndCombinedOptions(string words)
    {
        Show(words);

        Assert.Contains("loud=true", output.Lines);
        Assert.Contains("times=3", output.Lines);
        Assert.Contains("name=Tony", output.Lines);
    }

    [Fact]
    public void CombinesShortFlags()
    {
        Show("'-lq', 'Tony'");

        Assert.Contains("loud=true", output.Lines);
        Assert.Contains("quiet=true", output.Lines);
    }

    [Fact]
    public void GathersTheLastArgumentsIntoAList()
    {
        Show("'Tony', 'Ada', 'Grace'");

        Assert.Contains("name=Tony", output.Lines);
        Assert.Contains("rest={Ada,Grace}", output.Lines);
    }

    [Fact]
    public void TakesWordsAfterDoubleDashAsArguments()
    {
        Show("'--', '--loud', '-l'");

        Assert.Contains("name=--loud", output.Lines);
        Assert.Contains("rest={-l}", output.Lines);
        Assert.Contains("loud=false", output.Lines);
    }

    [Fact]
    public void TakesNegativeNumbersAsArguments()
    {
        Show("'-5', '--times', '-2'");

        Assert.Contains("name=-5", output.Lines);
        Assert.Contains("times=-2", output.Lines);
    }

    [Theory]
    [InlineData("", "greet: missing <name>")]
    [InlineData("'Tony', '--shout'", "greet: unknown option --shout")]
    [InlineData("'Tony', '-x'", "greet: unknown option -x")]
    [InlineData("'Tony', '--times'", "greet: --times needs a value")]
    [InlineData("'Tony', '-t'", "greet: -t needs a value")]
    [InlineData("'Tony', '--times', 'many'", "greet: --times expects a number, not \"many\"")]
    [InlineData("'Tony', '--loud=yes'", "greet: --loud doesn't take a value")]
    public void ExplainsMistakesWithTheUsage(string words, string message)
    {
        Show(words);

        Assert.Equal(
            [message, "Usage: greet [options] <name> [rest...]", "Type \"greet --help\" for more.", "nil"],
            output.Lines);
    }

    [Fact]
    public void RejectsExtraArguments()
    {
        Run("""
            local args = require("venture.args")
            print(select(2, args.parse({ "a", "b" }, { name = "one", arguments = { "only" } })))
            """);

        Assert.Equal("one: unexpected argument \"b\"", output.Lines[0]);
        Assert.Equal("unexpected argument \"b\"", output.Lines[^1]);
    }

    [Theory]
    [InlineData("'--help'")]
    [InlineData("'Tony', '-h'")]
    [InlineData("'-lh'")]
    public void PrintsGeneratedHelp(string words)
    {
        Show(words);

        Assert.Equal(
            [
                "Greet someone.",
                "Usage: greet [options] <name> [rest...]",
                "Arguments:",
                "  rest                     More names.",
                "Options:",
                "      --greeting <string>  (default: Hello)",
                "  -l, --loud               Shout it.",
                "  -q, --quiet",
                "  -t, --times <number>     How many times. (default: 1)",
                "  -h, --help               Show this help.",
                "nil",
            ],
            output.Lines);
    }

    [Fact]
    public void GivesTheUsageAndHelpAsText()
    {
        Run("""
            local args = require("venture.args")
            local spec = { name = "copy", arguments = { "from", "to" } }
            print(args.usage(spec))
            print(args.help(spec))
            """);

        Assert.Equal(["copy <from> <to>", "Usage: copy <from> <to>", "Options:", "  -h, --help  Show this help."],
            output.Lines);
    }

    [Theory]
    [InlineData("{}", "needs a name")]
    [InlineData("{ name = 'x', arguments = { { optional = true } } }", "argument 1 needs a name")]
    [InlineData("{ name = 'x', arguments = { { name = 'a', optional = true }, 'b' } }", "comes after an optional")]
    [InlineData("{ name = 'x', arguments = { { name = 'a', many = true }, 'b' } }", "only the last argument")]
    [InlineData("{ name = 'x', arguments = { 'a', 'a' } }", "declared twice")]
    [InlineData("{ name = 'x', arguments = { 'a' }, options = { a = {} } }", "declared twice")]
    [InlineData("{ name = 'x', options = { help = {} } }", "--help is built in")]
    [InlineData("{ name = 'x', options = { a = { short = 'h' } } }", "-h is built in")]
    [InlineData("{ name = 'x', options = { a = { short = 'ab' } } }", "one letter")]
    [InlineData("{ name = 'x', options = { a = { short = 'z' }, b = { short = 'z' } } }", "used twice")]
    [InlineData("{ name = 'x', options = { a = { type = 'date' } } }", "unknown type")]
    [InlineData("{ name = 'x', options = { ['two words'] = {} } }", "isn't a valid option name")]
    public void RaisesForMistakesInTheDeclaration(string spec, string message)
    {
        Run($"print(pcall(require('venture.args').parse, {{}}, {spec}))");

        var line = Assert.Single(output.Lines);
        Assert.StartsWith("false\t", line);
        Assert.Contains("venture.args: ", line);
        Assert.Contains(message, line);
    }

    [Fact]
    public void WorksFromAScriptCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"venture-args-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "greet.lua"), """
                -- Greet someone.
                local options = require("venture.args").parse({...}, {
                  name = "greet",
                  arguments = { "name" },
                  options = { loud = { short = "l" } },
                })
                if not options then return end
                print(options.loud and ("HELLO, " .. options.name:upper() .. "!") or ("Hello, " .. options.name .. "."))
                """);
            ScriptCommands commands = new(root, root, libraryDirectory: LibraryDirectory);
            commands.TryGet("greet", out var greet);

            greet!.Execute(new(output, new RecordingHost(), new()), ["Tony", "-l"]);

            Assert.Equal(["HELLO, TONY!"], output.Lines);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private void Show(string words) => Run($"{Greet}\nshow({{ {words} }})");

    private void Run(string code)
    {
        var finished = LuaRunner.Run(
            output, Path.GetTempPath(), lua => lua.DoString(code), libraryDirectory: LibraryDirectory);

        Assert.True(finished, string.Join('\n', output.Lines));
    }
}
