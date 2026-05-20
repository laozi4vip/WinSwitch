using System.Runtime.CompilerServices;

namespace WinSwitch.Core.Services;

/// <summary>
/// 简易日志服务 — 支持 Info/Debug 两级输出
/// 运行时可通过托盘菜单切换
/// </summary>
public class LogService
{
    private static LogService? _instance;
    public static LogService Instance => _instance ??= new LogService();

    private readonly string _logDir;
    private readonly object _lock = new();
    private LogLevel _currentLevel = LogLevel.Info;

    public event Action<LogEntry>? LogAdded;

    public LogLevel CurrentLevel
    {
        get => _currentLevel;
        set => _currentLevel = value;
    }

    private LogService()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinSwitch", "logs");
        Directory.CreateDirectory(_logDir);
    }

    public void Info(string message, [CallerMemberName] string source = "")
        => Write(LogLevel.Info, message, source);

    public void Debug(string message, [CallerMemberName] string source = "")
        => Write(LogLevel.Debug, message, source);

    public void Warning(string message, [CallerMemberName] string source = "")
        => Write(LogLevel.Warning, message, source);

    public void Error(string message, [CallerMemberName] string source = "")
        => Write(LogLevel.Error, message, source);

    private void Write(LogLevel level, string message, string source)
    {
        if (level < _currentLevel)
            return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        LogAdded?.Invoke(entry);

        lock (_lock)
        {
            var logFile = Path.Combine(_logDir, $"winswitch_{DateTime.Now:yyyyMMdd}.log");
            var line = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Source}] {entry.Message}";
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}
