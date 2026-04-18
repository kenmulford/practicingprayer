using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerCardsViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ICardService _cardService;
        private readonly IPrayerService _prayerService;
        private readonly IOnboardingService _onboardingService;
        private readonly ITagService _tagService;
        private readonly IBoxService _boxService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly ISettings _settings;
        private Dictionary<int, HashSet<int>> _cardTagIds = new();
        public ObservableCollection<PrayerCardViewModel> AllPrayerCards { get; }
        public ObservableCollection<TagFilterChipViewModel> AvailableTags { get; } = new();
        public bool HasTags => AvailableTags.Count > 0;

        private ObservableCollection<BoxSectionViewModel> _boxSections = new();
        /// <summary>
        /// Grouped sections bound to the CollectionView. Each section is a BoxSectionViewModel
        /// that contains the PrayerCardViewModels for cards in that box.
        /// Replaced (not mutated) on each rebuild to avoid iOS UICollectionView layout desync.
        /// </summary>
        public ObservableCollection<BoxSectionViewModel> BoxSections
        {
            get => _boxSections;
            private set => SetProperty(ref _boxSections, value);
        }

        private bool _isSorting;
        private bool _suppressFilterAnnounce;
        private readonly Dictionary<PrayerCardViewModel, System.ComponentModel.PropertyChangedEventHandler> _cardHandlers = new();
        private CancellationTokenSource? _filterAnnounceCts;

        /// <summary>Cached box list for section building. Refreshed on LoadAsync/RefreshAsync.</summary>
        private IReadOnlyList<CardBox> _boxes = Array.Empty<CardBox>();

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

        /// <summary>True when no sections exist (no data loaded). Used to control EmptyView visibility.</summary>
        public bool HasNoSections => BoxSections.Count == 0;

        private bool _showCollectionsBanner;
        public bool ShowCollectionsBanner
        {
            get => _showCollectionsBanner;
            private set => SetProperty(ref _showCollectionsBanner, value);
        }

        public ICommand DismissCollectionsBannerCommand { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        public ICommand NewCommand { get; }
        public ICommand CancelMultiSelectCommand { get; }
        public ICommand MoveSelectedCommand { get; }
        public ICommand LongPressCardCommand { get; }
        public ICommand EnterMultiSelectCommand { get; }
        public ICommand OpenCollectionsCommand { get; }

        private bool _isMultiSelectMode;
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                if (SetProperty(ref _isMultiSelectMode, value))
                {
                    OnPropertyChanged(nameof(SelectedCardCount));
                    OnPropertyChanged(nameof(SelectedCountText));
                    // Propagate to sections for header dimming
                    foreach (var section in BoxSections)
                        section.IsMultiSelectMode = value;
                    // Propagate to cards so the check slot in the DataTemplate can bind directly
                    foreach (var card in AllPrayerCards)
                        card.IsMultiSelectMode = value;
                }
            }
        }

        public int SelectedCardCount => AllPrayerCards.Count(c => c.IsMultiSelected);

        public string SelectedCountText
        {
            get
            {
                var count = SelectedCardCount;
                return count switch { 0 => "None selected", 1 => "1 selected", _ => $"{count} selected" };
            }
        }

        /// <summary>Raised when a newly created card should be scrolled to and highlighted.</summary>
        public event EventHandler<PrayerCardViewModel>? HighlightCardRequested;

        #region Constructors

        public PrayerCardsViewModel(ICardService cardService, IPrayerService prayerService,
            IOnboardingService onboardingService, INavigationService navigationService,
            IAccessibilityService accessibilityService, ITagService tagService, ISettings settings,
            IBoxService boxService)
        {
            _cardService = cardService;
            _prayerService = prayerService;
            _onboardingService = onboardingService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _tagService = tagService;
            _settings = settings;
            _boxService = boxService;

            AllPrayerCards = new ObservableCollection<PrayerCardViewModel>();
            _showCollectionsBanner = !settings.CollectionsBannerDismissed;

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerCardAsync);
            CancelMultiSelectCommand = new RelayCommand(ExitMultiSelectMode);
            MoveSelectedCommand = new AsyncRelayCommand(MoveSelectedAsync);
            LongPressCardCommand = new RelayCommand<PrayerCardViewModel>(card =>
            {
                if (card != null) EnterMultiSelectMode(card);
            });
            EnterMultiSelectCommand = new RelayCommand(EnterMultiSelectModeFromToolbar);
            OpenCollectionsCommand = new AsyncRelayCommand(() => _navigationService.GoToAsync(Routes.BoxesPage));
            DismissCollectionsBannerCommand = new RelayCommand(() =>
            {
                _settings.CollectionsBannerDismissed = true;
                ShowCollectionsBanner = false;
            });
        }

        public PrayerCardsViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ISettings>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>())
        { }

        #endregion

        #region Private Methods

        private async Task BuildCardTagLookupAsync()
        {
            var allJunctions = await PrayerCardTag.LoadAllAsync();
            var allPrayers = await _prayerService.GetAllPrayersAsync();

            // Build set of unanswered prayer IDs
            var unansweredIds = allPrayers
                .Where(p => !p.IsAnswered)
                .Select(p => p.Id)
                .ToHashSet();

            // Build prayer-to-card lookup
            var prayerToCard = allPrayers.ToDictionary(p => p.Id, p => p.PrayerCardId);

            // Build cardId → Set<tagId> from unanswered prayers only
            var lookup = new Dictionary<int, HashSet<int>>();
            foreach (var row in allJunctions.Where(r => r.PrayerRequestId > 0 && unansweredIds.Contains(r.PrayerRequestId)))
            {
                if (prayerToCard.TryGetValue(row.PrayerRequestId, out var cardId))
                {
                    if (!lookup.ContainsKey(cardId))
                        lookup[cardId] = new HashSet<int>();
                    lookup[cardId].Add(row.PrayerTagId);
                }
            }

            _cardTagIds = lookup;
        }

        private PrayerCardViewModel CreateCardViewModel(PrayerCard pc) =>
            new(pc, _cardService, _prayerService, _onboardingService,
                _navigationService, _accessibilityService, _boxService);

        private async Task NewPrayerCardAsync()
        {
            _onboardingService.Advance(); // CreateCard → NameCard (no-op if not at CreateCard)
            await _navigationService.GoToAsync(Routes.PrayerCardPage);
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerCardString = query["deleted"].ToString();
                PrayerCardViewModel? matched = AllPrayerCards.FirstOrDefault<PrayerCardViewModel>(pc => pc.Identifier == PrayerCardString);

                if (matched != null)
                {
                    UnsubscribeFromPropertyChanges(matched);
                    AllPrayerCards.Remove(matched);
                    RebuildSections();
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerCardString = query["saved"].ToString();
                PrayerCardViewModel? matched = AllPrayerCards.Where((c) => c.Identifier == PrayerCardString).FirstOrDefault();

                // If card is found, update it
                if (matched != null)
                {
                    matched.Reload();
                    // Card's BoxId may have changed — rebuild sections
                    RebuildSections();
                }
                // If card isn't found, it's new; add it.
                else
                {
                    AddNewCardAsync(PrayerCardString).SafeFireAndForget();
                }
            }
            else if (query.ContainsKey("prayerSaved") && query.ContainsKey("parentCardId"))
            {
                if (int.TryParse(query["prayerSaved"].ToString(), out int prayerId)
                    && int.TryParse(query["parentCardId"].ToString(), out int parentCardId))
                {
                    // If the prayer moved to a different card, remove it from the old card
                    if (query.TryGetValue("oldCardId", out var oldVal)
                        && int.TryParse(oldVal?.ToString(), out int oldCardId))
                    {
                        var oldCard = AllPrayerCards.FirstOrDefault(card => card.Id == oldCardId);
                        oldCard?.RemovePrayer(prayerId);
                    }

                    var matched = AllPrayerCards.FirstOrDefault(card => card.Id == parentCardId);
                    if (matched != null)
                    {
                        // Re-expand the parent card so the user sees their saved prayer
                        matched.IsExpanded = true;
                        matched.AddOrUpdatePrayerAsync(prayerId).SafeFireAndForget();
                        matched.RefreshActivePrayerCount();
                    }
                }
            }
            else if (query.ContainsKey("prayerDeleted") && query.ContainsKey("parentCardId"))
            {
                if (int.TryParse(query["prayerDeleted"].ToString(), out int prayerId)
                    && int.TryParse(query["parentCardId"].ToString(), out int parentCardId))
                {
                    var matched = AllPrayerCards.FirstOrDefault(card => card.Id == parentCardId);
                    matched?.RemovePrayer(prayerId);
                }
            }
            else if (query.ContainsKey("imported"))
            {
                // Deep link import — caches already invalidated by DeepLinkService.
                // Full refresh needed because a new card/prayer was created externally.
                RefreshAsync().SafeFireAndForget();
            }
        }

        #endregion

        #region Helper Methods

        private async Task AddNewCardAsync(string? cardIdString)
        {
            try
            {
                var card = await PrayerCard.LoadAsync(int.Parse(cardIdString ?? "0"));
                if (card is null) return;
                var newCard = CreateCardViewModel(card);
                SubscribeToPropertyChanges(newCard);
                AllPrayerCards.Add(newCard);

                // Expand + highlight the new card (accordion handler auto-collapses others)
                newCard.IsExpanded = true;
                newCard.IsHighlighted = true;

                RebuildSections();

                HighlightCardRequested?.Invoke(this, newCard);
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to add new card: {e.Message}", "OK");
            }
        }

        public async Task LoadAsync()
        {
            if (IsMultiSelectMode) ExitMultiSelectMode();
            IsLoading = true;
            _suppressFilterAnnounce = true;
            try
            {
                _cardService.InvalidateCache();
                _boxService.InvalidateCache();
                var cards = await _cardService.GetCardsAsync();
                _boxes = await _boxService.GetBoxesAsync();

                var viewModels = cards.Select(pc => CreateCardViewModel(pc)).ToList();
                foreach (var vm in viewModels)
                {
                    SubscribeToPropertyChanges(vm);
                }

                foreach (var old in AllPrayerCards)
                    UnsubscribeFromPropertyChanges(old);
                AllPrayerCards.Clear();
                foreach (var vm in viewModels)
                {
                    AllPrayerCards.Add(vm);
                }

                // Build tag filter data
                _tagService.InvalidateCache();
                await BuildCardTagLookupAsync();

                var tags = await _tagService.GetTagsAsync();
                AvailableTags.Clear();
                foreach (var tag in tags)
                {
                    var chip = new TagFilterChipViewModel(tag, _ => ApplyFilter());
                    AvailableTags.Add(chip);
                }
                OnPropertyChanged(nameof(HasTags));

                RebuildSections();
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to load card: {e.Message}", "OK");
            }
            finally
            {
                _suppressFilterAnnounce = false;
                IsLoading = false;
            }
        }

        private static IOrderedEnumerable<PrayerCardViewModel> SortCards(IEnumerable<PrayerCardViewModel> cards) =>
            cards.OrderByDescending(c => c.IsFavorite).ThenBy(c => c.Title);

        /// <summary>
        /// Parses the persisted expanded-section setting into a HashSet of BoxIds.
        /// Empty string → empty set → all collapsed by default.
        /// </summary>
        private HashSet<int> GetSavedExpandedIds()
        {
            var raw = _settings.ExpandedSectionIds;
            if (string.IsNullOrEmpty(raw)) return new HashSet<int>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
                       .Where(id => id.HasValue)
                       .Select(id => id!.Value)
                       .ToHashSet();
        }

        /// <summary>
        /// Persists the current expanded-section state to settings.
        /// Called from the code-behind when a section header is tapped.
        /// </summary>
        public void SaveSectionExpansionState()
        {
            var expandedIds = BoxSections
                .Where(s => s.IsExpanded)
                .Select(s => s.BoxId.ToString());
            _settings.ExpandedSectionIds = string.Join(",", expandedIds);
        }

        /// <summary>
        /// Rebuilds sections from AllPrayerCards grouped by BoxId.
        /// Preserves existing BoxSectionViewModel instances (and their user expansion state)
        /// when the same BoxId is present. New sections use persisted expansion state
        /// (collapsed by default on first launch).
        /// Sort order: Unboxed → user boxes (A→Z by name) → System → Archived.
        /// </summary>
        private void RebuildSections()
        {
            if (_isSorting) return;
            _isSorting = true;
            try
            {
                var cardsByBox = AllPrayerCards
                    .GroupBy(c => c.BoxId)
                    .ToDictionary(g => g.Key, g => SortCards(g).ToList());

                // Preserve existing sections to retain user expansion state
                var existingSections = BoxSections.ToDictionary(s => s.BoxId);
                var savedExpandedIds = GetSavedExpandedIds();
                var sections = new List<BoxSectionViewModel>();

                // Helper: reuse existing section or create new one
                BoxSectionViewModel GetOrCreate(int boxId, Func<BoxSectionViewModel> factory)
                {
                    if (existingSections.TryGetValue(boxId, out var existing))
                        return existing;
                    return factory();
                }

                // 1. Unboxed section (BoxId == 0)
                var unboxedCards = cardsByBox.GetValueOrDefault(0);
                if (unboxedCards is { Count: > 0 })
                {
                    var unboxed = GetOrCreate(0, () => new BoxSectionViewModel(
                        defaultExpanded: savedExpandedIds.Contains(0)));
                    unboxed.SetCards(unboxedCards);
                    sections.Add(unboxed);
                }

                // 2. User boxes (not system, sorted by name) — always shown, even when empty
                foreach (var box in _boxes.Where(b => !b.IsSystem).OrderBy(b => b.Name))
                {
                    var boxCards = cardsByBox.GetValueOrDefault(box.Id) ?? new List<PrayerCardViewModel>();
                    var section = GetOrCreate(box.Id, () => new BoxSectionViewModel(box,
                        defaultExpanded: savedExpandedIds.Contains(box.Id)));
                    section.SetCards(boxCards);
                    sections.Add(section);
                }

                // 3. System box
                var systemBox = _boxes.FirstOrDefault(b => b.SystemKey == CardBox.SystemKeySystem);
                if (systemBox != null)
                {
                    var systemCards = cardsByBox.GetValueOrDefault(systemBox.Id);
                    if (systemCards is { Count: > 0 })
                    {
                        var systemSection = GetOrCreate(systemBox.Id, () => new BoxSectionViewModel(systemBox,
                            defaultExpanded: savedExpandedIds.Contains(systemBox.Id)));
                        systemSection.SetCards(systemCards);
                        sections.Add(systemSection);
                    }
                }

                // 4. Archived box (always shown even when empty, collapsed by default)
                var archivedBox = _boxes.FirstOrDefault(b => b.SystemKey == CardBox.SystemKeyArchived);
                if (archivedBox != null)
                {
                    var archivedSection = GetOrCreate(archivedBox.Id, () => new BoxSectionViewModel(archivedBox,
                        defaultExpanded: savedExpandedIds.Contains(archivedBox.Id)));
                    archivedSection.SetCards(cardsByBox.GetValueOrDefault(archivedBox.Id) ?? new List<PrayerCardViewModel>());
                    sections.Add(archivedSection);
                }

                BoxSections = new ObservableCollection<BoxSectionViewModel>(sections);
                OnPropertyChanged(nameof(HasNoSections));
            }
            finally
            {
                _isSorting = false;
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var hasSearch = !string.IsNullOrWhiteSpace(_searchText);
            var searchQuery = _searchText?.Trim() ?? string.Empty;

            var selectedTagIds = AvailableTags
                .Where(c => c.IsSelected)
                .Select(c => c.Tag.Id)
                .ToHashSet();
            var hasTagFilter = selectedTagIds.Count > 0;
            var hasAnyFilter = hasSearch || hasTagFilter;

            // Group once, look up per section — O(cards + sections) instead of O(sections × cards)
            var cardsByBox = AllPrayerCards
                .GroupBy(c => c.BoxId)
                .ToDictionary(g => g.Key, g => SortCards(g).ToList());

            var totalVisible = 0;

            foreach (var section in BoxSections)
            {
                IEnumerable<PrayerCardViewModel> sectionCards =
                    cardsByBox.GetValueOrDefault(section.BoxId) ?? new List<PrayerCardViewModel>();

                if (hasSearch)
                    sectionCards = sectionCards.Where(c =>
                        c.Title?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false);

                if (hasTagFilter)
                    sectionCards = sectionCards.Where(c =>
                        _cardTagIds.TryGetValue(c.Id, out var tagIds) &&
                        selectedTagIds.Overlaps(tagIds));

                var filteredCards = sectionCards.ToList();
                section.SetCards(filteredCards);
                totalVisible += filteredCards.Count;

                if (hasAnyFilter && filteredCards.Count > 0)
                    section.FilterExpand();
                else if (!hasAnyFilter)
                    section.RestoreUserExpansionState();
            }

            if (!_suppressFilterAnnounce)
            {
                _accessibilityService.NotifyLayoutChanged();
                AnnounceFilterCountDebounced(totalVisible);
            }
        }

        private void AnnounceFilterCountDebounced(int count)
        {
            _filterAnnounceCts?.Cancel();
            _filterAnnounceCts?.Dispose();
            _filterAnnounceCts = new CancellationTokenSource();
            var token = _filterAnnounceCts.Token;
            Task.Delay(400, token).ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested)
                    _accessibilityService.Announce($"Showing {count} cards");
            }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        private void SubscribeToPropertyChanges(PrayerCardViewModel card)
        {
            void Handler(object? s, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(PrayerCardViewModel.Title))
                {
                    RebuildSections();
                }
                else if (e.PropertyName == nameof(PrayerCardViewModel.IsExpanded))
                {
                    if (card.IsExpanded)
                    {
                        foreach (var other in AllPrayerCards)
                            if (other != card && other.IsExpanded)
                                other.IsExpanded = false;
                    }
                    else
                    {
                        // Re-sort on collapse so favorite changes take effect
                        // at a natural transition point (not while user is interacting)
                        RebuildSections();
                    }

                    _accessibilityService.Announce(card.IsExpanded
                        ? $"Expanded {card.Title}"
                        : $"Collapsed {card.Title}");
                }
            }

            card.PropertyChanged += Handler;
            _cardHandlers[card] = Handler;
        }

        private void UnsubscribeFromPropertyChanges(PrayerCardViewModel card)
        {
            if (_cardHandlers.Remove(card, out var handler))
                card.PropertyChanged -= handler;
        }

        public void Reload()
        {
            LoadAsync().SafeFireAndForget();
        }

        /// <summary>
        /// Lightweight refresh for cross-tab consistency. Detects new/deleted cards
        /// without tearing down the entire ViewModel state (preserving expanded
        /// accordions, loaded prayers, etc.). Called from OnAppearing on subsequent
        /// tab visits.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (IsMultiSelectMode) ExitMultiSelectMode();
            _cardService.InvalidateCache();
            _prayerService.InvalidateCache();
            _boxService.InvalidateCache();
            var cards = await _cardService.GetCardsAsync();
            _boxes = await _boxService.GetBoxesAsync();
            var freshIds = cards.Select(c => c.Id).ToHashSet();
            var currentIds = AllPrayerCards.Select(c => c.Id).ToHashSet();

            // Remove deleted cards
            var toRemove = AllPrayerCards.Where(c => !freshIds.Contains(c.Id)).ToList();
            foreach (var vm in toRemove)
            {
                UnsubscribeFromPropertyChanges(vm);
                AllPrayerCards.Remove(vm);
            }

            // Add new cards
            foreach (var card in cards.Where(c => !currentIds.Contains(c.Id)))
            {
                var vm = CreateCardViewModel(card);
                SubscribeToPropertyChanges(vm);
                AllPrayerCards.Add(vm);
            }

            // Refresh prayer counts + reload prayers on expanded cards
            // (e.g. QuickAdd added a prayer, or "Save +" added multiple)
            foreach (var vm in AllPrayerCards)
            {
                vm.RefreshActivePrayerCount();
                if (vm.IsExpanded)
                    vm.ReloadPrayers();
            }

            // Rebuild tag filter data
            _tagService.InvalidateCache();
            await BuildCardTagLookupAsync();

            var tags = await _tagService.GetTagsAsync();
            var currentTagIds = AvailableTags.Select(c => c.Tag.Id).ToHashSet();
            var freshTagIds = tags.Select(t => t.Id).ToHashSet();

            // Remove deleted tags
            var chipsToRemove = AvailableTags.Where(c => !freshTagIds.Contains(c.Tag.Id)).ToList();
            foreach (var chip in chipsToRemove)
                AvailableTags.Remove(chip);

            // Add new tags
            foreach (var tag in tags.Where(t => !currentTagIds.Contains(t.Id)))
            {
                var chip = new TagFilterChipViewModel(tag, _ => ApplyFilter());
                AvailableTags.Add(chip);
            }
            OnPropertyChanged(nameof(HasTags));

            RebuildSections();
        }

        #endregion

        #region Multi-Select

        private DateTime _multiSelectEnteredAt;

        /// <summary>Enters multi-select mode from the toolbar without pre-selecting a card.
        /// Provides an accessible alternative to long-press for screen reader users.</summary>
        private void EnterMultiSelectModeFromToolbar()
        {
            if (IsMultiSelectMode) return;
            _multiSelectEnteredAt = DateTime.UtcNow;
            IsMultiSelectMode = true;
            NotifySelectionCount();
            _accessibilityService.Announce("Selection mode. Tap cards to select them.");
        }

        /// <summary>Enters multi-select mode with the given card pre-selected.</summary>
        public void EnterMultiSelectMode(PrayerCardViewModel card)
        {
            if (IsMultiSelectMode) return;
            _multiSelectEnteredAt = DateTime.UtcNow;
            IsMultiSelectMode = true;
            card.IsMultiSelected = true;
            NotifySelectionCount();
            _accessibilityService.Announce("Selection mode. Tap cards to select them.");
        }

        /// <summary>Toggles a card's selection state while in multi-select mode.</summary>
        public void ToggleCardSelection(PrayerCardViewModel card)
        {
            if (!IsMultiSelectMode) return;
            // iOS fires tap on finger-up after long-press — suppress the immediate
            // deselect of the card that just triggered multi-select entry.
            if ((DateTime.UtcNow - _multiSelectEnteredAt).TotalMilliseconds < 300) return;
            card.IsMultiSelected = !card.IsMultiSelected;
            NotifySelectionCount();
        }

        private void ExitMultiSelectMode()
        {
            foreach (var card in AllPrayerCards)
                card.IsMultiSelected = false;
            IsMultiSelectMode = false;
            _accessibilityService.Announce("Selection cancelled");
        }

        private async Task MoveSelectedAsync()
        {
            var selected = AllPrayerCards.Where(c => c.IsMultiSelected).ToList();
            if (selected.Count == 0) return;

            // Build picker options: user boxes + "Loose Cards"
            var boxes = await _boxService.GetBoxesAsync();
            var options = new List<string> { BoxStrings.Unorganized };
            var userBoxes = boxes.Where(b => !b.IsSystem).OrderBy(b => b.Name).ToList();
            options.AddRange(userBoxes.Select(b => b.Name));

            var result = await _navigationService.DisplayActionSheetAsync(
                $"Move {selected.Count} card{(selected.Count == 1 ? "" : "s")} to…",
                "Cancel", null, options.ToArray());

            if (result is null or "Cancel") return;

            // Resolve the selected box ID
            int targetBoxId;
            if (result == BoxStrings.Unorganized)
            {
                targetBoxId = 0;
            }
            else
            {
                var targetBox = userBoxes.FirstOrDefault(b => b.Name == result);
                if (targetBox == null) return;
                targetBoxId = targetBox.Id;
            }

            // Batch assign
            foreach (var card in selected)
                await _cardService.AssignBoxAsync(card.Card, targetBoxId);

            _accessibilityService.Announce(
                $"Moved {selected.Count} card{(selected.Count == 1 ? "" : "s")} to {result}");

            ExitMultiSelectMode();
            RebuildSections();
        }

        private void NotifySelectionCount()
        {
            OnPropertyChanged(nameof(SelectedCardCount));
            OnPropertyChanged(nameof(SelectedCountText));
        }

        #endregion
    }
}
