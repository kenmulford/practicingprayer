using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Tags
{
    public partial class TagsPage : ContentPage
    {
        private readonly TagsViewModel _vm;
        private bool _loaded;

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
            await App.InitTask; // ensure DB seeding is complete before loading data

            if (!_loaded)
            {
                _loaded = true;
                await _vm.LoadAsync();
            }
            else
            {
                // Subsequent visits — refresh without flicker
                await _vm.RefreshAsync();
            }
        }
    }
}
