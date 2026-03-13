using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Services;
using PrayerApp.Views.Prayer;
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

    public SuggestedPrayerViewModel(int prayerId, string cardTitle, string prayerTitle)
    {
        PrayerId = prayerId;
        CardTitle = cardTitle;
        PrayerTitle = prayerTitle;
        SelectCommand = new AsyncRelayCommand(SelectAsync);
    }

    private async Task SelectAsync() =>
        await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={PrayerId}&viewOnly=true");
}

public class HomeViewModel : ObservableObject
{
    private readonly IPrayerService _prayerService;
    private readonly ICardService _cardService;

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
        0 => "You're up to date — every request has been prayed for recently.",
        1 => "1 prayer request hasn't been prayed for in the past month.",
        _ => $"{OverdueCount} prayer requests haven't been prayed for in the past month."
    };

    public ObservableCollection<SuggestedPrayerViewModel> SuggestedPrayers { get; } = new();

    public HomeViewModel()
    {
        _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
        _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
    }

    public async Task LoadAsync()
    {
        try
        {
            var overdue = await _prayerService.GetOverduePrayersAsync(30);
            OverdueCount = overdue.Count;

            var cards = await _cardService.GetCardsAsync();
            var cardLookup = cards.ToDictionary(c => c.Id, c => c.Title ?? string.Empty);

            SuggestedPrayers.Clear();
            foreach (var p in overdue.Take(3))
            {
                var cardTitle = cardLookup.TryGetValue(p.PrayerCardId, out var t) ? t : string.Empty;
                SuggestedPrayers.Add(new SuggestedPrayerViewModel(p.Id, cardTitle, p.Title));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load home dashboard: {ex.Message}");
        }
    }
}
