using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Tags;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagsViewModel : ObservableObject
    {
        private readonly ITagService _tagService;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<TagItemViewModel> Tags { get; } = new();
        public ICommand AddCommand { get; }

        public TagsViewModel(ITagService tagService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            AddCommand = new AsyncRelayCommand(AddAsync);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var tags = await _tagService.GetTagsAsync();
                Tags.Clear();
                foreach (var tag in tags)
                    Tags.Add(new TagItemViewModel(tag, _tagService, this));
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Lightweight refresh for cross-tab consistency. Detects new/deleted tags
        /// without clearing the entire collection (avoids flicker).
        /// </summary>
        public async Task RefreshAsync()
        {
            var tags = await _tagService.GetTagsAsync();
            var freshIds = tags.Select(t => t.Id).ToHashSet();
            var currentIds = Tags.Select(t => t.Id).ToHashSet();

            // Remove deleted tags
            var toRemove = Tags.Where(t => !freshIds.Contains(t.Id)).ToList();
            foreach (var vm in toRemove)
                Tags.Remove(vm);

            // Add new tags
            foreach (var tag in tags.Where(t => !currentIds.Contains(t.Id)))
                Tags.Add(new TagItemViewModel(tag, _tagService, this));

            // Update existing tags with fresh data (name/color changes)
            foreach (var tag in tags)
            {
                var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);
                existing?.Update(tag);
            }
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
        public bool IsSystem => _tag.IsSystem;
        public Color DotColor => TagColorPalette.Resolve(_tag.Color);
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public TagItemViewModel(PrayerTag tag, ITagService tagService, TagsViewModel parent)
        {
            _tag = tag;
            _tagService = tagService;
            _parent = parent;
            EditCommand = new AsyncRelayCommand(EditAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_tag.IsSystem);
        }

        public void Update(PrayerTag tag)
        {
            _tag = tag;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DotColor));
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
            SemanticScreenReader.Announce("Tag deleted");
            _parent.RemoveTag(this);
        }
    }
}
