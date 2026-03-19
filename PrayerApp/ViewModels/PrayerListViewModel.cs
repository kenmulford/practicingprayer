using PrayerApp.Models;
using PrayerApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public enum FilterStatus { Active, Answered, All }

    internal class PrayerListViewModel : ObservableObject, IQueryAttributable
    {
        private List<Prayer> _prayerList = new();
        private readonly IPrayerService _prayerService;
        private readonly ICardService _cardService;
        private readonly ITagService _tagService;
        private Dictionary<int, string> _cardTitleLookup = new();

        // requestId → set of tagIds assigned to that request (for chip filter)
        private Dictionary<int, HashSet<int>> _requestTagIds = new();

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
                    ApplyFilter();
                }
            }
        }
        public bool IsActiveSelected   => StatusFilter == FilterStatus.Active;
        public bool IsAnsweredSelected => StatusFilter == FilterStatus.Answered;
        public bool IsAllSelected      => StatusFilter == FilterStatus.All;

        public ICommand NewCommand { get; }
        public ICommand SetStatusCommand { get; }

        public PrayerListViewModel()
        {
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            _cardService   = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
            _tagService    = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();

            // Any change to the backing store re-runs the filter
            AllPrayers.CollectionChanged += (_, _) => ApplyFilter();

            // Commands
            NewCommand = new AsyncRelayCommand(NewPrayerAsync);
            SetStatusCommand = new RelayCommand<string>(s =>
            {
                StatusFilter = s switch
                {
                    "Answered" => FilterStatus.Answered,
                    "All"      => FilterStatus.All,
                    _          => FilterStatus.Active
                };
            });
        }

        public async Task LoadAsync()
        {
            // Build card title lookup
            var cards = await _cardService.GetCardsAsync();
            _cardTitleLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

            // Load all prayers
            _prayerList = (await _prayerService.GetAllPrayersAsync()).ToList();

            // Build tag→request lookup for chip filter
            _requestTagIds = await BuildRequestTagLookupAsync();

            // Build prayer ViewModels
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

            // Initial filtered view
            ApplyFilter();
        }

        #region IQueryAttributable

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                var id = query["deleted"].ToString();
                var matched = AllPrayers.FirstOrDefault(p => p.Identifier == id);
                if (matched != null)
                    AllPrayers.Remove(matched); // CollectionChanged → ApplyFilter
            }
            else if (query.ContainsKey("saved"))
            {
                _ = HandleSavedAsync(query["saved"].ToString());
            }
        }

        #endregion

        #region Private helpers

        private async Task HandleSavedAsync(string? id)
        {
            // Rebuild tag lookup so chip filter reflects tag changes made on the detail page
            _requestTagIds = await BuildRequestTagLookupAsync();

            // Refresh tag chips in case a new tag was created on the detail page
            var allTags = (await _tagService.GetTagsAsync()).ToList();
            var existingIds = AvailableTags.Select(c => c.Tag.Id).ToHashSet();
            foreach (var tag in allTags.Where(t => !existingIds.Contains(t.Id)))
                AvailableTags.Add(new TagFilterChipViewModel(tag, _ => ApplyFilter()));
            OnPropertyChanged(nameof(HasTags));

            var matched = AllPrayers.FirstOrDefault(p => p.Identifier == id);
            if (matched != null)
            {
                matched.Reload();
                ApplyFilter();
            }
            else
            {
                await AddNewPrayerAsync(id);
            }
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
        }

        private async Task AddNewPrayerAsync(string? prayerIdString)
        {
            try
            {
                var p = await Prayer.LoadAsync(int.Parse(prayerIdString ?? "0"));
                var vm = BuildViewModel(p);
                AllPrayers.Add(vm); // CollectionChanged → ApplyFilter
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to add new prayer: {e.Message}", "OK");
            }
        }

        private async Task NewPrayerAsync()
        {
            await Shell.Current.GoToAsync(nameof(Views.Prayer.PrayerDetailPage));
        }

        private async Task LoadPrayersAsync()
        {
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

                AllPrayers.Clear();
                foreach (var vm in viewModels)
                    AllPrayers.Add(vm);

                ApplyFilter();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load prayers: {e.Message}", "OK");
            }
        }

        private void SubscribeToPropertyChanges(PrayerRequestDetailViewModel prayer)
        {
            prayer.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(PrayerRequestDetailViewModel.Title)
                                   or nameof(PrayerRequestDetailViewModel.IsAnswered))
                    ApplyFilter();
            };
        }

        public void Reload() => _ = LoadPrayersAsync();

        #endregion
    }
}
