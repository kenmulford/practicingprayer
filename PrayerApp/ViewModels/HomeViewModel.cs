using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

/// <summary>A single overdue prayer shown in the home dashboard suggestion list.</summary>
public class SuggestedPrayerViewModel
{
    public int PrayerId { get; }
    public string CardTitle { get; }
    public string PrayerTitle { get; }
    public ICommand SelectCommand { get; }

    public SuggestedPrayerViewModel(int prayerId, string cardTitle, string prayerTitle,
        INavigationService navigationService)
    {
        PrayerId = prayerId;
        CardTitle = cardTitle;
        PrayerTitle = prayerTitle;
        SelectCommand = new AsyncRelayCommand(() =>
            navigationService.GoToAsync($"{Routes.PrayerDetailPage}?load={PrayerId}&viewOnly=true"));
    }
}

public class HomeViewModel : ObservableObject
{
    private readonly IPrayerService _prayerService;
    private readonly ICardService _cardService;
    private readonly INavigationService _navigationService;
    private readonly ISettings _settings;

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
        0 => "You're all caught up!",
        1 => "1 Overdue",
        _ => $"{OverdueCount} Overdue"
    };

    public string OverdueEmptyDescription
    {
        get
        {
            var days = _settings.OverdueDayThreshold;
            var dayLabel = days == 1 ? "day" : "days";
            return $"Requests not prayed for in {days} {dayLabel} will appear here. You can update this in Settings.";
        }
    }

    private string _lastPrayedDisplay = string.Empty;
    public string LastPrayedDisplay
    {
        get => _lastPrayedDisplay;
        private set => SetProperty(ref _lastPrayedDisplay, value);
    }

    public ObservableCollection<SuggestedPrayerViewModel> SuggestedPrayers { get; } = new();

    public ICommand GoToOverdueCommand { get; }

    public HomeViewModel(IPrayerService prayerService, ICardService cardService,
        INavigationService navigationService, ISettings settings)
    {
        _prayerService = prayerService;
        _cardService = cardService;
        _navigationService = navigationService;
        _settings = settings;
        GoToOverdueCommand = new AsyncRelayCommand(GoToOverdueAsync);
    }

    public HomeViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
    { }

    public async Task LoadAsync()
    {
        try
        {
            _prayerService.InvalidateCache();
            _cardService.InvalidateCache();
            var overdue = await _prayerService.GetOverduePrayersAsync(_settings.OverdueDayThreshold);
            OverdueCount = overdue.Count;
            OnPropertyChanged(nameof(OverdueEmptyDescription));

            var cards = await _cardService.GetCardsAsync();
            var cardLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

            SuggestedPrayers.Clear();
            foreach (var p in overdue.Take(5))
            {
                var cardTitle = cardLookup.TryGetValue(p.PrayerCardId, out var t) ? t : string.Empty;
                SuggestedPrayers.Add(new SuggestedPrayerViewModel(p.Id, cardTitle, p.Title, _navigationService));
            }

            // Last prayed date
            var lastDate = await _prayerService.GetLastInteractionDateAsync();
            LastPrayedDisplay = FormatLastPrayed(lastDate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load home dashboard: {ex.Message}");
        }
    }

    internal static string FormatLastPrayed(DateTime? date)
    {
        if (date is null) return "Never";
        var days = (DateTime.Now - date.Value).TotalDays;
        return days switch
        {
            < 1    => "Today",
            < 2    => "Yesterday",
            < 7    => $"{(int)days} days ago",
            < 14   => "Last week",
            < 30   => $"{(int)(days / 7)} weeks ago",
            _      => $"{(int)(days / 30)} month{((int)(days / 30) == 1 ? "" : "s")} ago"
        };
    }

    private async Task GoToOverdueAsync()
    {
        if (!HasOverdue) return;
        await _navigationService.GoToAsync("//PrayersPage?filter=overdue");
    }
}
