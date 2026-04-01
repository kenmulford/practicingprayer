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
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly ISettings _settings;
        private Dictionary<int, HashSet<int>> _cardTagIds = new();
        public ObservableCollection<PrayerCardViewModel> AllPrayerCards { get; }
        public ObservableCollection<TagFilterChipViewModel> AvailableTags { get; } = new();
        public bool HasTags => AvailableTags.Count > 0;

        private ObservableCollection<PrayerCardViewModel> _filteredPrayerCards = new();
        /// <summary>
        /// Filtered view of cards bound to the CollectionView. Replaced (not mutated) on each
        /// filter pass to avoid iOS UICollectionView layout desync from rapid CollectionChanged events.
        /// </summary>
        public ObservableCollection<PrayerCardViewModel> FilteredPrayerCards
        {
            get => _filteredPrayerCards;
            private set => SetProperty(ref _filteredPrayerCards, value);
        }
        private bool _isSorting;
        private bool _suppressFilterAnnounce;
        private readonly Dictionary<PrayerCardViewModel, System.ComponentModel.PropertyChangedEventHandler> _cardHandlers = new();
        private CancellationTokenSource? _filterAnnounceCts;

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

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        public ICommand NewCommand { get; }

        /// <summary>Raised when a newly created card should be scrolled to and highlighted.</summary>
        public event EventHandler<PrayerCardViewModel>? HighlightCardRequested;

        #region Constructors

        public PrayerCardsViewModel(ICardService cardService, IPrayerService prayerService,
            IOnboardingService onboardingService, INavigationService navigationService,
            IAccessibilityService accessibilityService, ITagService tagService, ISettings settings)
        {
            _cardService = cardService;
            _prayerService = prayerService;
            _onboardingService = onboardingService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _tagService = tagService;
            _settings = settings;

            AllPrayerCards = new ObservableCollection<PrayerCardViewModel>();

            // register commands
            NewCommand = new AsyncRelayCommand(NewPrayerCardAsync);
        }

        public PrayerCardsViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
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
                _navigationService, _accessibilityService);

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
                    ApplySorting();
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

                ApplySorting(); // also calls ApplyFilter()

                HighlightCardRequested?.Invoke(this, newCard);
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to add new card: {e.Message}", "OK");
            }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            _suppressFilterAnnounce = true;
            try
            {
                _cardService.InvalidateCache();
                var cards = await _cardService.GetCardsAsync();

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

                ApplySorting();
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

        private void ApplySorting()
        {
            if (_isSorting) return;
            _isSorting = true;
            try
            {
                var sorted = AllPrayerCards
                    .OrderByDescending(pc => pc.IsSystem)
                    .ThenByDescending(pc => pc.IsFavorite)
                    .ThenBy(pc => pc.Title)
                    .ToList();

                // Only update if order changed (minimize UI updates)
                bool needsUpdate = false;
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (i >= AllPrayerCards.Count || AllPrayerCards[i] != sorted[i])
                    {
                        needsUpdate = true;
                        break;
                    }
                }

                if (needsUpdate)
                {
                    AllPrayerCards.Clear();
                    foreach (var card in sorted)
                    {
                        AllPrayerCards.Add(card);
                    }
                }
            }
            finally
            {
                _isSorting = false;
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<PrayerCardViewModel> result = AllPrayerCards;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = _searchText.Trim();
                result = result.Where(c =>
                    c.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            // Tag chip filter
            var selectedTagIds = AvailableTags
                .Where(c => c.IsSelected)
                .Select(c => c.Tag.Id)
                .ToHashSet();

            if (selectedTagIds.Count > 0)
            {
                result = result.Where(c =>
                    _cardTagIds.TryGetValue(c.Id, out var tagIds) &&
                    selectedTagIds.Overlaps(tagIds));
            }

            FilteredPrayerCards = new ObservableCollection<PrayerCardViewModel>(result);

            if (!_suppressFilterAnnounce)
            {
                _accessibilityService.NotifyLayoutChanged();
                AnnounceFilterCountDebounced();
            }
        }

        private void AnnounceFilterCountDebounced()
        {
            _filterAnnounceCts?.Cancel();
            _filterAnnounceCts?.Dispose();
            _filterAnnounceCts = new CancellationTokenSource();
            var token = _filterAnnounceCts.Token;
            var count = FilteredPrayerCards.Count;
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
                    ApplySorting();
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
                        ApplySorting();
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
            _cardService.InvalidateCache();
            _prayerService.InvalidateCache();
            var cards = await _cardService.GetCardsAsync();
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

            ApplySorting();
        }

        #endregion
    }
}
