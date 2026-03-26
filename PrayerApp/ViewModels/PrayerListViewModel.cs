using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public enum FilterStatus { Active, Answered, All, Overdue }

    public class PrayerListViewModel : ObservableObject, IQueryAttributable
    {
        private List<Prayer> _prayerList = new();
        private readonly IPrayerService _prayerService;
        private readonly ICardService _cardService;
        private readonly ITagService _tagService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly ISettings _settings;
        private Dictionary<int, string> _cardTitleLookup = new();

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    _accessibilityService.Announce(value ? "Loading" : "Content loaded");
            }
        }

        // requestId → set of tagIds assigned to that request (for chip filter)
        private Dictionary<int, HashSet<int>> _requestTagIds = new();

        // prayer IDs considered overdue (not prayed in 30+ days)
        private HashSet<int> _overdueIds = new();

        // Suppress screen reader announcements during bulk loads
        private bool _suppressAnnounce;
        private bool _suppressFilter;
        private readonly Dictionary<PrayerRequestDetailViewModel, System.ComponentModel.PropertyChangedEventHandler> _prayerHandlers = new();

        // Pre-selected tag from tag detail navigation (F6)
        private int _preselectedTagId;

        // Full unfiltered backing store — manipulated by IQueryAttributable
        public ObservableCollection<PrayerRequestDetailViewModel> AllPrayers { get; } = new();

        // Filtered + sorted view that the CollectionView binds to
        public ObservableCollection<PrayerRequestDetailViewModel> FilteredPrayers { get; } = new();

        // Tag chips
        public ObservableCollection<TagFilterChipViewModel> AvailableTags { get; } = new();
        public bool HasTags => AvailableTags.Count > 0;

        // Search
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        // Status toggle
        private FilterStatus _statusFilter = FilterStatus.Active;
        public FilterStatus StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    OnPropertyChanged(nameof(IsActiveSelected));
                    OnPropertyChanged(nameof(IsAnsweredSelected));
                    OnPropertyChanged(nameof(IsAllSelected));
                    OnPropertyChanged(nameof(IsOverdueSelected));
                    ApplyFilter();
                }
            }
        }
        public bool IsActiveSelected   => StatusFilter == FilterStatus.Active;
        public bool IsAnsweredSelected => StatusFilter == FilterStatus.Answered;
        public bool IsAllSelected      => StatusFilter == FilterStatus.All;
        public bool IsOverdueSelected  => StatusFilter == FilterStatus.Overdue;

        public ICommand NewCommand { get; }
        public ICommand SetStatusCommand { get; }

        public PrayerListViewModel(IPrayerService prayerService, ICardService cardService, ITagService tagService,
            INavigationService navigationService, IAccessibilityService accessibilityService, ISettings settings)
        {
            _prayerService = prayerService;
            _cardService   = cardService;
            _tagService    = tagService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _settings = settings;

            // Any change to the backing store re-runs the filter (suppressed during bulk loads)
            AllPrayers.CollectionChanged += (_, _) => { if (!_suppressFilter) ApplyFilter(); };

            // Commands
            NewCommand = new AsyncRelayCommand(NewPrayerAsync);
            SetStatusCommand = new RelayCommand<string>(s =>
            {
                StatusFilter = s switch
                {
                    "Answered" => FilterStatus.Answered,
                    "All"      => FilterStatus.All,
                    "Overdue"  => FilterStatus.Overdue,
                    _          => FilterStatus.Active
                };
            });
        }

        public PrayerListViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
        { }

        public async Task LoadAsync()
        {
            IsLoading = true;
            _suppressAnnounce = true;
            try
            {
                _suppressFilter = true;

                // Build card title lookup
                var cards = await _cardService.GetCardsAsync();
                _cardTitleLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

                // Load all prayers
                _prayerList = (await _prayerService.GetAllPrayersAsync()).ToList();

                // Build tag→request lookup for chip filter
                _requestTagIds = await BuildRequestTagLookupAsync();

                // Build overdue set for Overdue filter
                var overdue = await _prayerService.GetOverduePrayersAsync(_settings.OverdueDayThreshold);
                _overdueIds = overdue.Select(p => p.Id).ToHashSet();

                // Build prayer ViewModels
                foreach (var old in AllPrayers)
                    UnsubscribeFromPropertyChanges(old);
                AllPrayers.Clear();
                foreach (var p in _prayerList)
                {
                    var vm = BuildViewModel(p);
                    AllPrayers.Add(vm);
                }

                // Load tag chips
                var allTags = (await _tagService.GetTagsAsync()).ToList();
                AvailableTags.Clear();
                foreach (var tag in allTags)
                    AvailableTags.Add(new TagFilterChipViewModel(tag, _ => ApplyFilter()));
                OnPropertyChanged(nameof(HasTags));

                // Apply pre-selected tag if navigated from tag detail
                ApplyPreselectedTag();

                // Initial filtered view
                ApplyFilter();
            }
            finally
            {
                _suppressFilter = false;
                _suppressAnnounce = false;
                IsLoading = false;
            }
        }

        #region IQueryAttributable

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("tagId"))
            {
                if (int.TryParse(query["tagId"].ToString(), out int tagId))
                {
                    _preselectedTagId = tagId;
                    ApplyPreselectedTag();
                    ApplyFilter();
                }
            }
            else if (query.ContainsKey("filter"))
            {
                var filter = query["filter"].ToString();
                StatusFilter = filter switch
                {
                    "overdue" => FilterStatus.Overdue,
                    _         => FilterStatus.Active
                };
            }
            else if (query.ContainsKey("deleted"))
            {
                var id = query["deleted"].ToString();
                var matched = AllPrayers.FirstOrDefault(p => p.Identifier == id);
                if (matched != null)
                {
                    UnsubscribeFromPropertyChanges(matched);
                    AllPrayers.Remove(matched); // CollectionChanged → ApplyFilter
                }
            }
            else if (query.ContainsKey("saved"))
            {
                HandleSavedAsync(query["saved"].ToString()).SafeFireAndForget();
            }
        }

        #endregion

        #region Private helpers

        private async Task HandleSavedAsync(string? id)
        {
            // Rebuild tag lookup so chip filter reflects tag changes made on the detail page
            _requestTagIds = await BuildRequestTagLookupAsync();

            // Refresh tag chips — add new tags, remove deleted tags
            var allTags = (await _tagService.GetTagsAsync()).ToList();
            var freshTagIds = allTags.Select(t => t.Id).ToHashSet();
            var existingIds = AvailableTags.Select(c => c.Tag.Id).ToHashSet();

            // Remove chips for tags that no longer exist
            var chipsToRemove = AvailableTags.Where(c => !freshTagIds.Contains(c.Tag.Id)).ToList();
            foreach (var chip in chipsToRemove)
                AvailableTags.Remove(chip);

            // Add chips for newly created tags
            foreach (var tag in allTags.Where(t => !existingIds.Contains(t.Id)))
                AvailableTags.Add(new TagFilterChipViewModel(tag, _ => ApplyFilter()));
            OnPropertyChanged(nameof(HasTags));

            var matched = AllPrayers.FirstOrDefault(p => p.Identifier == id);
            if (matched != null)
            {
                if (int.TryParse(id, out int prayerId))
                {
                    var freshPrayer = await Prayer.LoadAsync(prayerId);
                    if (freshPrayer is not null)
                    {
                        // Refresh card lookup in case the prayer moved to a different card
                        var cards = await _cardService.GetCardsAsync();
                        _cardTitleLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

                        // Update all list-visible properties from the awaited DB load
                        // (replaces fire-and-forget Reload which raced with ApplyFilter)
                        matched.Title = freshPrayer.Title;
                        matched.IsAnswered = freshPrayer.IsAnswered;
                        matched.CardTitle = _cardTitleLookup.TryGetValue(freshPrayer.PrayerCardId, out var t) ? t : string.Empty;
                    }
                }
                ApplyFilter();
            }
            else
            {
                await AddNewPrayerAsync(id);
            }
        }

        private void ApplyPreselectedTag()
        {
            if (_preselectedTagId <= 0) return;

            // Deselect all chips first
            foreach (var chip in AvailableTags)
                chip.IsSelected = false;

            var target = AvailableTags.FirstOrDefault(c => c.Tag.Id == _preselectedTagId);
            if (target is not null)
            {
                target.IsSelected = true;
                // Bypass setter to avoid premature ApplyFilter — caller runs it once
                if (_statusFilter != FilterStatus.All)
                {
                    _statusFilter = FilterStatus.All;
                    OnPropertyChanged(nameof(StatusFilter));
                    OnPropertyChanged(nameof(IsActiveSelected));
                    OnPropertyChanged(nameof(IsAnsweredSelected));
                    OnPropertyChanged(nameof(IsAllSelected));
                    OnPropertyChanged(nameof(IsOverdueSelected));
                }
                _accessibilityService.Announce($"Filtered by {target.Tag.Name}");
            }

            _preselectedTagId = 0;
        }

        private PrayerRequestDetailViewModel BuildViewModel(Prayer p)
        {
            var vm = new PrayerRequestDetailViewModel(p);
            vm.CardTitle = _cardTitleLookup.TryGetValue(p.PrayerCardId, out var t) ? t : string.Empty;
            SubscribeToPropertyChanges(vm);
            return vm;
        }

        private async Task<Dictionary<int, HashSet<int>>> BuildRequestTagLookupAsync()
        {
            var lookup = new Dictionary<int, HashSet<int>>();
            var allRows = await PrayerCardTag.LoadAllAsync();
            foreach (var row in allRows.Where(r => r.PrayerRequestId > 0))
            {
                if (!lookup.ContainsKey(row.PrayerRequestId))
                    lookup[row.PrayerRequestId] = new HashSet<int>();
                lookup[row.PrayerRequestId].Add(row.PrayerTagId);
            }
            return lookup;
        }

        private void ApplyFilter()
        {
            IEnumerable<PrayerRequestDetailViewModel> result = AllPrayers;

            // 1. Status filter
            result = StatusFilter switch
            {
                FilterStatus.Active   => result.Where(p => !p.IsAnswered),
                FilterStatus.Answered => result.Where(p => p.IsAnswered),
                FilterStatus.Overdue  => result.Where(p => !p.IsAnswered && _overdueIds.Contains(p.Id)),
                _                     => result
            };

            // 2. Tag chip filter
            var selectedTagIds = AvailableTags
                .Where(c => c.IsSelected)
                .Select(c => c.Tag.Id)
                .ToHashSet();

            if (selectedTagIds.Count > 0)
            {
                result = result.Where(p =>
                    _requestTagIds.TryGetValue(p.Id, out var tagIds) &&
                    selectedTagIds.Overlaps(tagIds));
            }

            // 3. Text search (title + card name)
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = _searchText.Trim();
                result = result.Where(p =>
                    (p.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.CardTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // 4. Sort
            var sorted = result
                .OrderBy(p => p.CardTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FilteredPrayers.Clear();
            foreach (var p in sorted)
                FilteredPrayers.Add(p);

            if (!_suppressAnnounce)
                _accessibilityService.Announce($"Showing {FilteredPrayers.Count} prayers");
        }

        private async Task AddNewPrayerAsync(string? prayerIdString)
        {
            try
            {
                var p = await Prayer.LoadAsync(int.Parse(prayerIdString ?? "0"));
                if (p is null) return;
                var vm = BuildViewModel(p);
                AllPrayers.Add(vm); // CollectionChanged → ApplyFilter
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to add new prayer: {e.Message}", "OK");
            }
        }

        private async Task NewPrayerAsync()
        {
            await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?new=true");
        }

        private async Task LoadPrayersAsync()
        {
            _suppressAnnounce = true;
            _suppressFilter = true;
            try
            {
                _prayerService.InvalidateCache();
                var prayers = await _prayerService.GetAllPrayersAsync();
                _prayerList = prayers.ToList();

                var cards = await _cardService.GetCardsAsync();
                _cardTitleLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

                // Rebuild tag lookup
                _requestTagIds = await BuildRequestTagLookupAsync();

                var viewModels = _prayerList.Select(BuildViewModel).ToList();

                foreach (var old in AllPrayers)
                    UnsubscribeFromPropertyChanges(old);
                AllPrayers.Clear();
                foreach (var vm in viewModels)
                    AllPrayers.Add(vm);

                ApplyFilter();
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to load prayers: {e.Message}", "OK");
            }
            finally
            {
                _suppressFilter = false;
                _suppressAnnounce = false;
            }
        }

        private void SubscribeToPropertyChanges(PrayerRequestDetailViewModel prayer)
        {
            void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName is nameof(PrayerRequestDetailViewModel.Title)
                                   or nameof(PrayerRequestDetailViewModel.IsAnswered))
                    ApplyFilter();
            }

            prayer.PropertyChanged += Handler;
            _prayerHandlers[prayer] = Handler;
        }

        private void UnsubscribeFromPropertyChanges(PrayerRequestDetailViewModel prayer)
        {
            if (_prayerHandlers.Remove(prayer, out var handler))
                prayer.PropertyChanged -= handler;
        }

        public void Reload() => LoadPrayersAsync().SafeFireAndForget();

        /// <summary>
        /// Lightweight refresh for cross-tab consistency. Rebuilds the tag lookup
        /// and prayer list without tearing down the entire ViewModel state. Called
        /// from OnAppearing on subsequent tab visits.
        /// </summary>
        public async Task RefreshAsync()
        {
            _prayerService.InvalidateCache();

            // Rebuild prayer list
            var prayers = await _prayerService.GetAllPrayersAsync();
            var currentIds = AllPrayers.Select(p => p.Id).ToHashSet();
            var freshIds = prayers.Select(p => p.Id).ToHashSet();

            // Remove deleted prayers
            var toRemove = AllPrayers.Where(p => !freshIds.Contains(p.Id)).ToList();
            foreach (var vm in toRemove)
            {
                UnsubscribeFromPropertyChanges(vm);
                AllPrayers.Remove(vm);
            }

            // Refresh card lookup
            var cards = await _cardService.GetCardsAsync();
            _cardTitleLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

            // Update existing prayers with fresh data from DB
            var prayerLookup = prayers.ToDictionary(p => p.Id);
            foreach (var vm in AllPrayers)
            {
                if (prayerLookup.TryGetValue(vm.Id, out var fresh))
                {
                    vm.Title = fresh.Title;
                    vm.IsAnswered = fresh.IsAnswered;
                    vm.CardTitle = _cardTitleLookup.TryGetValue(fresh.PrayerCardId, out var t) ? t : string.Empty;
                }
            }

            // Add new prayers (e.g. from QuickAdd)
            foreach (var p in prayers.Where(p => !currentIds.Contains(p.Id)))
            {
                _prayerList.Add(p);
                AllPrayers.Add(BuildViewModel(p));
            }

            // Rebuild tag lookup for chip filter
            _requestTagIds = await BuildRequestTagLookupAsync();

            // Refresh tag chips — add new, remove deleted
            var allTags = (await _tagService.GetTagsAsync()).ToList();
            var freshTagIds = allTags.Select(t => t.Id).ToHashSet();
            var existingTagIds = AvailableTags.Select(c => c.Tag.Id).ToHashSet();

            var chipsToRemove = AvailableTags.Where(c => !freshTagIds.Contains(c.Tag.Id)).ToList();
            foreach (var chip in chipsToRemove)
                AvailableTags.Remove(chip);

            foreach (var tag in allTags.Where(t => !existingTagIds.Contains(t.Id)))
                AvailableTags.Add(new TagFilterChipViewModel(tag, _ => ApplyFilter()));

            // Rebuild overdue set so the Overdue filter reflects current state
            var overdue = await _prayerService.GetOverduePrayersAsync(_settings.OverdueDayThreshold);
            _overdueIds = overdue.Select(p => p.Id).ToHashSet();

            OnPropertyChanged(nameof(HasTags));
            ApplyPreselectedTag();
            ApplyFilter();
        }

        #endregion
    }
}
