#if ANDROID
using Android.Content.Res;
using Android.Graphics;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
#endif
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Onboarding;
using PrayerApp.Views.Prayer;
using PrayerApp.Views.PrayerCard;
using PrayerApp.Views.PrayerTime;
using PrayerApp.Views.Tags;

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
            Routing.RegisterRoute(nameof(TagDetailPage), typeof(TagDetailPage));

            // Subtle crossfade when switching tabs
            this.Navigated += OnShellNavigated;

            // Subscribe to onboarding step changes to show the closing popup
            var onboardingService = IPlatformApplication.Current!.Services
                .GetRequiredService<IOnboardingService>();

            onboardingService.StepChanged += (_, _) =>
            {
                if (onboardingService.CurrentStep != OnboardingStep.Complete) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ShowOnboardingCompletePopupAsync().SafeFireAndForget();
                });
            };
        }

        private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            // Only animate Push (new pages). Pop and tab-switch pages already
            // have visible content; hiding them to fade back in causes a flash
            // when OnAppearing triggers a data refresh.
            if (e.Source != ShellNavigationSource.Push) return;

            var page = CurrentPage;
            if (page is null) return;

            page.Opacity = 0;
            page.TranslationX = 60;
            await Task.WhenAll(
                page.FadeToAsync(1, 250, Easing.CubicOut),
                page.TranslateToAsync(0, 0, 250, Easing.CubicOut));
        }

        private static async Task ShowOnboardingCompletePopupAsync()
        {
            var page = Shell.Current?.CurrentPage;
            if (page is not null)
                await page.ShowPopupAsync(new OnboardingCompletePopup(),
                    new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false },
                    CancellationToken.None);
        }
    }
}
