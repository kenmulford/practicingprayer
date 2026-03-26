using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagsViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    _accessibilityService.Announce(value ? "Loading" : "Content loaded");
            }
        }

        public ObservableCollection<TagItemViewModel> Tags { get; } = new();
        public ICommand AddCommand { get; }

        public TagsViewModel(ITagService tagService, INavigationService navigationService,
            IAccessibilityService accessibilityService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            AddCommand = new AsyncRelayCommand(AddAsync);
        }

        public TagsViewModel(ITagService tagService) : this(
            tagService,
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>())
        { }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var tags = await _tagService.GetTagsAsync();
                Tags.Clear();
                foreach (var tag in tags)
                    Tags.Add(new TagItemViewModel(tag, _tagService, this, _navigationService, _accessibilityService));
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
            _tagService.InvalidateCache();
            var tags = await _tagService.GetTagsAsync();
            var freshIds = tags.Select(t => t.Id).ToHashSet();
            var currentIds = Tags.Select(t => t.Id).ToHashSet();

            // Remove deleted tags
            var toRemove = Tags.Where(t => !freshIds.Contains(t.Id)).ToList();
            foreach (var vm in toRemove)
                Tags.Remove(vm);

            // Add new tags
            foreach (var tag in tags.Where(t => !currentIds.Contains(t.Id)))
                Tags.Add(new TagItemViewModel(tag, _tagService, this, _navigationService, _accessibilityService));

            // Update existing tags with fresh data (name/color changes)
            foreach (var tag in tags)
            {
                var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);
                existing?.Update(tag);
            }
        }

        public void RemoveTag(TagItemViewModel item) => Tags.Remove(item);

        private async Task AddAsync() =>
            await _navigationService.GoToAsync(Routes.TagDetailPage);
    }

    public class TagItemViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private readonly TagsViewModel _parent;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private PrayerTag _tag;

        public int Id => _tag.Id;
        public string Name => _tag.Name;
        public bool IsSystem => _tag.IsSystem;
        public Color DotColor => TagColorPalette.Resolve(_tag.Color);
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public TagItemViewModel(PrayerTag tag, ITagService tagService, TagsViewModel parent,
            INavigationService navigationService, IAccessibilityService accessibilityService)
        {
            _tag = tag;
            _tagService = tagService;
            _parent = parent;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
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
            await _navigationService.GoToAsync($"{Routes.TagDetailPage}?load={Id}");

        private async Task DeleteAsync()
        {
            bool confirmed = await _navigationService.DisplayConfirmAsync(
                "Delete Tag",
                $"Delete \"{Name}\"? It will be removed from all prayer requests.",
                "Delete", "Cancel");

            if (!confirmed) return;

            await _tagService.DeleteTagAsync(Id);
            _accessibilityService.Announce("Tag deleted");
            _parent.RemoveTag(this);
        }
    }
}
