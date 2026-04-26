using PrayerApp.Helpers;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Tags
{
    public partial class TagsPage : ContentPage
    {
        private readonly TagsViewModel _vm;

        public TagsPage(TagsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;
        }

        private void OnBackgroundTapped(object? sender, TappedEventArgs e)
            => _vm.DeselectAll();

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await PageSync.OnAppearingAsync(_vm);
        }
    }
}
