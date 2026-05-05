namespace PrayerApp.Services;

public interface IDiagnosticLog
{
    void Log(string category, Exception ex);
    void Log(string category, string message);
    string GetLogPath();
    void Trim();
}
