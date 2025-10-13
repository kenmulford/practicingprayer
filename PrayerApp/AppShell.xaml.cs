using PrayerApp.Views.PrayerCategory;

namespace PrayerApp

{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // register explicit routes for child pages
            Routing.RegisterRoute(nameof(PrayerCategoryPage), typeof(PrayerCategoryPage));
        }
    }
}
