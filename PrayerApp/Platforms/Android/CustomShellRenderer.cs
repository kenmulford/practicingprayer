using Android.Content;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace PrayerApp.Platforms.Android;

/// <summary>
/// Custom Shell renderer that makes tapping the already-active tab in the bottom
/// tab bar pop the tab's navigation stack back to its root. MAUI Shell's default
/// no-ops same-tab taps (dotnet/maui#15301); this overrides
/// <see cref="ShellItemRenderer.OnTabReselected"/> — invoked by Android's
/// <c>NavigationBarView.IOnItemSelectedListener</c> when the currently-selected
/// item is tapped again — to call <c>PopToRootAsync</c> on that tab's section.
///
/// Android-only. iOS Shell uses a different renderer stack (UITabBarController);
/// the equivalent there is out of scope for this change.
/// </summary>
public class CustomShellRenderer : ShellRenderer
{
    public CustomShellRenderer(Context context) : base(context) { }

    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem)
        => new PopToRootShellItemRenderer(this);

    private sealed class PopToRootShellItemRenderer : ShellItemRenderer
    {
        public PopToRootShellItemRenderer(IShellContext shellContext) : base(shellContext) { }

        protected override void OnTabReselected(ShellSection shellSection)
        {
            base.OnTabReselected(shellSection);
            if (shellSection is null) return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await shellSection.Navigation.PopToRootAsync();
                }
                catch
                {
                    // Best-effort — navigation may be canceled (e.g. IEditGuard).
                }
            });
        }
    }
}
