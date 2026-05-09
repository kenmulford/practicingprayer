using Microsoft.Maui.Dispatching;

namespace PrayerApp.Services;

/// <summary>
/// Encapsulates the gate-then-dispatch-then-push handshake required to
/// safely PushModalAsync from a non-UI thread on iOS, with a cold-start
/// wait for AppShell realization. Shared by INavigationService impls and
/// any service that originates a modal from a platform callback (deep-link
/// intent / open-url / iOS Window.Activated App Group reader).
///
/// Without the gate (Window.Activated TCS) PushModalAsync no-ops or throws
/// when a deep link fires before AppShell appears. Without the dispatch
/// hop PushModalAsync throws on iOS off the main thread.
/// </summary>
internal static class ModalPushSequence
{
    public static async Task ExecuteAsync(
        IDispatcher dispatcher,
        Task whenShellReady,
        Func<Task> pushModal)
    {
        await whenShellReady;
        var tcs = new TaskCompletionSource();
        dispatcher.Dispatch(async () =>
        {
            try
            {
                await pushModal();
            }
            finally
            {
                tcs.TrySetResult();
            }
        });
        await tcs.Task;
    }
}
