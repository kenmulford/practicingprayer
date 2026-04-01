using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using PrayerApp.Models;
using Plugin.LocalNotification;
using PrayerApp.Services;
using PrayerApp.Helpers;
using PrayerApp.ViewModels;
using PrayerApp.Views;
using PrayerApp.Views.Prayer;
using PrayerApp.Views.PrayerCard;
using PrayerApp.Views.PrayerTime;
using PrayerApp.Views.Settings;
using PrayerApp.Views.Tags;
using System.Globalization;

namespace PrayerApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Force English locale for all formatting (dates, AM/PM, numbers)
            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

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
                    android.AddChannel(new Plugin.LocalNotification.Core.Models.AndroidOption.AndroidNotificationChannelRequest
                    {
                        Id = LocalNotificationCenterWrapper.PrayerRemindersChannelId,
                        Name = "Prayer Reminders",
                        Description = "Scheduled prayer reminder notifications",
                        Importance = Plugin.LocalNotification.Core.Models.AndroidOption.AndroidImportance.High,
                        EnableSound = true,
                        EnableVibration = true
                    });
                });
#endif
            });

            // F-10: Deep link handling via platform lifecycle events
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android =>
                {
                    // Cold launch — Intent arrives with the activity
                    android.OnCreate((activity, _) =>
                        HandleAndroidIntent(activity.Intent));

                    // Warm launch — app already running, new link tapped
                    android.OnNewIntent((activity, intent) =>
                        HandleAndroidIntent(intent));
                });
#elif IOS
                events.AddiOS(ios =>
                {
                    // Warm launch via Universal Link (app already running)
                    ios.ContinueUserActivity((app, activity, handler) =>
                    {
                        if (activity.ActivityType == Foundation.NSUserActivityType.BrowsingWeb)
                            HandleDeepLink(activity.WebPageUrl?.ToString());
                        return true;
                    });

                    // Scene-based launch (iPadOS multi-window, cold + warm)
                    ios.SceneWillConnect((scene, session, options) =>
                    {
                        var activity = options.UserActivities?
                            .ToArray<Foundation.NSUserActivity>()
                            .FirstOrDefault(a =>
                                a.ActivityType == Foundation.NSUserActivityType.BrowsingWeb);
                        HandleDeepLink(activity?.WebPageUrl?.ToString());
                    });
                });
#endif
            });

#if ANDROID
            PrayerApp.Platforms.Android.Handlers.TextInputTimePickerHandler.Configure();
#elif IOS
            PrayerApp.Platforms.iOS.Handlers.EnglishLocaleTimePickerHandler.Configure();
#endif

            // Fix Switch thumb color not syncing on initial load when IsToggled is already true.
            // The VisualStateManager "On" state doesn't fire until user interaction.
            Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("SyncInitialThumbColor", (handler, view) =>
            {
                if (view is Switch sw && sw.IsToggled)
                {
                    // Force the On VisualState so thumb color matches the style
                    VisualStateManager.GoToState(sw, "On");
                }
            });

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
            // Register box service as singleton
            builder.Services.AddSingleton<IBoxService, BoxService>();
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
            // Register settings wrapper (delegates to static Settings, enables VM testing)
            builder.Services.AddSingleton<ISettings, SettingsService>();
            // Navigation + accessibility abstractions (enable VM unit testing)
            builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
            builder.Services.AddSingleton<IAccessibilityService, MauiAccessibilityService>();
            // Deep link sharing service
            builder.Services.AddSingleton<IDeepLinkService, DeepLinkService>();

#if ANDROID
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.Android.OrientationService>();
            builder.Services.AddSingleton<IColorPickerService, PrayerApp.Platforms.Android.ColorPickerService>();
#elif IOS
            builder.Services.AddSingleton<IOrientationService, PrayerApp.Platforms.iOS.OrientationService>();
            builder.Services.AddSingleton<IColorPickerService, PrayerApp.Platforms.iOS.ColorPickerService>();
#endif

            // ViewModels — Transient (fresh per page navigation)
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<PrayerCardsViewModel>();
            builder.Services.AddTransient<PrayerCardViewModel>();
            builder.Services.AddTransient<PrayerListViewModel>();
            builder.Services.AddTransient<PrayerRequestDetailViewModel>();
            builder.Services.AddTransient<QuickAddViewModel>();
            builder.Services.AddTransient<PrayerTimeViewModel>();
            builder.Services.AddTransient<PrayerTimeScopeViewModel>();
            builder.Services.AddTransient<TagsViewModel>();
            builder.Services.AddTransient<TagDetailViewModel>();
            builder.Services.AddTransient<BoxesViewModel>();
            builder.Services.AddTransient<BoxDetailViewModel>();

            // Pages — Transient (Shell resolves from DI on navigation)
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<PrayerCardsPage>();
            builder.Services.AddTransient<PrayerCardPage>();
            builder.Services.AddTransient<PrayerListPage>();
            builder.Services.AddTransient<PrayerDetailPage>();
            builder.Services.AddTransient<QuickAddPage>();
            builder.Services.AddTransient<PrayerTimePage>();
            builder.Services.AddTransient<PrayerTimeScopePage>();
            builder.Services.AddTransient<TagsPage>();
            builder.Services.AddTransient<TagDetailPage>();
            builder.Services.AddTransient<Views.Boxes.BoxesPage>();
            builder.Services.AddTransient<Views.Boxes.BoxDetailPage>();
            builder.Services.AddTransient<SettingsHubPage>();
            builder.Services.AddTransient<AppSettingsPage>();
            builder.Services.AddTransient<BackupPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<HelpPage>();
            builder.Services.AddTransient<HelpViewModel>();

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
            CardBox.SetDBService(myDBService);

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

#if ANDROID
        private static void HandleAndroidIntent(Android.Content.Intent? intent)
        {
            if (intent?.Action != Android.Content.Intent.ActionView || intent.Data is null)
                return;
            HandleDeepLink(intent.Data.ToString());
        }
#endif

        /// <summary>
        /// Processes an incoming Universal Link / App Link URI.
        /// Waits for DB initialization, then delegates to DeepLinkService.
        /// </summary>
        private static void HandleDeepLink(string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://practicingprayerapp.com/share"))
                return;

            // Suppress onboarding for this session — must happen before UI dispatch
            // so MainPage.OnAppearing sees the flag when it checks.
            var onboarding = IPlatformApplication.Current?.Services?.GetService<IOnboardingService>();
            onboarding?.MarkDeepLinkSession();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await App.InitTask;
                    var svc = IPlatformApplication.Current!.Services.GetRequiredService<IDeepLinkService>();
                    await svc.HandleAsync(url);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeepLink] HandleDeepLink failed: {ex.Message}");
                }
            });
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

            // Ensure system boxes (System, Archived) exist — resilience fallback
            var boxService = services.GetRequiredService<IBoxService>();
            await boxService.SeedSystemBoxesAsync();

            // Ensure ArchivedFolderId setting is in sync (covers edge cases where
            // DBService migration wrote it but the box was re-created by seed)
            var archivedBox = await boxService.GetSystemBoxAsync(CardBox.SystemKeyArchived);
            if (archivedBox != null)
                Settings.ArchivedFolderId = archivedBox.Id;

            // Ensure the system "Quick Add" card exists
            var cardService = services.GetRequiredService<ICardService>();
            await cardService.GetOrCreateQuickAddCardAsync();

            // Load active prayers once — reused for recently-notified tagging and M-11 renewal
            var prayerService = services.GetRequiredService<IPrayerService>();
            var activePrayers = await prayerService.GetAllActivePrayersAsync();

            // Tag prayers that were recently notified (within last 24h based on schedule)
            try
            {
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

            // Reconcile notifications: clear orphans from deleted prayers or prior
            // app versions, then reschedule all prayers with CanNotify=true.
            // On Android this also renews the 12 monthly one-shot notifications (M-11).
            // On iOS this clears native UNCalendarNotificationTrigger orphans.
            try
            {
                var notificationService = services.GetRequiredService<INotificationService>();
                await notificationService.ReconcileNotificationsAsync(activePrayers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification reconciliation failed: {ex}");
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
