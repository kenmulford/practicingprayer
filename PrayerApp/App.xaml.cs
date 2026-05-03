#if IOS
using Microsoft.Extensions.DependencyInjection;
using PrayerApp.Helpers;
using PrayerApp.Services;
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
            // Static handler — no captured state, so no need to unsubscribe
            // when the Window is destroyed (the GC clears the delegate when
            // the Window itself is collected).
            window.Activated += OnWindowActivated;
#endif
            return window;
        }

#if IOS
        private static void OnWindowActivated(object? sender, EventArgs e)
        {
            // Activated fires on cold launch (after Shell is realized) AND on
            // every warm resume. SafeFireAndForget routes any exception to
            // IDiagnosticLog so failures show up in Settings → About → Send
            // Diagnostic Info instead of vanishing into Debug.WriteLine.
            CheckPendingAsync().SafeFireAndForget();
        }

        private static async Task CheckPendingAsync()
        {
            await InitTask;
            var orchestrator = IPlatformApplication.Current?.Services
                .GetService<AppGroupImportOrchestrator>();
            if (orchestrator is null) return;
            await orchestrator.CheckPendingAsync();
        }
#endif
    }
}
