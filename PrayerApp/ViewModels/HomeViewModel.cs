using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class HomeViewModel : ObservableObject
{
    private readonly IPrayerService _prayerService;
    private readonly ICardService _cardService;
    private readonly ITagService _tagService;
    private readonly IBoxService _boxService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly ISettings _settings;

    // ── Overdue metric ────────────────────────────────────────────────

    private int _overdueCount;
    public int OverdueCount
    {
        get => _overdueCount;
        private set
        {
            if (SetProperty(ref _overdueCount, value))
            {
                OnPropertyChanged(nameof(HasOverdue));
                OnPropertyChanged(nameof(OverdueHeadline));
                OnPropertyChanged(nameof(OverdueAccessible));
            }
        }
    }

    public bool HasOverdue => OverdueCount > 0;

    public string OverdueHeadline => OverdueCount switch
    {
        0 => "All requests have been recently prayed for.",
        1 => "1 Overdue",
        _ => $"{OverdueCount} Overdue"
    };

    public string OverdueAccessible => HasOverdue
        ? $"Overdue prayers, {OverdueCount}. Tap to view overdue prayers."
        : "All requests have been recently prayed for.";

    // ── Active card count metric ──────────────────────────────────────

    private int _activeCardCount;
    public int ActiveCardCount
    {
        get => _activeCardCount;
        private set
        {
            if (SetProperty(ref _activeCardCount, value))
            {
                OnPropertyChanged(nameof(ActiveCardLabel));
                OnPropertyChanged(nameof(HasActiveCards));
                OnPropertyChanged(nameof(ActiveCardTapHint));
                OnPropertyChanged(nameof(ActiveCardsAccessible));
            }
        }
    }

    public string ActiveCardLabel => ActiveCardCount == 1 ? "Active Card" : "Active Cards";
    public bool HasActiveCards => ActiveCardCount > 0;
    public string ActiveCardTapHint => HasActiveCards ? "View Cards \u2192" : "Add a Card \u2192";

    public string ActiveCardsAccessible => HasActiveCards
        ? $"Active cards, {ActiveCardCount}. Tap to view prayer cards."
        : "No active cards. Tap to create your first card.";

    // ── Unanswered prayer count metric ────────────────────────────────

    private int _unansweredCount;
    public int UnansweredCount
    {
        get => _unansweredCount;
        private set
        {
            if (SetProperty(ref _unansweredCount, value))
            {
                OnPropertyChanged(nameof(UnansweredLabel));
                OnPropertyChanged(nameof(HasUnanswered));
                OnPropertyChanged(nameof(UnansweredTapHint));
                OnPropertyChanged(nameof(UnansweredAccessible));
            }
        }
    }

    public string UnansweredLabel => UnansweredCount == 1 ? "Unanswered Prayer" : "Unanswered Prayers";
    public bool HasUnanswered => UnansweredCount > 0;
    public string UnansweredTapHint => HasUnanswered ? "View Prayers \u2192" : "Quick Add \u2192";

    public string UnansweredAccessible => HasUnanswered
        ? $"Unanswered prayers, {UnansweredCount}. Tap to view prayers."
        : "No prayers yet. Tap to add your first prayer.";

    // ── Last prayed date metric ───────────────────────────────────────

    private string _lastPrayedMonth = string.Empty;
    public string LastPrayedMonth
    {
        get => _lastPrayedMonth;
        private set
        {
            if (SetProperty(ref _lastPrayedMonth, value))
                OnPropertyChanged(nameof(LastPrayedAccessible));
        }
    }

    private string _lastPrayedDay = string.Empty;
    public string LastPrayedDay
    {
        get => _lastPrayedDay;
        private set
        {
            if (SetProperty(ref _lastPrayedDay, value))
                OnPropertyChanged(nameof(LastPrayedAccessible));
        }
    }

    private bool _hasLastPrayed;
    public bool HasLastPrayed
    {
        get => _hasLastPrayed;
        private set
        {
            if (SetProperty(ref _hasLastPrayed, value))
            {
                OnPropertyChanged(nameof(LastPrayedTapHint));
                OnPropertyChanged(nameof(LastPrayedAccessible));
            }
        }
    }

    public string LastPrayedTapHint => HasLastPrayed ? "Pray Now \u2192" : "Start Praying \u2192";

    public string LastPrayedAccessible => HasLastPrayed
        ? $"Last prayed, {LastPrayedMonth} {LastPrayedDay}."
        : "Not yet prayed.";

    // ── Answered on this date ─────────────────────────────────────────

    private IReadOnlyList<Prayer> _answeredOnThisDate = Array.Empty<Prayer>();
    public IReadOnlyList<Prayer> AnsweredOnThisDate
    {
        get => _answeredOnThisDate;
        private set
        {
            if (SetProperty(ref _answeredOnThisDate, value))
            {
                OnPropertyChanged(nameof(HasAnsweredOnThisDate));
                OnPropertyChanged(nameof(AnsweredOnThisDateAccessible));
            }
        }
    }

    public bool HasAnsweredOnThisDate => AnsweredOnThisDate.Count > 0;

    public string AnsweredOnThisDateHeader =>
        $"Answered prayers from {DateTime.Now:MMMM d}";

    public string AnsweredOnThisDateAccessible =>
        HasAnsweredOnThisDate
            ? $"{AnsweredOnThisDateHeader}: {string.Join(", ", AnsweredOnThisDate.Select(p => p.Title))}"
            : string.Empty;

    // ── Prayer Time readiness ─────────────────────────────────────────

    private bool _hasActivePrayers;
    public bool HasActivePrayers
    {
        get => _hasActivePrayers;
        private set => SetProperty(ref _hasActivePrayers, value);
    }

    private bool _hasTags;
    public bool HasTags
    {
        get => _hasTags;
        private set => SetProperty(ref _hasTags, value);
    }

    private bool _hasUserBoxesWithCards;
    public bool HasUserBoxesWithCards
    {
        get => _hasUserBoxesWithCards;
        private set => SetProperty(ref _hasUserBoxesWithCards, value);
    }

    // ── Commands ──────────────────────────────────────────────────────

    public ICommand GoToOverdueCommand { get; }
    public ICommand GoToCardsCommand { get; }
    public ICommand GoToPrayersCommand { get; }

    // ── Constructors ──────────────────────────────────────────────────

    public HomeViewModel(IPrayerService prayerService, ICardService cardService,
        ITagService tagService, IBoxService boxService, INavigationService navigationService,
        IAccessibilityService accessibilityService, ISettings settings)
    {
        _prayerService = prayerService;
        _cardService = cardService;
        _tagService = tagService;
        _boxService = boxService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _settings = settings;

        GoToOverdueCommand = new AsyncRelayCommand(GoToOverdueAsync);
        GoToCardsCommand = new AsyncRelayCommand(() => _navigationService.GoToAsync(Routes.PrayerCardsTab));
        GoToPrayersCommand = new AsyncRelayCommand(() => _navigationService.GoToAsync(Routes.PrayersTab));
    }

    public HomeViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
    { }

    // ── Loading state ─────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    // ── Data Loading ──────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _prayerService.InvalidateCache();
            _cardService.InvalidateCache();
            _tagService.InvalidateCache();
            _boxService.InvalidateCache();

            // Overdue
            var overdue = await _prayerService.GetOverduePrayersAsync(_settings.OverdueDayThreshold);
            OverdueCount = overdue.Count;

            // Active prayers (reused for multiple metrics)
            var activePrayers = await _prayerService.GetAllActivePrayersAsync();
            HasActivePrayers = activePrayers.Count > 0;

            // Active card count: distinct cards with ≥1 unanswered prayer
            ActiveCardCount = activePrayers.Select(p => p.PrayerCardId).Distinct().Count();

            // Unanswered prayer count
            UnansweredCount = activePrayers.Count;

            // Tags (for Prayer Time scope)
            var tags = await _tagService.GetTagsAsync();
            HasTags = tags.Count > 0;

            // User collections with active prayers (for Prayer Time scope)
            var boxes = await _boxService.GetBoxesAsync();
            var cards = await _cardService.GetCardsAsync();
            var cardIdsWithPrayers = activePrayers.Select(p => p.PrayerCardId).ToHashSet();
            HasUserBoxesWithCards = boxes
                .Where(b => !b.IsSystem)
                .Any(b => cards.Any(c => c.BoxId == b.Id && cardIdsWithPrayers.Contains(c.Id)));

            // Last prayed date components
            var lastDate = await _prayerService.GetLastInteractionDateAsync();
            if (lastDate is not null)
            {
                LastPrayedMonth = lastDate.Value.ToString("MMM").ToUpper();
                LastPrayedDay = lastDate.Value.Day.ToString();
                HasLastPrayed = true;
            }
            else
            {
                LastPrayedMonth = string.Empty;
                LastPrayedDay = string.Empty;
                HasLastPrayed = false;
            }

            // Prayers answered on this date in prior years
            AnsweredOnThisDate = await _prayerService.GetAnsweredOnThisDateAsync()
                ?? Array.Empty<Prayer>();
            OnPropertyChanged(nameof(AnsweredOnThisDateHeader));

            _accessibilityService.Announce("Dashboard loaded");
        }
        catch (Exception ex)
        {
            AnsweredOnThisDate = Array.Empty<Prayer>();
            System.Diagnostics.Debug.WriteLine($"Failed to load home dashboard: {ex.Message}");
            _accessibilityService.Announce("Failed to load dashboard");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Command Handlers ──────────────────────────────────────────────

    private async Task GoToOverdueAsync()
    {
        if (!HasOverdue) return;
        await _navigationService.GoToAsync($"{Routes.PrayersTab}?filter=overdue");
    }
}
