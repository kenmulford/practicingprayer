#if IOS
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PrayerApp.Services;
using PrayerApp.Views;

namespace PrayerApp.Platforms.iOS;

/// <summary>
/// iOS DI-registered facade over <see cref="AppGroupImportOrchestrator"/>.
/// Resolves <see cref="ConfirmImportPage"/> from the service provider when
/// the orchestrator needs a page to push.
/// </summary>
public sealed class AppGroupImportDispatcher
{
    private readonly AppGroupImportOrchestrator _orchestrator;

    public AppGroupImportDispatcher(
        IAppGroupContainerProvider container,
        IImportPayloadService payloadService,
        INavigationService navigation,
        IDispatcher uiDispatcher,
        IServiceProvider serviceProvider)
    {
        _orchestrator = new AppGroupImportOrchestrator(
            container,
            payloadService,
            navigation,
            uiDispatcher,
            () => serviceProvider.GetRequiredService<ConfirmImportPage>());
    }

    public Task CheckPendingAsync() => _orchestrator.CheckPendingAsync();
}
#endif
