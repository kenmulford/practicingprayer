using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

/// <summary>
/// Checkbox wrapper for one prayer card in the "Choose cards" modal. Holds the
/// modal's OWN selection state (independent of the cards-list session selection)
/// plus the card's active prayer IDs so Start can union them without re-querying.
/// </summary>
public class SelectableCard : ObservableObject
{
    private bool _isSelected;
    private readonly Action? _onSelectionChanged;
    public PrayerCard Card { get; }
    public IReadOnlyList<int> ActivePrayerIds { get; }

    public string Title => Card.Title;
    public int ActivePrayerCount => ActivePrayerIds.Count;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                _onSelectionChanged?.Invoke();
        }
    }

    public SelectableCard(PrayerCard card, IReadOnlyList<int> activePrayerIds,
        Action? onSelectionChanged = null)
    {
        Card = card;
        ActivePrayerIds = activePrayerIds;
        _onSelectionChanged = onSelectionChanged;
    }
}

/// <summary>
/// "Choose cards" modal VM: a collection-filter picker over the top, a checkbox
/// list of eligible cards (≥1 active prayer, non-system, non-archived), and a
/// "Start Prayer Time (N)" button that resolves the selected cards to the union
/// of their active prayer IDs, hands them to <see cref="IPrayerSelectionService"/>,
/// and launches Prayer Time in <c>scope=selection</c>.
///
/// Mirrors <see cref="PrayerTimeBoxScopeViewModel"/>; reuses the existing
/// <see cref="BoxPickerItem"/> family for the collection filter.
/// </summary>
public class PrayerTimeCardSelectViewModel : ObservableObject
{
    private readonly IBoxService _boxService;
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IPrayerSelectionService _selectionService;
    private readonly ISettings _settings;

    // Full eligible-card set (all collections), narrowed into Cards by the filter.
    private readonly List<SelectableCard> _allEligibleCards = new();
    private bool _loaded;
    private bool _loading;

    /// <summary>Collection-filter picker rows: "All collections" sentinel + one per eligible user box.</summary>
    public ObservableCollection<BoxPickerItem> AvailableBoxes { get; } = new();

    /// <summary>Cards visible under the current collection filter.</summary>
    public ObservableCollection<SelectableCard> Cards { get; } = new();

    private BoxPickerItem? _selectedBox;
    public BoxPickerItem? SelectedBox
    {
        get => _selectedBox;
        set
        {
            if (SetProperty(ref _selectedBox, value))
                ApplyFilter();
        }
    }

    private int _selectedCount;
    /// <summary>Number of currently-selected cards. Drives the Start button label + canExecute.</summary>
    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(StartButtonText));
                OnPropertyChanged(nameof(StartButtonDescription));
                OnPropertyChanged(nameof(HasSelection));
                (StartCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedCount > 0;
    public string StartButtonText => $"Start Prayer Time ({SelectedCount})";
    public string StartButtonDescription =>
        SelectedCount == 0
            ? "Select at least one card to begin"
            : $"Begin prayer time with {SelectedCount} card{(SelectedCount == 1 ? "" : "s")}";

    private bool _isEmpty;
    /// <summary>True when the filtered collection has no eligible cards — drives the empty hint.</summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }

    public PrayerTimeCardSelectViewModel(IBoxService boxService, ICardService cardService,
        IPrayerService prayerService, INavigationService navigationService,
        IAccessibilityService accessibilityService, IPrayerSelectionService selectionService,
        ISettings settings)
    {
        _boxService = boxService;
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _selectionService = selectionService;
        _settings = settings;

        StartCommand = new AsyncRelayCommand(StartAsync, () => HasSelection);
        CancelCommand = new AsyncRelayCommand(CancelAsync);

        LoadCardsAsync().SafeFireAndForget();
    }

    public PrayerTimeCardSelectViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerSelectionService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
    { }

    public async Task LoadCardsAsync()
    {
        // Re-entrancy guard (mirrors ConfirmImportViewModel.LoadBoxesAsync): the
        // ctor fires this once; a second caller (or a resume) must not rebuild the
        // wrapper set out from under live selections / bound Cards instances.
        if (_loaded || _loading) return;
        _loading = true;
        try
        {
            // Single active-prayer load + group by card, mirroring
            // PrayerTimeBoxScopeViewModel.LoadBoxesAsync (avoids N per-card queries).
            var boxesTask = _boxService.GetBoxesAsync();
            var cardsTask = _cardService.GetCardsAsync();
            var prayersTask = _prayerService.GetAllActivePrayersAsync();
            await Task.WhenAll(boxesTask, cardsTask, prayersTask);

            var allBoxes = boxesTask.Result;
            var cards = cardsTask.Result;
            var activePrayers = prayersTask.Result;

            // active prayer IDs grouped by card
            var idsByCard = activePrayers
                .GroupBy(p => p.PrayerCardId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(p => p.Id).ToList());

            // Eligible cards: non-system, not in the Archived box, with ≥1 active prayer.
            // Rebuild from scratch so a re-entrant load (constructor fire-and-forget
            // + an explicit caller) cannot desync Cards from _allEligibleCards.
            _allEligibleCards.Clear();
            SelectedCount = 0;
            foreach (var card in cards)
            {
                if (card.IsSystem) continue;
                if (card.BoxId == _settings.ArchivedFolderId) continue;
                if (!idsByCard.TryGetValue(card.Id, out var ids) || ids.Count == 0) continue;
                _allEligibleCards.Add(new SelectableCard(card, ids, RecomputeSelectedCount));
            }

            // Build the collection-filter picker: "All collections" + each user box
            // that contains an eligible card. Mirrors the !IsSystem / archived
            // exclusion used in PrayerTimeBoxScopeViewModel.LoadBoxesAsync.
            var eligibleBoxIds = _allEligibleCards.Select(c => c.Card.BoxId).ToHashSet();
            AvailableBoxes.Clear();
            AvailableBoxes.Add(AllCollectionsPickerItem.Instance);
            foreach (var box in allBoxes.Where(b => !b.IsSystem && eligibleBoxIds.Contains(b.Id)))
                AvailableBoxes.Add(new RealBoxPickerItem(box.Id, box.Name));

            SelectedBox = AllCollectionsPickerItem.Instance; // triggers ApplyFilter
            _loaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cards: {ex.Message}");
            await _navigationService.DisplayAlertAsync("Error", "Unable to load cards.", "OK");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RecomputeSelectedCount()
    {
        SelectedCount = _allEligibleCards.Count(c => c.IsSelected);
        // Announce the running count on each toggle so screen-reader users hear how
        // many cards are selected (acceptance criterion). This path is toggle-only —
        // LoadCardsAsync resets the count via the direct SelectedCount setter, so the
        // cold-load reset never reaches here and never announces (mirrors the
        // suppress-on-load discipline in prayer-app-accessibility).
        _accessibilityService.Announce($"{SelectedCount} selected");
    }

    private void ApplyFilter()
    {
        IEnumerable<SelectableCard> visible = _allEligibleCards;
        if (SelectedBox is RealBoxPickerItem real)
            visible = visible.Where(c => c.Card.BoxId == real.BoxId);

        Cards.Clear();
        foreach (var card in visible.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase))
            Cards.Add(card);

        IsEmpty = Cards.Count == 0;
    }

    private async Task StartAsync()
    {
        if (!HasSelection)
        {
            await _navigationService.DisplayAlertAsync(
                "No Cards Selected", "Please select at least one card.", "OK");
            return;
        }

        // Union of active prayer IDs across selected cards.
        var ids = _allEligibleCards
            .Where(c => c.IsSelected)
            .SelectMany(c => c.ActivePrayerIds)
            .Distinct()
            .ToList();

        _selectionService.Set(ids);
        await _navigationService.PopModalAsync();
        await _navigationService.GoToAsync(
            $"{Routes.PrayerTimePage}?scope={Routes.ScopeSelection}");
    }

    public async Task CancelAsync()
    {
        await _navigationService.PopModalAsync();
    }
}
