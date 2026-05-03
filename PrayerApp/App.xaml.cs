#if IOS
using Microsoft.Extensions.DependencyInjection;
#endif

namespace PrayerApp
{
    public partial class App : Application
    {
        /// <summary>
        /// Async initialization task (schema seeding) started in MauiProgram.
        /// Pages await this before loading data to ensure the DB is ready.
        /// </summary>
        public static Task InitTask { get; set; } = Task.CompletedTask;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
#if IOS
            window.Activated += OnWindowActivated;
#endif
            return window;
        }

#if IOS
        private static void OnWindowActivated(object? sender, EventArgs e)
        {
            // Fire-and-forget. Activated fires on cold launch (after Shell is
            // realized) AND on every warm resume. The orchestrator awaits
            // App.InitTask internally so DB seed completes before any DI
            // resolution that touches IDBService.
            _ = Task.Run(async () =>
            {
                try
                {
                    await InitTask;
                    var dispatcher = IPlatformApplication.Current?.Services
                        .GetService<PrayerApp.Platforms.iOS.AppGroupImportDispatcher>();
                    if (dispatcher is null) return;
                    await dispatcher.CheckPendingAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppGroupImport] Activated handler failed: {ex}");
                }
            });
        }
#endif
    }
}
