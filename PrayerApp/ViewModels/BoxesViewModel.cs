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
    public class BoxesViewModel : ObservableObject, ISyncableViewModel
    {
        private readonly IBoxService _boxService;
        private readonly ICardService _cardService;
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

        /// <summary>Mutated in-place across syncs so item-level state survives refresh.</summary>
        public ObservableCollection<BoxItemViewModel> Boxes { get; } = new();

        public ICommand AddCommand { get; }

        public BoxesViewModel(IBoxService boxService, ICardService cardService,
            INavigationService navigationService, IAccessibilityService accessibilityService,
            IMessenger messenger)
        {
            _boxService = boxService ?? throw new ArgumentNullException(nameof(boxService));
            _cardService = cardService ?? throw new ArgumentNullException(nameof(cardService));
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _messenger = messenger;
            AddCommand = new AsyncRelayCommand(AddAsync);

            // Box card-counts derive from cards-per-box, so card mutations can change
            // displayed counts even if no box itself changed.
            _messenger.Register<BoxesViewModel, CardBoxChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<BoxesViewModel, PrayerCardChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<BoxesViewModel, BulkChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
        }

        public BoxesViewModel(IBoxService boxService) : this(
            boxService,
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IMessenger>())
        { }

        private async Task<(IReadOnlyList<CardBox> boxes, Dictionary<int, int> counts)> FetchBoxDataAsync()
        {
            var boxes = await _boxService.GetBoxesAsync();
            var cards = await _cardService.GetCardsAsync();
            var counts = cards.GroupBy(c => c.BoxId)
                .ToDictionary(g => g.Key, g => g.Count());
            return (boxes, counts);
        }

        public async Task SyncAsync()
        {
            IsLoading = true;
            try
            {
                var (boxes, counts) = await FetchBoxDataAsync();
                var freshIds = boxes.Select(b => b.Id).ToHashSet();

                // Remove deleted
                var toRemove = Boxes.Where(b => !freshIds.Contains(b.Id)).ToList();
                foreach (var vm in toRemove)
                    Boxes.Remove(vm);

                // Add new
                var currentIds = Boxes.Select(b => b.Id).ToHashSet();
                foreach (var box in boxes.Where(b => !currentIds.Contains(b.Id)))
                    Boxes.Add(new BoxItemViewModel(
                        box, _boxService, this, _navigationService, _accessibilityService,
                        counts.GetValueOrDefault(box.Id, 0)));

                // Update existing
                foreach (var box in boxes)
                {
                    var existing = Boxes.FirstOrDefault(b => b.Id == box.Id);
                    existing?.Update(box, counts.GetValueOrDefault(box.Id, 0));
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void RemoveBox(BoxItemViewModel item) => Boxes.Remove(item);

        public void DeselectOthers(BoxItemViewModel except)
        {
            foreach (var box in Boxes)
                if (box != except)
                    box.Deselect();
        }

        public void DeselectAll()
        {
            foreach (var box in Boxes)
                box.Deselect();
        }

        private async Task AddAsync() =>
            await _navigationService.GoToAsync(Routes.BoxDetailPage);
    }

    public class BoxItemViewModel : ObservableObject
    {
        internal const string ActionUnassign = "Unassign Cards";
        internal const string ActionDeleteAll = "Delete All Cards & Requests";

        private readonly IBoxService _boxService;
        private readonly BoxesViewModel _parent;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private CardBox _box;

        public int Id => _box.Id;
        public string Name => _box.Name;
        public bool IsSystem => _box.IsSystem;

        private int _cardCount;
        public int CardCount
        {
            get => _cardCount;
            private set
            {
                if (SetProperty(ref _cardCount, value))
                    OnPropertyChanged(nameof(AccessibleDescription));
            }
        }

        public string AccessibleDescription
        {
            get
            {
                var parts = new List<string> { Name };
                parts.Add($"{CardCount} card{(CardCount == 1 ? "" : "s")}");
                if (IsSystem) parts.Add("System");
                return string.Join(", ", parts);
            }
        }

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

        /// <summary>Show actions when selected. System boxes only show Edit (no Delete).</summary>
        public bool ShowActions => _isSelected;

        public ICommand SelectCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public BoxItemViewModel(CardBox box, IBoxService boxService, BoxesViewModel parent,
            INavigationService navigationService, IAccessibilityService accessibilityService,
            int cardCount)
        {
            _box = box;
            _boxService = boxService;
            _parent = parent;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _cardCount = cardCount;
            SelectCommand = new RelayCommand(ToggleSelection);
            EditCommand = new AsyncRelayCommand(EditAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !_box.IsSystem);
        }

        public void Update(CardBox box, int cardCount)
        {
            _box = box;
            CardCount = cardCount;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsSystem));
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
            await _navigationService.GoToAsync($"{Routes.BoxDetailPage}?load={Id}");
        }

        private async Task DeleteAsync()
        {
            var result = await _navigationService.DisplayActionSheetAsync(
                $"Delete \"{Name}\"?",
                "Cancel",
                null,
                ActionUnassign, ActionDeleteAll);

            if (result is null or "Cancel") return;

            var deleteCards = result == ActionDeleteAll;
            await _boxService.DeleteBoxAsync(Id, deleteCards);
            _accessibilityService.Announce(deleteCards
                ? $"{BoxStrings.Word} and all cards deleted"
                : $"{BoxStrings.Word} deleted, cards unassigned");
            _parent.RemoveBox(this);
        }
    }
}
