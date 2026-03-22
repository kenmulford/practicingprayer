using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PrayerApp.Helpers;
using Plugin.LocalNotification;

namespace PrayerApp.Services
{
    public static class Settings
    {
        private static INotificationService? _notificationService;

        public static void ConfigureNotificationService(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public static bool FirstRun
        {
            get => Preferences.Get(nameof(FirstRun), true);
            set => Preferences.Set(nameof(FirstRun), value);
        }

        /// <summary>Seconds per card in auto-mode. Persisted across sessions.</summary>
        public static int AutoModeIntervalSeconds
        {
            get => Preferences.Get(nameof(AutoModeIntervalSeconds), 30);
            set => Preferences.Set(nameof(AutoModeIntervalSeconds), value);
        }

        public static bool OnboardingComplete
        {
            get => Preferences.Get(nameof(OnboardingComplete), false);
            set => Preferences.Set(nameof(OnboardingComplete), value);
        }

        public static bool AllowNotifications
        {
            get => Preferences.Get(nameof(AllowNotifications), true);
            set {
                Preferences.Set(nameof(AllowNotifications), value);
                if (_notificationService is null)
                    return;

                UpdateAllowNotificationsAsync(value).SafeFireAndForget();
            }
        }

        private static async Task UpdateAllowNotificationsAsync(bool allowNotifications)
        {
            if (_notificationService is null)
                return;

            if (allowNotifications)
            {
                var granted = await _notificationService.RequestPermissionAsync();
                if (!granted)
                {
                    Preferences.Set(nameof(AllowNotifications), false);
                }
            }
            else
            {
                await _notificationService.ClearAllAsync();
            }
        }

        /// <summary>
        /// Requests the OS notification permission if global notifications are enabled.
        /// Call when a user enables per-item notifications anywhere in the app (e.g. during
        /// onboarding or prayer creation) so the OS dialog isn't deferred until Settings is visited.
        /// </summary>
        public static void EnsureNotificationPermissionRequested()
        {
            if (AllowNotifications && _notificationService != null)
                _notificationService.RequestPermissionAsync().SafeFireAndForget();
        }

        public static void ClearSettings()
        {
            // reset "first run" flag
            Preferences.Clear();
        }
    }
}
