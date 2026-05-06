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

            // Cross-platform: Activated fires after Shell is realized on
            // cold launch (and on every warm resume). First fire flips
            // the ShellNavigationService cold-start gate so deep-link /
            // file imports staged from intent / open-url callbacks can
            // safely PushModalAsync.
            window.Activated += SignalShellReadyOnce;

#if IOS
            // Static handler — no captured state, so no need to unsubscribe
            // when the Window is destroyed (the GC clears the delegate when
            // the Window itself is collected).
            window.Activated += OnWindowActivated;
#endif
            return window;
        }

        private static void SignalShellReadyOnce(object? sender, EventArgs e)
        {
            // Activated fires on every warm resume; the gate only needs to
            // flip once. Unsubscribe so subsequent resumes skip the DI lookup.
            if (sender is Window w) w.Activated -= SignalShellReadyOnce;

            var nav = IPlatformApplication.Current?.Services
                .GetService<PrayerApp.Services.INavigationService>();
            if (nav is PrayerApp.Services.ShellNavigationService shellNav)
                shellNav.SignalShellReady();
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
