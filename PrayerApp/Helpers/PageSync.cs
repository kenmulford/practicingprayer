using PrayerApp.ViewModels;

namespace PrayerApp.Helpers;

/// <summary>
/// Single OnAppearing pipeline shared across list pages. Replaces the per-page
/// `_loaded` flag plus the LoadAsync/RefreshAsync branch with a single call.
/// Awaits <see cref="App.InitTask"/> for DB readiness, then invokes
/// <see cref="ISyncableViewModel.SyncAsync"/>.
/// </summary>
public static class PageSync
{
    /// <summary>Call from <c>OnAppearing</c> on any page whose ViewModel implements <see cref="ISyncableViewModel"/>.</summary>
    public static async Task OnAppearingAsync(ISyncableViewModel vm)
    {
        await App.InitTask;
        await vm.SyncAsync();
    }
}
