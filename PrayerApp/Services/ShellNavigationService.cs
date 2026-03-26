namespace PrayerApp.Services;

/// <summary>
/// Production implementation of INavigationService.
/// Delegates to Shell.Current for navigation and dialogs.
/// Registered as Singleton — stateless wrapper.
/// </summary>
public class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route)
        => Shell.Current.GoToAsync(route);

    public Task PopModalAsync()
        => Shell.Current.Navigation.PopModalAsync();

    public Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel)
        => Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    public Task DisplayAlertAsync(string title, string message, string ok)
        => Shell.Current.DisplayAlertAsync(title, message, ok);
}
