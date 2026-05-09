using Microsoft.Maui.Dispatching;

namespace PrayerApp.Services;

/// <summary>
/// Production implementation of INavigationService.
/// Delegates to Shell.Current for navigation and dialogs.
/// Registered as Singleton — stateless wrapper.
/// </summary>
public class ShellNavigationService : INavigationService
{
    // Cold-start gate for deep-link / file imports. App.xaml.cs hooks
    // Window.Activated — fires on cold launch after Shell is realized
    // (and on every warm resume) — and calls SignalShellReady on first
    // fire. TrySetResult is idempotent so re-fires are no-ops.
    private readonly TaskCompletionSource _shellReady = new();
    private readonly IDispatcher _dispatcher;

    public ShellNavigationService(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void SignalShellReady() => _shellReady.TrySetResult();

    public Task GoToAsync(string route)
        => Shell.Current.GoToAsync(route);

    public Task PushModalAsync(Page page)
        => Shell.Current.Navigation.PushModalAsync(page);

    public Task PopModalAsync()
        => Shell.Current.Navigation.PopModalAsync();

    public Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel)
        => Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    public Task DisplayAlertAsync(string title, string message, string ok)
        => Shell.Current.DisplayAlertAsync(title, message, ok);

    public Task<string?> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        => Shell.Current.DisplayActionSheetAsync(title, cancel, destruction, buttons);

    public Task WhenShellReadyAsync() => _shellReady.Task;

    public Task PushModalOnUiThreadAsync(Page page)
        => ModalPushSequence.ExecuteAsync(
            _dispatcher,
            _shellReady.Task,
            () => Shell.Current.Navigation.PushModalAsync(page));

    public Task PushModalWithNavigationBarAsync(Page page)
        => PushModalOnUiThreadAsync(new NavigationPage(page));
}
