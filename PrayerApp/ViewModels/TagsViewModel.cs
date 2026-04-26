using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Helpers;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagsViewModel : ObservableObject, ISyncableViewModel
    {
        private readonly ITagService _tagService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly IMessenger _messenger;

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

        /// <summary>
        /// Tag list bound to the CollectionView. Mutated in-place across syncs so
        /// item-level state (selection, etc.) survives refresh.
        /// </summary>
        public ObservableCollection<TagItemViewModel> Tags { get; } = new();

        public ICommand AddCommand { get; }

        public TagsViewModel(ITagService tagService, INavigationService navigationService,
            IAccessibilityService accessibilityService, IMessenger messenger)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _messenger = messenger;
            AddCommand = new AsyncRelayCommand(AddAsync);

            _messenger.Register<TagsViewModel, TagChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<TagsViewModel, BulkChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
        }

        public TagsViewModel(ITagService tagService) : this(
            tagService,
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IMessenger>())
        { }

        public async Task SyncAsync()
        {
            IsLoading = true;
            try
            {
                var tags = await _tagService.GetTagsAsync();
                var freshIds = tags.Select(t => t.Id).ToHashSet();

                // Remove deleted tags
                var toRemove = Tags.Where(t => !freshIds.Contains(t.Id)).ToList();
                foreach (var vm in toRemove)
                    Tags.Remove(vm);

                // Add new tags
                var currentIds = Tags.Select(t => t.Id).ToHashSet();
                foreach (var tag in tags.Where(t => !currentIds.Contains(t.Id)))
                    Tags.Add(new TagItemViewModel(tag, _tagService, this, _navigationService, _accessibilityService));

                // Update existing tags with fresh data (name/color changes)
                foreach (var tag in tags)
                {
                    var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);
                    existing?.Update(tag);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void RemoveTag(TagItemViewModel item) => Tags.Remove(item);

        public void DeselectOthers(TagItemViewModel except)
        {
            foreach (var tag in Tags)
                if (tag != except)
                    tag.Deselect();
        }

        public void DeselectAll()
        {
            foreach (var tag in Tags)
                tag.Deselect();
        }

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

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            private set
            {
                if (SetProperty(ref _isSelected, value))
                    OnPropertyChanged(nameof(ShowActions));
            }
        }

        /// <summary>True when this tag is selected and actions should be visible.</summary>
        public bool ShowActions => _isSelected;

        public ICommand SelectCommand { get; }
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
            SelectCommand = new RelayCommand(ToggleSelection);
            EditCommand = new AsyncRelayCommand(EditAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_tag.IsSystem);
        }

        public void Update(PrayerTag tag)
        {
            _tag = tag;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DotColor));
        }

        public void Deselect() => IsSelected = false;

        private void ToggleSelection()
        {
            if (_isSelected)
            {
                IsSelected = false;
            }
            else
            {
                _parent.DeselectOthers(this);
                IsSelected = true;
            }
        }

        private async Task EditAsync()
        {
            _parent.DeselectAll();
            await _navigationService.GoToAsync($"{Routes.TagDetailPage}?load={Id}");
        }

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
