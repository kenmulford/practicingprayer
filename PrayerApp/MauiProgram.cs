using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using PrayerApp.Views;
using PrayerApp.Views.Tags;

namespace PrayerApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            SQLitePCL.Batteries_V2.Init();

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "prayer_app.db");

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.UseMauiCommunityToolkit();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Add DB to scope as singleton; only need one connection for the life of the app.
            builder.Services.AddSingleton<IDBService>(s => new DBService(dbPath));
            // Register card service as singleton
            builder.Services.AddSingleton<ICardService, CardService>();
            // Register tag service as singleton
            builder.Services.AddSingleton<ITagService, TagService>();
            // Register prayer service as singleton
            builder.Services.AddSingleton<IPrayerService, PrayerService>();
            // Register prayer interaction service as singleton
            builder.Services.AddSingleton<IPrayerInteractionService, PrayerInteractionService>();
            // Register local notification center wrapper (wraps Plugin.LocalNotification static)
            builder.Services.AddSingleton<ILocalNotificationCenter, LocalNotificationCenterWrapper>();
            // Register notification service — Settings.AllowNotifications supplied here so
            // NotificationService.cs itself has no MAUI dependency and can be unit-tested.
            builder.Services.AddSingleton<INotificationService>(sp =>
                new NotificationService(
                    sp.GetRequiredService<ILocalNotificationCenter>(),
                    () => PrayerApp.Services.Settings.AllowNotifications));
            // Register onboarding service as singleton
            builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
            // Register backup service
            builder.Services.AddSingleton<IBackupService, BackupService>();
            // Register user color service
            builder.Services.AddSingleton<IUserColorService, UserColorService>();

#if ANDROID
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.Android.OrientationService>();
            builder.Services.AddSingleton<IColorPickerService, PrayerApp.Platforms.Android.ColorPickerService>();
#elif IOS
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.iOS.OrientationService>();
            builder.Services.AddSingleton<IColorPickerService, PrayerApp.Platforms.iOS.ColorPickerService>();
#endif

            // add transient viewmodel so each instance of PrayerCardPage is new (avoid data bleed/leak)
            builder.Services.AddTransient<PrayerCardViewModel>();
            // tag detail page + viewmodel (transient — each navigation gets a fresh instance)
            builder.Services.AddTransient<TagDetailViewModel>();
            builder.Services.AddTransient<TagDetailPage>();

            var app = builder.Build();

            PrayerApp.Services.Settings.ConfigureNotificationService(
                app.Services.GetRequiredService<INotificationService>());

            // Set DB service for the necessary models (synchronous — just stores a reference).
            var myDBService = app.Services.GetRequiredService<IDBService>();
            PrayerCard.SetDBService(myDBService);
            PrayerTag.SetDBService(myDBService);
            PrayerCardTag.SetDBService(myDBService);
            Prayer.SetDBService(myDBService);
            PrayerInteraction.SetDBService(myDBService);

            // Kick off seeding asynchronously — no blocking on the startup thread.
            // DBService internally awaits its own schema init before any query runs,
            // so seeding will wait for tables to exist automatically.
            // Pages await App.InitTask before loading data.
            App.InitTask = SeedAsync(app.Services);

            return app;
        }

        /// <summary>
        /// Runs post-schema seed work asynchronously. Awaited by App.InitTask
        /// before the first page loads data.
        /// </summary>
        private static async Task SeedAsync(IServiceProvider services)
        {
            var userColorService = services.GetRequiredService<IUserColorService>();
            await userColorService.SeedDefaultsAsync();

#if DEBUG
            if (PrayerApp.Services.Settings.FirstRun)
            {
                var dbService = services.GetRequiredService<IDBService>();
                await dbService.SeedDataAsync();
                PrayerApp.Services.Settings.FirstRun = false;
            }
#endif
        }
    }
}
