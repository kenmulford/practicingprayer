using Microsoft.Maui.Handlers;
using PrayerApp.Platforms.iOS.Helpers;
using UIKit;

namespace PrayerApp.Platforms.iOS.Handlers;

/// <summary>
/// Configures modal pages to use PageSheet presentation on iPad instead of
/// full-screen. PageSheet slides up as a card covering ~85% of the screen,
/// which feels more natural for lightweight modals on iPad's large display.
/// On iPhone, PageSheet and FullScreen are visually identical.
///
/// Applies to pages that implement <see cref="Views.IPageSheetModal"/>, OR
/// to a <see cref="NavigationPage"/> whose root child implements that
/// marker — ConfirmImportPage is wrapped in a NavigationPage so its
/// ToolbarItems render in the modal nav bar; the wrapper is the actual
/// presented controller, so the PageSheet style must apply to it.
/// Pages like RestoreProgressPage (blocking progress) stay full-screen.
/// </summary>
public static class ModalPageSheetHandler
{
    public static void Configure()
    {
        PageHandler.Mapper.AppendToMapping("iPadPageSheet", (handler, view) =>
        {
            if (view is not Page page) return;
            if (!IsPageSheetTarget(page)) return;

            var vc = page.FindViewController();
            if (vc != null)
                vc.ModalPresentationStyle = UIModalPresentationStyle.PageSheet;
        });
    }

    private static bool IsPageSheetTarget(Page page)
    {
        if (page is PrayerApp.Views.IPageSheetModal) return true;
        // NavigationPage wrapper: keep PageSheet style on iPad when the
        // wrapped root page opted into the marker.
        if (page is NavigationPage nav && nav.RootPage is PrayerApp.Views.IPageSheetModal)
            return true;
        return false;
    }
}
