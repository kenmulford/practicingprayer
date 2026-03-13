using PrayerApp.ViewModels;

namespace PrayerApp.Views.Tags
{
    public partial class TagDetailPage : ContentPage
    {
        public TagDetailPage(TagDetailViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
