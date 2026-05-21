using System;
using System.Collections.Concurrent;
using System.IO;

namespace OttoSynth.Core.Diagnostics;

/// <summary>
/// Logger thread-safe e fail-safe.
/// Funciona em duas vias:
///   1. Buffer em memória (ring buffer) que a UI consulta a 30fps.
///   2. Arquivo em disco (%APPDATA%/OttoSynth/log.txt).
///
/// Use <c>Log(level, msg)</c> de qualquer thread. NUNCA lança exceção
/// (engole tudo) — é seguro chamar do audio thread.
/// </summary>
public static class Logger
{
    public enum Level { Trace, Debug, Info, Warning, Error }

    private const int MaxBuffered = 500;
    private static readonly ConcurrentQueue<LogEntry> _buffer = new();
    private static readonly object _fileLock = new();
    private static string? _logFilePath;
    private static bool _writeToFile = false;
    private static Level _minLevel = Level.Info;

    public readonly record struct LogEntry(DateTime Timestamp, Level Level, string Source, string Message)
    {
        public override string ToString() =>
            $"[{Timestamp:HH:mm:ss.fff}] [{Level,-7}] [{Source}] {Message}";
    }

    /// <summary>Initializes the file-based logging. Safe to call multiple times.</summary>
    public static void Initialize(string? logPath = null)
    {
        try
        {
            _logFilePath = logPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OttoSynth", "log.txt");

            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Rotate if too big (> 5MB)
            try
            {
                var fi = new FileInfo(_logFilePath);
                if (fi.Exists && fi.Length > 5_000_000)
                {
                    var bak = _logFilePath + ".old";
                    File.Delete(bak);
                    File.Move(_logFilePath, bak);
                }
            }
            catch { /* ignore */ }

            _writeToFile = true;
            Info("Logger", "===== OttoSynth session started =====");
        }
        catch
        {
            // Initialize must NEVER throw
            _writeToFile = false;
        }
    }

    public static Level MinimumLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    public static void Trace(string source, string message) => Log(Level.Trace, source, message);
    public static void Debug(string source, string message) => Log(Level.Debug, source, message);
    public static void Info(string source, string message) => Log(Level.Info, source, message);
    public static void Warn(string source, string message) => Log(Level.Warning, source, message);
    public static void Error(string source, string message) => Log(Level.Error, source, message);

    public static void Error(string source, Exception ex, string? context = null)
    {
        string msg = context != null ? $"{context}: {ex.GetType().Name}: {ex.Message}" : $"{ex.GetType().Name}: {ex.Message}";
        Log(Level.Error, source, msg);
        if (ex.StackTrace != null)
            Log(Level.Error, source, ex.StackTrace);
    }

    /// <summary>Core log call. Catches all exceptions so it's safe from any thread.</summary>
    public static void Log(Level level, string source, string message)
    {
        if (level < _minLevel) return;
        try
        {
            var entry = new LogEntry(DateTime.UtcNow, level, source ?? "?", message ?? "");
            _buffer.Enqueue(entry);

            // Drop oldest if over limit
            while (_buffer.Count > MaxBuffered && _buffer.TryDequeue(out _)) { }

            if (_writeToFile && _logFilePath != null)
            {
                lock (_fileLock)
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, entry.ToString() + Environment.NewLine);
                    }
                    catch { /* swallow */ }
                }
            }
        }
        catch
        {
            // Logger NUNCA pode lançar
        }
    }

    /// <summary>Drains all currently buffered entries into the supplied callback. Safe from UI thread.</summary>
    public static void DrainTo(Action<LogEntry> callback)
    {
        while (_buffer.TryDequeue(out var entry))
        {
            try { callback(entry); }
            catch { /* swallow */ }
        }
    }

    /// <summary>Snapshots the current buffer without draining.</summary>
    public static LogEntry[] Snapshot()
    {
        return _buffer.ToArray();
    }

    /// <summary>Clears the in-memory buffer.</summary>
    public static void Clear()
    {
        while (_buffer.TryDequeue(out _)) { }
    }
}
