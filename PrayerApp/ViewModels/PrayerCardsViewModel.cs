using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Helpers;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerCardsViewModel : ObservableObject, IQueryAttributable, ISyncableViewModel
    {
        private readonly ICardService _cardService;
        private readonly IPrayerService _prayerService;
        private readonly IOnboardingService _onboardingService;
        private readonly ITagService _tagService;
        private readonly IBoxService _boxService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly ISettings _settings;
        private readonly IMessenger _messenger;
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
        // True after the first SyncAsync completes. The first sync suppresses the
        // filter-count announce so screen reader users don't get "Loading" + "Content
        // loaded" + "Showing N cards" all at once on cold load. Subsequent syncs
        // (messenger-driven) do announce — the count is the user-visible signal that
        // a CRUD elsewhere refreshed this page.
        private bool _hasSyncedOnce;

        // PERF-1: setting matched.IsExpanded = true in ConsumePendingSavedAsync cascades
        // into the IsExpanded handler, which collapses other expanded cards; each cascading
        // collapse calls RebuildSections, which replaces BoxSections and forces MAUI's
        // CollectionViewHandler on Android to reset its RecyclerView adapter (~600ms per
        // visible Border). 3+ cascading rebuilds = ~5s UI-thread blockage.
        private bool _suppressIsExpandedRebuild;

        // Slice 6a / PERF-10: collapses bursts of concurrent SyncAsync triggers (messenger
        // broadcast + PageSync.OnAppearingAsync + sibling rebroadcasts) into one in-flight
        // sync plus one coalesced follow-up. Each SyncAsync tail replaces BoxSections,
        // which on Android triggers a full RecyclerView re-inflate cascade (~330 ms per
        // visible cell). Pre-Slice-6a a single Save fired 3 cascades back-to-back.
        private readonly Helpers.SingleFlightGate _syncGate = new();
        private readonly Dictionary<PrayerCardViewModel, System.ComponentModel.PropertyChangedEventHandler> _cardHandlers = new();
        private CancellationTokenSource? _filterAnnounceCts;

        /// <summary>Cached box list for section building. Refreshed on SyncAsync.</summary>
        private IReadOnlyList<CardBox> _boxes = Array.Empty<CardBox>();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    _accessibilityService.Announce(value ? "Loading" : "Content loaded");
                    OnPropertyChanged(nameof(IsBusyOverall));
                }
            }
        }

        // Slice 6g — Cross-page save indicator continuity.
        // The post-save flow spans: edit-page Save → GoToAsync → Cards.OnAppearing
        // → SyncAsync (toggles IsLoading internally) → ConsumePendingSavedAsync →
        // ScrollTo. Without this flag, IsLoading flips off the moment SyncAsync's
        // finally runs (mid-flow), the LoadingOverlay disappears, and the user sees
        // half-rendered cells + the new card popping in late. View code-behind sets
        // IsAwaitingSavedCard=true at OnAppearing entry when PendingSavedIdentifier
        // is non-empty, and clears it after ScrollTo completes. The LoadingOverlay
        // binds to IsBusyOverall (= IsLoading || IsAwaitingSavedCard) so either
        // flag keeps it visible.
        private bool _isAwaitingSavedCard;
        public bool IsAwaitingSavedCard
        {
            get => _isAwaitingSavedCard;
            set
            {
                if (SetProperty(ref _isAwaitingSavedCard, value))
                    OnPropertyChanged(nameof(IsBusyOverall));
            }
        }

        public bool IsBusyOverall => _isLoading || _isAwaitingSavedCard;

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

        /// <summary>
        /// Lifecycle-gated post-save signal. ApplyQueryAttributes("saved") stages the
        /// identifier here; PrayerCardsPage.OnAppearing consumes it via ConsumePendingSavedAsync.
        /// Direct C# event handlers crashed on Android because the MauiRecyclerView adapter
        /// snapshot lagged the BoxSections rebuild — the property/lifecycle pattern lets
        /// the View gate the scroll on the dispatcher instead.
        /// </summary>
        private string? _pendingSavedIdentifier;
        public string? PendingSavedIdentifier
        {
            get => _pendingSavedIdentifier;
            private set => SetProperty(ref _pendingSavedIdentifier, value);
        }

        /// <summary>
        /// Captured at ApplyQueryAttributes time (before OnAppearing's SyncAsync runs):
        /// was the saved-id already present in AllPrayerCards? True = edit (don't highlight),
        /// false = newly created (highlight + scroll, even if SyncAsync's diff loop
        /// happens to add the row before ConsumePendingSavedAsync runs).
        /// </summary>
        private bool _pendingSavedWasAlreadyInList;

        #region Constructors

        public PrayerCardsViewModel(ICardService cardService, IPrayerService prayerService,
            IOnboardingService onboardingService, INavigationService navigationService,
            IAccessibilityService accessibilityService, ITagService tagService, ISettings settings,
            IBoxService boxService, IMessenger messenger)
        {
            _cardService = cardService;
            _prayerService = prayerService;
            _onboardingService = onboardingService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _tagService = tagService;
            _settings = settings;
            _boxService = boxService;
            _messenger = messenger;

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

            // Cross-page CRUD signals — Cards displays counts and tag chips that derive
            // from every entity type, so subscribe to all of them. Weak refs auto-clean
            // on GC. BulkChangedMessage covers backup restore / deep-link import.
            _messenger.Register<PrayerCardsViewModel, PrayerCardChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<PrayerCardsViewModel, PrayerChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<PrayerCardsViewModel, TagChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<PrayerCardsViewModel, CardBoxChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
            _messenger.Register<PrayerCardsViewModel, BulkChangedMessage>(this, (vm, _) => vm.SyncAsync().SafeFireAndForget());
        }

        public PrayerCardsViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ISettings>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IMessenger>())
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
                // Stage the identifier; ConsumePendingSavedAsync (called from OnAppearing)
                // does the actual work on the lifecycle channel — keeps the View free of
                // the VM→View C# event whose handler raced the MauiRecyclerView adapter
                // snapshot on Galaxy Ultra.
                var id = query["saved"].ToString();
                PendingSavedIdentifier = id;
                // Snapshot now: SyncAsync runs after this and may add the new card to
                // AllPrayerCards via its diff loop. We need to know whether the card was
                // *already* in the list before that, so the highlight decision in
                // ConsumePendingSavedAsync isn't fooled into the matched-edit branch.
                _pendingSavedWasAlreadyInList = AllPrayerCards.Any(c => c.Identifier == id);
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
                // Deep link import — full sync needed because a new card/prayer was
                // created externally. Belt-and-suspenders with DeepLinkService's
                // BulkChangedMessage; explicit call here makes the import → refresh
                // contract obvious at the call site.
                SyncAsync().SafeFireAndForget();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Consumes the saved-identifier signal staged by ApplyQueryAttributes. Returns
        /// the loaded/added card so the View can scroll/highlight, or null to mean
        /// "do not scroll" (no save pending, parse failure, DB miss, or matched-existing
        /// card — already in the rendered list, no scroll needed).
        /// </summary>
        public async Task<PrayerCardViewModel?> ConsumePendingSavedAsync()
        {
            PerfLog.Log($"ConsumePendingSavedAsync.entry id={PendingSavedIdentifier} wasInList={_pendingSavedWasAlreadyInList}");
            var id = PendingSavedIdentifier;
            if (string.IsNullOrEmpty(id)) return null;
            var wasAlreadyInList = _pendingSavedWasAlreadyInList;
            PendingSavedIdentifier = null;
            _pendingSavedWasAlreadyInList = false;

            var matched = AllPrayerCards.FirstOrDefault(c => c.Identifier == id);

            // Edit case: card was already in the list when ApplyQueryAttributes ran.
            // Just reload + rebuild; don't expand/highlight; return null so the View
            // doesn't ScrollTo (and doesn't risk the adapter race that crashed Galaxy Ultra).
            if (matched != null && wasAlreadyInList)
            {
                PerfLog.Log("ConsumePendingSavedAsync edit-path (matched, was-in-list)");
                matched.Reload();
                RebuildSections();
                return null;
            }

            // New-card case where SyncAsync's diff loop already added the row before
            // we got here. The matched VM is the one we want to highlight + scroll to.
            // SyncAsync's tail RebuildSections already placed the new card into the
            // correct BoxSection — no second rebuild needed here. The cascade-collapse
            // of any previously-expanded card still happens via the IsExpanded handler;
            // _suppressIsExpandedRebuild only suppresses *that* handler's own rebuild,
            // and BoxSections doesn't need to be replaced just because IsExpanded
            // toggled on cards already in their final sections.
            if (matched != null && !wasAlreadyInList)
            {
                PerfLog.Log("ConsumePendingSavedAsync new-via-sync (matched, NOT was-in-list)");
                _suppressIsExpandedRebuild = true;
                try
                {
                    matched.IsExpanded = true;
                    matched.IsHighlighted = true;
                }
                finally { _suppressIsExpandedRebuild = false; }
                return matched;
            }

            // New-card case where AllPrayerCards still doesn't have the row (e.g., cold-load
            // path with a saved=Id in the query string). Load the card from DB and add it.
            if (!int.TryParse(id, out var cardId)) return null;
            try
            {
                PerfLog.Log("ConsumePendingSavedAsync new-via-db before LoadAsync");
                var card = await PrayerCard.LoadAsync(cardId);
                if (card is null) return null;
                var newCard = CreateCardViewModel(card);
                SubscribeToPropertyChanges(newCard);
                AllPrayerCards.Add(newCard);
                _suppressIsExpandedRebuild = true;
                try
                {
                    newCard.IsExpanded = true;
                    newCard.IsHighlighted = true;
                }
                finally { _suppressIsExpandedRebuild = false; }
                RebuildSections();
                return newCard;
            }
            catch (Exception ex)
            {
                Diagnostics.ResolveLog()?.Log("PrayerCardsViewModel.ConsumePendingSavedAsync", ex);
                return null;
            }
        }

        /// <summary>
        /// Single primitive that brings the VM in line with the data store. Diff-based —
        /// idempotent across first-call and Nth-call. Replaces the prior LoadAsync /
        /// RefreshAsync split. Triggered by <see cref="Helpers.PageSync.OnAppearingAsync"/>
        /// and by entity-change messenger broadcasts. Bursts coalesce via
        /// <see cref="_syncGate"/>; user-driven UI state (multi-select, search, tag chips)
        /// is preserved across syncs.
        /// </summary>
        public async Task SyncAsync()
        {
            // Burst-scoped state: capture once before the gate runs. Per-iteration reset
            // would let a coalesced follow-up announce filter counts on the very first
            // cold load, defeating the suppress-on-first-sync contract.
            IsLoading = true;
            _suppressFilterAnnounce = !_hasSyncedOnce;
            try
            {
                await _syncGate.RunAsync(SyncCoreAsync);
                _hasSyncedOnce = true;
            }
            finally
            {
                _suppressFilterAnnounce = false;
                IsLoading = false;
            }
        }

        private async Task SyncCoreAsync()
        {
            // Service caches are auto-invalidated by their own mutation methods (Slice 2);
            // no defensive InvalidateCache call needed here.
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
            foreach (var vm in AllPrayerCards)
            {
                vm.RefreshActivePrayerCount();
                if (vm.IsExpanded)
                    vm.ReloadPrayers();
            }

            // Rebuild tag filter data
            await BuildCardTagLookupAsync();

            var tags = await _tagService.GetTagsAsync();
            var currentTagIds = AvailableTags.Select(c => c.Tag.Id).ToHashSet();
            var freshTagIds = tags.Select(t => t.Id).ToHashSet();

            var chipsToRemove = AvailableTags.Where(c => !freshTagIds.Contains(c.Tag.Id)).ToList();
            foreach (var chip in chipsToRemove)
                AvailableTags.Remove(chip);

            var tagsAdded = false;
            foreach (var tag in tags.Where(t => !currentTagIds.Contains(t.Id)))
            {
                var chip = new TagFilterChipViewModel(tag, _ => ApplyFilter());
                AvailableTags.Add(chip);
                tagsAdded = true;
            }
            // Skip the PropertyChanged when nothing changed — under messenger-driven
            // sync this would otherwise fire on every CRUD anywhere, churning bindings.
            if (chipsToRemove.Count > 0 || tagsAdded)
                OnPropertyChanged(nameof(HasTags));

            RebuildSections();
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
            PerfLog.Log($"RebuildSections.entry cards={AllPrayerCards.Count}");
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

                // Slice 6b: skip the BoxSections replacement when GetOrCreate reused every
                // section (no box added or removed — the typical save-flow case). Replacement
                // raises PropertyChanged on the grouped ItemsSource and forces a RecyclerView
                // re-inflate cascade on Android. Per-section card mutations surface via SetCards.
                // Hole: a CardBox.Name rename keeps the same section instance, so the section
                // header won't update unless BoxSectionViewModel observes box-property changes.
                // No rename UI today; revisit if one is added.
                if (!SectionListsReferenceEqual(sections, BoxSections))
                {
                    BoxSections = new ObservableCollection<BoxSectionViewModel>(sections);
                    OnPropertyChanged(nameof(HasNoSections));
                }
            }
            finally
            {
                _isSorting = false;
            }

            PerfLog.Log("RebuildSections.before ApplyFilter");
            ApplyFilter();
            PerfLog.Log("RebuildSections.exit");
        }

        private static bool SectionListsReferenceEqual(
            IList<BoxSectionViewModel> proposed,
            IList<BoxSectionViewModel> current)
        {
            if (proposed.Count != current.Count) return false;
            for (int i = 0; i < proposed.Count; i++)
                if (!ReferenceEquals(proposed[i], current[i])) return false;
            return true;
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
                    else if (!_suppressIsExpandedRebuild)
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
            _accessibilityService.Announce(SelectedCountText);
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
