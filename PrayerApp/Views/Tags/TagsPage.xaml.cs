using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Views.Tags
{
    public partial class TagsPage : ContentPage
    {
        private readonly TagsViewModel _vm;

        public TagsPage()
        {
            InitializeComponent();
            var tagService = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();
            _vm = new TagsViewModel(tagService);
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await App.InitTask; // ensure DB seeding is complete before loading data
            await _vm.LoadAsync();
        }
    }
}
