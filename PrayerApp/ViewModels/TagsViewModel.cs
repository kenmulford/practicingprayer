using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Helpers;
using PrayerApp.Views.Tags;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagsViewModel : ObservableObject
    {
        private readonly ITagService _tagService;

        public ObservableCollection<TagItemViewModel> Tags { get; } = new();
        public ICommand AddCommand { get; }

        public TagsViewModel(ITagService tagService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            AddCommand = new AsyncRelayCommand(AddAsync);
        }

        public async Task LoadAsync()
        {
            var tags = await _tagService.GetTagsAsync();
            Tags.Clear();
            foreach (var tag in tags)
                Tags.Add(new TagItemViewModel(tag, _tagService, this));
        }

        public void RemoveTag(TagItemViewModel item) => Tags.Remove(item);

        private async Task AddAsync() =>
            await Shell.Current.GoToAsync(nameof(TagDetailPage));
    }

    public class TagItemViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private readonly TagsViewModel _parent;
        private PrayerTag _tag;

        public int Id => _tag.Id;
        public string Name => _tag.Name;
        public Color DotColor => TagColorPalette.Resolve(_tag.Color);
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public TagItemViewModel(PrayerTag tag, ITagService tagService, TagsViewModel parent)
        {
            _tag = tag;
            _tagService = tagService;
            _parent = parent;
            EditCommand = new AsyncRelayCommand(EditAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        }

        private async Task EditAsync() =>
            await Shell.Current.GoToAsync($"{nameof(TagDetailPage)}?load={Id}");

        private async Task DeleteAsync()
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Delete Tag",
                $"Delete \"{Name}\"? It will be removed from all prayer requests.",
                "Delete", "Cancel");

            if (!confirmed) return;

            await _tagService.DeleteTagAsync(Id);
            _parent.RemoveTag(this);
        }
    }
}
