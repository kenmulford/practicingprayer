#if ANDROID
using Android.Content.Res;
using Android.Graphics;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
#endif
using PrayerApp.Views.Prayer;
using PrayerApp.Views.PrayerCard;
using PrayerApp.Views.PrayerTime;

namespace PrayerApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

#if ANDROID
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", static (handler, view) =>
            {
                if (handler.PlatformView is AppCompatEditText editText)
                {
                    ViewCompat.SetBackgroundTintList(editText, ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
                }
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", static (handler, view) =>
            {
                if (handler.PlatformView is AppCompatEditText editText)
                {
                    ViewCompat.SetBackgroundTintList(editText, ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
                }
            });
#endif

            // register explicit routes for child pages
            Routing.RegisterRoute(nameof(PrayerCardPage), typeof(PrayerCardPage));
            Routing.RegisterRoute(nameof(PrayerDetailPage), typeof(PrayerDetailPage));
            Routing.RegisterRoute(nameof(PrayerTimePage), typeof(PrayerTimePage));
        }
    }
}
