using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Services;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class HomeViewModel : ObservableObject
{
    private readonly IPrayerService _prayerService;
    private readonly ICardService _cardService;
    private readonly ITagService _tagService;
    private readonly INavigationService _navigationService;
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
            }
        }
    }

    public string ActiveCardLabel => ActiveCardCount == 1 ? "Active Card" : "Active Cards";
    public bool HasActiveCards => ActiveCardCount > 0;
    public string ActiveCardTapHint => HasActiveCards ? "View Cards \u2192" : "Add a Card \u2192";

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
            }
        }
    }

    public string UnansweredLabel => UnansweredCount == 1 ? "Unanswered Prayer" : "Unanswered Prayers";
    public bool HasUnanswered => UnansweredCount > 0;
    public string UnansweredTapHint => HasUnanswered ? "View Prayers \u2192" : "Quick Add \u2192";

    // ── Last prayed date metric ───────────────────────────────────────

    private string _lastPrayedMonth = string.Empty;
    public string LastPrayedMonth
    {
        get => _lastPrayedMonth;
        private set => SetProperty(ref _lastPrayedMonth, value);
    }

    private string _lastPrayedDay = string.Empty;
    public string LastPrayedDay
    {
        get => _lastPrayedDay;
        private set => SetProperty(ref _lastPrayedDay, value);
    }

    private bool _hasLastPrayed;
    public bool HasLastPrayed
    {
        get => _hasLastPrayed;
        private set
        {
            if (SetProperty(ref _hasLastPrayed, value))
                OnPropertyChanged(nameof(LastPrayedTapHint));
        }
    }

    public string LastPrayedTapHint => HasLastPrayed ? "Pray Now \u2192" : "Start Praying \u2192";

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

    // ── Commands ──────────────────────────────────────────────────────

    public ICommand GoToOverdueCommand { get; }
    public ICommand GoToCardsCommand { get; }
    public ICommand GoToPrayersCommand { get; }

    // ── Constructors ──────────────────────────────────────────────────

    public HomeViewModel(IPrayerService prayerService, ICardService cardService,
        ITagService tagService, INavigationService navigationService, ISettings settings)
    {
        _prayerService = prayerService;
        _cardService = cardService;
        _tagService = tagService;
        _navigationService = navigationService;
        _settings = settings;

        GoToOverdueCommand = new AsyncRelayCommand(GoToOverdueAsync);
        GoToCardsCommand = new AsyncRelayCommand(() => _navigationService.GoToAsync(Routes.PrayerCardsTab));
        GoToPrayersCommand = new AsyncRelayCommand(() => _navigationService.GoToAsync(Routes.PrayersTab));
    }

    public HomeViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
    { }

    // ── Data Loading ──────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        try
        {
            _prayerService.InvalidateCache();
            _cardService.InvalidateCache();
            _tagService.InvalidateCache();

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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load home dashboard: {ex.Message}");
        }
    }

    // ── Command Handlers ──────────────────────────────────────────────

    private async Task GoToOverdueAsync()
    {
        if (!HasOverdue) return;
        await _navigationService.GoToAsync($"{Routes.PrayersTab}?filter=overdue");
    }
}
