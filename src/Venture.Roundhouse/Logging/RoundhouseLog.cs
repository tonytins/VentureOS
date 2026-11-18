using NLog;
using NLog.Config;
using NLog.Targets;

namespace Venture.Roundhouse.Logging;

/// <summary>
/// Roundhouse's log, through NLog, set up in code: a file a day in the person's own application data, kept for a week,
/// and <see cref="Buffer"/>, which the Log tab shows. It records what Roundhouse does (opening, saving, running),
/// what VentureOS's apps print while they run, what's refused, and every error that escapes, so a problem leaves a
/// trail to read.
/// </summary>
public static class RoundhouseLog
{
    /// <summary>How long a day's log file is kept.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(7);

    /// <summary>What every line looks like: the time, the level, who wrote it, the message, and an error's whole account.</summary>
    public const string Layout = "${date:format=yyyy-MM-dd HH\\:mm\\:ss} ${level:uppercase=true:padding=-5} ${logger}: ${message}${onexception:${newline}${exception:format=tostring}}";

    /// <summary>The lines the Log tab shows, the newest last.</summary>
    public static LogBuffer Buffer { get; } = new() { Layout = Layout };

    /// <summary>The folder the log files are in, or <see langword="null"/> while the log is only shown, not kept.</summary>
    public static string? Folder { get; private set; }

    /// <summary>The folder Roundhouse keeps its log in: its own, in the person's local application data.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roundhouse", "logs");

    /// <summary>A logger named for what writes to it, such as <c>Roundhouse</c> or <c>VentureOS</c>.</summary>
    public static Logger For(string name) => LogManager.GetLogger(name);

    /// <summary>
    /// Starts the log: always to <see cref="Buffer"/>, and to a file a day in <paramref name="folder"/> when one is
    /// given, after clearing the files older than <see cref="KeptFor"/>. A folder that can't be written is reported in
    /// the log, and it goes on without the file.
    /// </summary>
    public static void Configure(string? folder)
    {
        LoggingConfiguration configuration = new();
        configuration.AddRule(LogLevel.Info, LogLevel.Fatal, Buffer);

        string? problem = null;
        if (folder is not null)
        {
            try
            {
                Directory.CreateDirectory(folder);
                RemoveOld(folder, DateTime.Now - KeptFor);
                FileTarget file = new("file")
                {
                    FileName = Path.Combine(folder, "roundhouse-${shortdate}.log"),
                    Layout = Layout,
                    KeepFileOpen = true,
                };
                configuration.AddRule(LogLevel.Info, LogLevel.Fatal, file);
                Folder = folder;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                problem = $"The log can't be kept in {folder}: {exception.Message}";
                Folder = null;
            }
        }

        LogManager.Configuration = configuration;
        if (problem is not null)
            For("Roundhouse").Warn(problem);
    }

    /// <summary>Writes out what's been logged, before Roundhouse ends.</summary>
    public static void Flush() => LogManager.Flush(TimeSpan.FromSeconds(2));

    /// <summary>Deletes the log files last written before <paramref name="before"/>.</summary>
    /// <returns>How many it deleted.</returns>
    public static int RemoveOld(string folder, DateTime before)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "roundhouse-*.log"))
        {
            if (File.GetLastWriteTime(file) >= before)
                continue;
            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // One in use is left for next time.
            }
        }

        return removed;
    }
}

/// <summary>
/// The log's newest lines, at most <see cref="Capacity"/>, for the Log tab. NLog writes to it from whichever thread
/// logs, so it keeps its lines under a lock, and <see cref="Added"/> comes on that thread too.
/// </summary>
public sealed class LogBuffer : TargetWithLayout
{
    /// <summary>How many lines are kept; older ones go first.</summary>
    public const int Capacity = 2000;

    private readonly Queue<string> lines = new();
    private readonly Lock gate = new();

    public LogBuffer() => Name = "buffer";

    /// <summary>A line was logged.</summary>
    public event Action<string>? Added;

    /// <summary>The lines kept, oldest first.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (gate)
                return [.. lines];
        }
    }

    protected override void Write(LogEventInfo logEvent)
    {
        var line = RenderLogEvent(Layout, logEvent);
        lock (gate)
        {
            lines.Enqueue(line);
            while (lines.Count > Capacity)
                lines.Dequeue();
        }

        Added?.Invoke(line);
    }
}
