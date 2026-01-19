using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using PrayerApp;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views;


namespace PrayerApp
{
    public static class MauiProgram
    {
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

            var app = builder.Build();

            // get the new DB Service
            using var scope = app.Services.CreateScope();
            var myDBService = scope.ServiceProvider.GetRequiredService<IDBService>();

            // set DB service for the necessary models
            PrayerCategory.SetDBService(myDBService);
            Prayer.SetDBService(myDBService);

            PrayerApp.Services.Settings.ClearSettings();

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
