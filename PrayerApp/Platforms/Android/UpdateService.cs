using Com.Google.Android.Play.Core.Appupdate;
using Com.Google.Android.Play.Core.Install;
using Com.Google.Android.Play.Core.Install.Model;
using Com.Google.Android.Play.Core.Tasks;
using Microsoft.Maui.ApplicationModel;
using PrayerApp.Services;

namespace PrayerApp.Platforms.Android;

/// <summary>
/// Android Play Core implementation of IUpdateService.
/// Queries the Play Store directly — no server or JSON file required.
/// Internal / beta testers see new builds immediately after Google's build
/// processing completes (not during review).
/// </summary>
public class UpdateService : IUpdateService
{
    private const int UpdateRequestCode = 1001;

    public Task CheckForUpdateAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return Task.CompletedTask;

        var manager = AppUpdateManagerFactory.Create(activity);

        manager.AppUpdateInfo
            .AddOnSuccessListener(new SuccessListener<AppUpdateInfo>(info =>
            {
                bool updateAvailable =
                    info.UpdateAvailability() == UpdateAvailability.UpdateAvailable;
                bool flexibleAllowed =
                    info.IsUpdateTypeAllowed(AppUpdateType.Flexible);

                if (!updateAvailable || !flexibleAllowed) return;

                // Starts the Play Store flexible-update overlay (non-blocking).
                // The user can dismiss it; we never force a restart.
                manager.StartUpdateFlowForResult(
                    info,
                    AppUpdateType.Flexible,
                    activity,
                    UpdateRequestCode);

                // Watch for the download finishing so we can offer a restart prompt.
                manager.RegisterListener(new InstallStateListener(state =>
                {
                    if (state.InstallStatus() == InstallStatus.Downloaded)
                        PromptRestart(manager);
                }));
            }));

        // Fire-and-forget: listener callbacks arrive asynchronously on the main thread.
        return Task.CompletedTask;
    }

    // ── Restart prompt ────────────────────────────────────────────────────────

    private static void PromptRestart(IAppUpdateManager manager)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null) return;

            bool restart = await page.DisplayAlertAsync(
                "Update Ready",
                "A new version of Practicing Prayer has downloaded. Restart now to apply it.",
                "Restart Now",
                "Later");

            if (restart)
                manager.CompleteUpdate();
        });
    }

    // ── Play Core task-listener helpers ───────────────────────────────────────
    // Play Core uses its own Java Task type (not System.Threading.Tasks.Task),
    // so success/failure are delivered through listener callbacks rather than await.

    private sealed class SuccessListener<T> : Java.Lang.Object, IOnSuccessListener
        where T : Java.Lang.Object
    {
        private readonly Action<T> _action;
        public SuccessListener(Action<T> action) => _action = action;
        public void OnSuccess(Java.Lang.Object? result) => _action((T)result!);
    }

    private sealed class InstallStateListener : Java.Lang.Object, IInstallStateUpdatedListener
    {
        private readonly Action<InstallState> _action;
        public InstallStateListener(Action<InstallState> action) => _action = action;
        public void OnStateUpdate(InstallState? state)
        {
            if (state is not null) _action(state);
        }
    }
}
