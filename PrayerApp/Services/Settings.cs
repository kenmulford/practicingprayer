using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>
        /// The user's preferred display name, collected on first launch.
        /// Empty string means the user skipped or has not been asked yet.
        /// </summary>
        public static string UserName
        {
            get => Preferences.Get(nameof(UserName), string.Empty);
            set => Preferences.Set(nameof(UserName), value);
        }

        /// <summary>True once the user has provided or explicitly skipped the name prompt.</summary>
        public static bool UserNameSet
        {
            get => Preferences.Get(nameof(UserNameSet), false);
            set => Preferences.Set(nameof(UserNameSet), value);
        }

        public static bool AllowNotifications
        {
            get => Preferences.Get(nameof(AllowNotifications), true);
            set {
                Preferences.Set(nameof(AllowNotifications), value);
                if (_notificationService is null)
                    return;

                _ = UpdateAllowNotificationsAsync(value);
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

        public static void ClearSettings()
        {
            // reset "first run" flag
            Preferences.Clear();
        }
    }
}
