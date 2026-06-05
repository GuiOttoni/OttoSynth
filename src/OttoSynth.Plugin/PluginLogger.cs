using System.IO;
using System.Text;

namespace OttoSynth.Plugin;

/// <summary>
/// Thread-safe, no-throw file logger for OttoSynth plugin diagnostics.
/// Writes to %APPDATA%\AudioPlugSharp\OttoSynth-{date}.log.
/// Format: [HH:mm:ss.fff] [TID] [LEVEL] [context] message
/// </summary>
internal static class PluginLogger
{
    private static readonly object _lock = new();
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioPlugSharp");

    private static string LogPath =>
        Path.Combine(_logDir, $"OttoSynth-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Log(string context, string message) =>
        Write("INFO ", context, message);

    public static void LogWarn(string context, string message) =>
        Write("WARN ", context, message);

    public static void LogException(string context, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            AppendHeader(sb, "ERROR", context);
            sb.AppendLine();
            for (var e = ex; e != null; e = e.InnerException)
                sb.AppendLine($"  {e.GetType().Name}: {e.Message}");
            if (ex.StackTrace != null)
                sb.AppendLine(ex.StackTrace);
            sb.AppendLine();
            Flush(sb.ToString());
        }
        catch { }
    }

    private static void Write(string level, string context, string message)
    {
        try
        {
            var sb = new StringBuilder();
            AppendHeader(sb, level, context);
            sb.AppendLine(message);
            Flush(sb.ToString());
        }
        catch { }
    }

    private static void AppendHeader(StringBuilder sb, string level, string context)
    {
        sb.Append($"[{DateTime.Now:HH:mm:ss.fff}] ");
        sb.Append($"[{Thread.CurrentThread.ManagedThreadId,4}] ");
        sb.Append($"[{level}] ");
        sb.Append($"[{context}] ");
    }

    private static void Flush(string text)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(_logDir);
                File.AppendAllText(LogPath, text, Encoding.UTF8);
            }
            catch { }
        }
    }
}
