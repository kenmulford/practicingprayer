namespace PrayerApp.Views.Backup;

public partial class RestoreProgressPage : ContentPage
{
    public RestoreProgressPage()
    {
        InitializeComponent();
    }

    // Prevent dismissal via hardware back button or back gesture
    protected override bool OnBackButtonPressed() => true;
}
