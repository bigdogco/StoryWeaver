using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm.Logging;

/// <summary>
/// Appends to a per-session file. One file per run keeps a session's traffic together, which
/// is what you want when reconstructing why a particular turn went wrong.
/// </summary>
public sealed class FileLlmLog : ILlmLog
{
    private readonly string _filePath;
    private readonly bool _logPrompts;
    private readonly bool _logResponses;
    private readonly object _gate = new();

    public FileLlmLog(LoggingSettings settings)
    {
        _logPrompts = settings.LogPrompts;
        _logResponses = settings.LogResponses;

        Directory.CreateDirectory(settings.LogDirectory);
        _filePath = Path.Combine(
            settings.LogDirectory,
            $"session-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    }

    /// <summary>Path of the file being written. Worth printing at startup.</summary>
    public string FilePath => _filePath;

    public void Prompt(LlmRole role, string payload)
    {
        if (_logPrompts)
        {
            Write($"REQUEST [{role}]", payload);
        }
    }

    public void Response(LlmRole role, string payload)
    {
        if (_logResponses)
        {
            Write($"RESPONSE [{role}]", payload);
        }
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string header, string body)
    {
        string entry =
            $"--- {DateTime.Now:HH:mm:ss.fff}  {header} ---{Environment.NewLine}" +
            $"{body}{Environment.NewLine}{Environment.NewLine}";

        // Logging must never take down a turn. A failed write is worth less than the session.
        try
        {
            lock (_gate)
            {
                File.AppendAllText(_filePath, entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Intentionally swallowed.
        }
    }
}
