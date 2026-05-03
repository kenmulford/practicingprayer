using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using PrayerApp.Shared;

namespace PrayerApp.Services;

/// <summary>
/// Reads pending-import.json from the App Group container, stages the raw
/// text via IImportPayloadService, pushes ConfirmImportPage, deletes the
/// file in finally, appends a breadcrumb. Lives in main app (not
/// PrayerApp.Shared) because it depends on Microsoft.Maui.Controls.Page,
/// and the iOS Share Extension cannot transitively depend on MAUI.
/// </summary>
public sealed class AppGroupImportOrchestrator
{
    private readonly IAppGroupContainerProvider _container;
    private readonly IImportPayloadService _payloadService;
    private readonly INavigationService _navigation;
    private readonly IDispatcher _uiDispatcher;
    private readonly Func<Page> _pageFactory;

    public AppGroupImportOrchestrator(
        IAppGroupContainerProvider container,
        IImportPayloadService payloadService,
        INavigationService navigation,
        IDispatcher uiDispatcher,
        Func<Page> pageFactory)
    {
        _container = container;
        _payloadService = payloadService;
        _navigation = navigation;
        _uiDispatcher = uiDispatcher;
        _pageFactory = pageFactory;
    }

    public async Task CheckPendingAsync()
    {
        var containerPath = _container.ResolveContainerPath();
        if (string.IsNullOrEmpty(containerPath)) return;

        var payloadPath = Path.Combine(containerPath, AppGroupConstants.PayloadFileName);
        if (!File.Exists(payloadPath)) return;

        // Single-writer phase: rotate the breadcrumb log first.
        AppGroupBreadcrumbLog.Truncate(containerPath);

        BreadcrumbOutcome outcome = BreadcrumbOutcome.IoFail;
        int byteCount = -1;
        ImportPayload? payload = null;

        try
        {
            var json = File.ReadAllText(payloadPath);
            byteCount = json.Length;

            try
            {
                payload = JsonSerializer.Deserialize(json, ImportPayloadJsonContext.Default.ImportPayload);
            }
            catch (JsonException)
            {
                outcome = BreadcrumbOutcome.ParseFail;
                return;
            }

            outcome = (payload is null || string.IsNullOrWhiteSpace(payload.Raw))
                ? BreadcrumbOutcome.Empty
                : BreadcrumbOutcome.Ok;
        }
        catch
        {
            outcome = BreadcrumbOutcome.IoFail;
        }
        finally
        {
            try { File.Delete(payloadPath); } catch { /* best effort */ }
            AppGroupBreadcrumbLog.Append(containerPath, outcome, byteCount);
        }

        // Stage + push happen OUTSIDE the finally so a push exception
        // can't double-delete or double-breadcrumb. The file is gone
        // either way; the in-memory slot is the source of truth here.
        if (outcome == BreadcrumbOutcome.Ok && payload is not null)
        {
            _payloadService.StagePayload(payload.Raw);
            var page = _pageFactory();
            var tcs = new TaskCompletionSource();
            _uiDispatcher.Dispatch(async () =>
            {
                try
                {
                    await _navigation.PushModalAsync(page);
                }
                finally
                {
                    tcs.TrySetResult();
                }
            });
            await tcs.Task;
        }
    }
}
