using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using PrayerApp.Views;

namespace PrayerApp
{
    public static class MauiProgram
    {
        // Set to true to reset database and re-seed on next run (DEBUG ONLY)
        private const bool FORCE_RESET_DATABASE = false;

        public static MauiApp CreateMauiApp()
        {
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
            // Register notification service as singleton
            builder.Services.AddSingleton<INotificationService, NotificationService>();

#if ANDROID
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.Android.OrientationService>();
#elif IOS
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.iOS.OrientationService>();
#endif

            var app = builder.Build();

            PrayerApp.Services.Settings.ConfigureNotificationService(
                app.Services.GetRequiredService<INotificationService>());

            // get the new DB Service
            using var scope = app.Services.CreateScope();
            var myDBService = scope.ServiceProvider.GetRequiredService<IDBService>();

            // set DB service for the necessary models
            PrayerCard.SetDBService(myDBService);
            PrayerTag.SetDBService(myDBService);
            PrayerRequestTag.SetDBService(myDBService);
            Prayer.SetDBService(myDBService);
            PrayerInteraction.SetDBService(myDBService);

            // DEBUG: Force reset database and re-seed
            if (FORCE_RESET_DATABASE)
            {
                PrayerApp.Services.Settings.ClearSettings();
            }

            // ensure the schema is updated
            Task.Run(async () => await myDBService.UpdateSchema()).Wait();

            if (PrayerApp.Services.Settings.FirstRun)
            {
                // seed initial data if needed
                Task.Run(async () => await myDBService.SeedDataAsync()).Wait();
                PrayerApp.Services.Settings.FirstRun = false;
            }

            return app;
        }
    }
}
