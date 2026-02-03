using PrayerApp.Views.Prayer;
using PrayerApp.Views.PrayerCard;

namespace PrayerApp

{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // register explicit routes for child pages
            Routing.RegisterRoute(nameof(PrayerCardPage), typeof(PrayerCardPage));
            Routing.RegisterRoute(nameof(PrayerDetailPage), typeof(PrayerDetailPage));
        }
    }
}
