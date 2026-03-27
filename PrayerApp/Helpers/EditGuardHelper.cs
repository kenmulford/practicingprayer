using PrayerApp.ViewModels;

namespace PrayerApp.Helpers;

/// <summary>
/// Wires up a BackButtonBehavior that checks IEditGuard before allowing back navigation.
/// iOS's native back button does NOT fire Shell.Navigating, so without this the unsaved
/// changes guard is bypassed and edits are silently lost.
/// </summary>
public static class EditGuardHelper
{
    public static void AttachEditGuardBackButton(ContentPage page)
    {
        Shell.SetBackButtonBehavior(page, new BackButtonBehavior
        {
            Command = new Command(async () =>
            {
                if (page.BindingContext is IEditGuard guard && guard.IsDirty)
                {
                    if (!await guard.CanLeaveAsync())
                        return;
                }
                await Shell.Current.GoToAsync("..");
            })
        });
    }
}
