namespace PrayerApp.ViewModels;

/// <summary>
/// List-page ViewModel contract for the loading-and-refresh architecture.
/// Implementations expose a single <see cref="SyncAsync"/> primitive that
/// brings the VM in line with the underlying data store, replacing the prior
/// LoadAsync/RefreshAsync split. <see cref="IsLoading"/> is toggled around the
/// work so a page-level <c>LoadingOverlay</c> can render.
///
/// Pages call <c>this.OnAppearingAsync(vm)</c> from their <c>OnAppearing</c>;
/// VM constructors register messenger subscribers that invoke
/// <see cref="SyncAsync"/> on relevant entity-change broadcasts.
///
/// Implementations must be idempotent — diffing against current state, not
/// clearing-and-repopulating — so first-call and Nth-call paths are the same.
/// </summary>
public interface ISyncableViewModel
{
    bool IsLoading { get; }
    Task SyncAsync();
}
