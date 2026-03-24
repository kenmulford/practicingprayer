using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using PrayerApp.Models;
using Plugin.LocalNotification;
using PrayerApp.Services;
using PrayerApp.Helpers;
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

            builder.UseLocalNotification(config =>
            {
#if ANDROID
                config.AddAndroid(android =>
                {
                    android.AddChannel(new Plugin.LocalNotification.AndroidOption.NotificationChannelRequest
                    {
                        Id = "prayer_reminders",
                        Name = "Prayer Reminders",
                        Description = "Scheduled prayer reminder notifications",
                        Importance = Plugin.LocalNotification.AndroidOption.AndroidImportance.High,
                        EnableSound = true,
                        EnableVibration = true
                    });
                });
#endif
            });

#if ANDROID
            PrayerApp.Platforms.Android.Handlers.TextInputTimePickerHandler.Configure();
#endif

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
            // Register diagnostic log service
            builder.Services.AddSingleton<IDiagnosticLog>(s => new DiagnosticLog(FileSystem.AppDataDirectory));
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

            RegisterGlobalExceptionHandlers();

            var app = builder.Build();

            PrayerApp.Services.Settings.ConfigureNotificationService(
                app.Services.GetRequiredService<INotificationService>());

            // Wire notification tap → confirmation → Prayer Time navigation
            var notificationCenter = app.Services.GetRequiredService<ILocalNotificationCenter>();
            var tagServiceForNotification = app.Services.GetRequiredService<ITagService>();
            notificationCenter.NotificationTapped += async (_, notificationId) =>
            {
                await App.InitTask; // Ensure DB is ready

                var systemTag = await tagServiceForNotification.GetSystemTagAsync(TagService.RecentlyNotifiedTagName);
                if (systemTag is null) return;

                // Must run on UI thread for dialog and navigation
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var confirmed = await Shell.Current.DisplayAlertAsync(
                        "Prayer Time",
                        "Would you like to pray for your recently notified prayer requests now?",
                        "Yes", "No");

                    if (confirmed)
                    {
                        await Shell.Current.GoToAsync(
                            $"{nameof(PrayerApp.Views.PrayerTime.PrayerTimePage)}?scope=tags&tagIds={systemTag.Id}");
                    }
                });
            };

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
        /// Registers global exception handlers that log to <see cref="IDiagnosticLog"/>
        /// with a console fallback (DI may not be built yet during startup).
        /// Called once from CreateMauiApp — shared by both iOS and Android.
        /// </summary>
        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var log = IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
                if (log != null && ex != null)
                    log.Log("UnhandledException", ex);
                else
                    Console.Error.WriteLine(ex?.ToString() ?? e.ExceptionObject?.ToString());
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                var log = IPlatformApplication.Current?.Services?.GetService<IDiagnosticLog>();
                if (log != null)
                    log.Log("UnobservedTaskException", e.Exception);
                else
                    Console.Error.WriteLine(e.Exception.ToString());
                e.SetObserved();
            };
        }

        /// <summary>
        /// Runs post-schema seed work asynchronously. Awaited by App.InitTask
        /// before the first page loads data.
        /// </summary>
        private static async Task SeedAsync(IServiceProvider services)
        {
            // Trim the diagnostic log on startup (non-blocking — runs inside InitTask)
            services.GetRequiredService<IDiagnosticLog>().Trim();

            var userColorService = services.GetRequiredService<IUserColorService>();
            await userColorService.SeedDefaultsAsync();

            var tagService = services.GetRequiredService<ITagService>();
            await tagService.SeedSystemTagsAsync();

            // Ensure the system "Quick Add" card exists
            var cardService = services.GetRequiredService<ICardService>();
            await cardService.GetOrCreateQuickAddCardAsync();

            // Tag prayers that were recently notified (within last 24h based on schedule)
            try
            {
                var prayerService = services.GetRequiredService<IPrayerService>();
                var activePrayers = await prayerService.GetAllActivePrayersAsync();
                var recentIds = NotificationHelper.GetRecentlyNotifiedPrayerIds(activePrayers, DateTime.Now);

                var systemTag = await tagService.GetSystemTagAsync(TagService.RecentlyNotifiedTagName);
                if (systemTag is not null)
                {
                    await tagService.ClearAllAssignmentsForTagAsync(systemTag.Id);
                    foreach (var prayerId in recentIds)
                        await tagService.AddTagToRequestAsync(prayerId, systemTag.Id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recently-notified tagging failed: {ex}");
            }

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
