namespace PrayerApp.Services;

public class DiagnosticLog : IDiagnosticLog
{
    private const int MaxEntries = 100;
    private const string Delimiter = "---";
    private const string FileName = "diagnostics.log";

    private readonly string _logPath;
    private readonly object _lock = new();

    public DiagnosticLog(string logDirectory)
    {
        _logPath = Path.Combine(logDirectory, FileName);
    }

    public void Log(string category, Exception ex)
    {
        var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{category}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n{Delimiter}";
        lock (_lock)
        {
            File.AppendAllText(_logPath, entry + "\n");
        }
    }

    public void Log(string category, string message)
    {
        var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        lock (_lock)
        {
            File.AppendAllText(_logPath, entry + "\n");
        }
    }

    public string GetLogPath() => _logPath;

    public void Trim()
    {
        lock (_lock)
        {
            if (!File.Exists(_logPath)) return;

            var content = File.ReadAllText(_logPath);
            var entries = content.Split(
                new[] { Delimiter + "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            if (entries.Length <= MaxEntries) return;

            var trimmed = entries.Skip(entries.Length - MaxEntries);
            File.WriteAllText(_logPath,
                string.Join(Delimiter + "\n", trimmed) + Delimiter + "\n");
        }
    }
}
