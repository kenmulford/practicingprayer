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
/// Applies only to pages that implement <see cref="Views.IPageSheetModal"/>.
/// Pages like RestoreProgressPage (blocking progress) stay full-screen.
/// </summary>
public static class ModalPageSheetHandler
{
    public static void Configure()
    {
        PageHandler.Mapper.AppendToMapping("iPadPageSheet", (handler, view) =>
        {
            if (view is not PrayerApp.Views.IPageSheetModal || view is not Page page)
                return;

            var vc = page.FindViewController();
            if (vc != null)
                vc.ModalPresentationStyle = UIModalPresentationStyle.PageSheet;
        });
    }
}
