using System.Diagnostics.CodeAnalysis;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// Commands registered while the system runs, with <c>venture.command</c>, by a <see cref="LuaOwner"/>.
/// </summary>
/// <remarks>
/// <para>Add it to the registry before the script commands, so a runtime command wins a lookup. Who may take which
/// name:</para>
/// <list type="bullet">
/// <item><description>Nobody can take a built-in command's name: it would never run.</description></item>
/// <item><description>A name another owner registered is taken until that owner ends or removes it. The same owner
/// registering a name again replaces its earlier command.</description></item>
/// <item><description>Only an owner with <see cref="LuaOwner.MayReplaceScripts"/>, which is code the user types,
/// may take a system script command's name, and the command then says it replaces that script.</description></item>
/// </list>
/// </remarks>
/// <param name="isBuiltIn">Whether a name belongs to a built-in command, such as <see cref="CommandRegistry.IsBuiltIn"/>.</param>
/// <param name="scripts">The script commands a runtime command could replace, or <see langword="null"/>.</param>
/// <param name="stop">What can stop a running command, such as a key press; see <see cref="LuaRunner.Run"/>.</param>
public sealed class RuntimeCommands(
    Func<string, bool> isBuiltIn, ICommandSource? scripts = null, Func<string?>? stop = null) : ICommandSource
{
    private readonly Func<string, bool> isBuiltIn = isBuiltIn ?? throw new ArgumentNullException(nameof(isBuiltIn));
    private readonly Dictionary<string, RuntimeCommand> commands = [];

    public IReadOnlyList<IShellCommand> Commands => [.. commands.Values];

    public bool TryGet(string name, [NotNullWhen(true)] out IShellCommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);

        command = commands.TryGetValue(name.ToLowerInvariant(), out var found) ? found : null;
        return command is not null;
    }

    /// <summary>
    /// Registers a command whose <c>run</c> function is at <paramref name="reference"/> in the owner's Lua
    /// registry, and records its undo with the owner.
    /// </summary>
    /// <returns>Why the name can't be taken, or <see langword="null"/> once it's registered.</returns>
    internal string? Register(
        LuaOwner owner, string name, string? summary, string? usage, int reference, out RuntimeCommand? command)
    {
        command = null;

        if (!CommandRegistry.IsValidName(name))
            return $"\"{name}\" isn't a valid command name: use lowercase letters, digits, and hyphens.";

        if (isBuiltIn(name))
            return $"\"{name}\" is a built-in command and can't be replaced.";

        if (commands.TryGetValue(name, out var existing))
        {
            if (existing.Owner != owner)
                return $"\"{name}\" is already registered by {existing.Owner.Name}.";

            // The same owner registering again, as when trying a new version at the prompt.
            existing.Undo?.Run();
        }

        var replacesScript = scripts is not null && scripts.TryGet(name, out _);
        if (replacesScript && !owner.MayReplaceScripts)
            return $"\"{name}\" is a system command, and {owner.Name} can't replace it.";

        RuntimeCommand created = new(this, owner, name, summary, usage, reference, replacesScript, stop);
        commands[name] = created;

        var state = owner.Interpreter.State;
        created.Undo = owner.AddUndo(() =>
        {
            if (commands.TryGetValue(name, out var current) && current == created)
                commands.Remove(name);

            state.L_Unref(Cosmos.Executable.Lua.LuaDef.LUA_REGISTRYINDEX, reference);
        });

        command = created;
        return null;
    }

    /// <summary>A command registered at runtime, which runs its Lua function in its owner's interpreter.</summary>
    internal sealed class RuntimeCommand(
        RuntimeCommands source,
        LuaOwner owner,
        string name,
        string? summary,
        string? usage,
        int reference,
        bool replacesScript,
        Func<string?>? stop) : IRuntimeCommand
    {
        public string Name { get; } = name;
        public string Summary { get; } = string.IsNullOrEmpty(summary) ? ScriptCommands.NoSummary : summary;
        public string Usage { get; } = string.IsNullOrEmpty(usage) ? $"{name} [arguments]" : usage;
        public int MinArguments => 0;
        public int MaxArguments => int.MaxValue;

        public string From { get; } = replacesScript ? $"{owner.Name}, replacing the {name} script" : owner.Name;

        public bool ReplacesScript { get; } = replacesScript;

        public LuaOwner Owner { get; } = owner;

        public LuaOwner.Undo? Undo { get; set; }

        public void Execute(ShellContext context, IReadOnlyList<string> arguments)
        {
            // The owner may have ended while the command was being found, such as a prompt that just closed.
            if (Owner.IsDisposed || !source.commands.TryGetValue(Name, out var current) || current != this)
            {
                context.Output.WriteLine($"\"{Name}\" is no longer registered.");
                return;
            }

            Owner.Call(reference, arguments, context.Output, context.CurrentDirectory, stop);
        }
    }
}

/// <summary>
/// A command registered while the system runs, which says who registered it.
/// </summary>
public interface IRuntimeCommand : IShellCommand
{
    /// <summary>Who registered it, such as <c>init.lua</c>, and the script it replaces, if any.</summary>
    string From { get; }
}
