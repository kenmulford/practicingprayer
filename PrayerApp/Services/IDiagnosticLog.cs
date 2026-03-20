namespace PrayerApp.Services;

public interface IDiagnosticLog
{
    void Log(string category, Exception ex);
    string GetLogPath();
    void Trim();
}
