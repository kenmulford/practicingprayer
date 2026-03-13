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

            // add transient viewmodel so each instance of PrayerCardDetail is new (avoid data bleed/leak)
            builder.Services.AddTransient<PrayerCardDetailViewModel>();
            // tag detail page + viewmodel (transient — each navigation gets a fresh instance)
            builder.Services.AddTransient<TagDetailViewModel>();
            builder.Services.AddTransient<TagDetailPage>();

            var app = builder.Build();

            PrayerApp.Services.Settings.ConfigureNotificationService(
                app.Services.GetRequiredService<INotificationService>());

            // get the new DB Service
            using var scope = app.Services.CreateScope();
            var myDBService = scope.ServiceProvider.GetRequiredService<IDBService>();

            // set DB service for the necessary models
            PrayerCard.SetDBService(myDBService);
            PrayerTag.SetDBService(myDBService);
            PrayerCardTag.SetDBService(myDBService);
            Prayer.SetDBService(myDBService);
            PrayerInteraction.SetDBService(myDBService);

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
