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
            return new Window(new AppShell());
        }
    }
}