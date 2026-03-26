namespace PrayerApp.Services;

/// <summary>
/// Abstracts Shell navigation and user-facing dialogs for testability.
/// ViewModels inject this instead of calling Shell.Current directly.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route);
    Task PopModalAsync();
    Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel);
    Task DisplayAlertAsync(string title, string message, string ok);
}
