using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public sealed record PrayerTimeEntry(int PrayerId, string CardTitle, string PrayerTitle, string Details);

public class PrayerTimeViewModel : ObservableObject, IQueryAttributable
{
    private readonly IPrayerService _prayerService;
    private readonly ICardService _cardService;
    private readonly ITagService _tagService;
    private readonly IPrayerInteractionService _interactionService;
    private CancellationTokenSource _loadCts = new();

    public IReadOnlyList<PrayerTimeEntry> Entries { get; private set; } = Array.Empty<PrayerTimeEntry>();

    private int _currentIndex;
    public int CurrentIndex
    {
        get => _currentIndex;
        private set
        {
            if (SetProperty(ref _currentIndex, value))
            {
                OnPropertyChanged(nameof(CurrentEntry));
                OnPropertyChanged(nameof(ProgressDisplay));
                OnPropertyChanged(nameof(HasPrevious));
                OnPropertyChanged(nameof(HasNext));
            }
        }
    }

    public PrayerTimeEntry? CurrentEntry => Entries.Count > 0 ? Entries[CurrentIndex] : null;
    public string ProgressDisplay => Entries.Count > 0 ? $"{CurrentIndex + 1} of {Entries.Count}" : string.Empty;
    public bool HasPrevious => CurrentIndex > 0;
    public bool HasNext => CurrentIndex < Entries.Count - 1;

    private bool _hasCompleted;
    public bool HasCompleted
    {
        get => _hasCompleted;
        private set => SetProperty(ref _hasCompleted, value);
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand EndSessionCommand { get; }

    public PrayerTimeViewModel()
    {
        _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
        _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
        _tagService = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();
        _interactionService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerInteractionService>();

        NextCommand = new AsyncRelayCommand(NextAsync);
        PreviousCommand = new RelayCommand(Previous);
        EndSessionCommand = new AsyncRelayCommand(EndSessionAsync);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var scope = query.TryGetValue("scope", out var s) ? s?.ToString() : "all";
        var tagIdsStr = query.TryGetValue("tagIds", out var t) ? t?.ToString() : null;

        IEnumerable<int> tagIds = tagIdsStr is not null
            ? tagIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(x => int.TryParse(x, out var id) ? id : -1)
                       .Where(id => id > 0)
            : Enumerable.Empty<int>();

        _ = LoadEntriesAsync(scope == "tags" ? tagIds : null);
    }

    private async Task LoadEntriesAsync(IEnumerable<int>? tagIds)
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        try
        {
            var allActive = await _prayerService.GetAllActivePrayersAsync();
            token.ThrowIfCancellationRequested();

            IEnumerable<Prayer> filtered;
            if (tagIds is not null)
            {
                var prayerIdSet = (await _tagService.GetPrayerIdsByTagIdsAsync(tagIds)).ToHashSet();
                token.ThrowIfCancellationRequested();
                filtered = allActive.Where(p => prayerIdSet.Contains(p.Id));
            }
            else
            {
                filtered = allActive;
            }

            var cards = await _cardService.GetCardsAsync();
            token.ThrowIfCancellationRequested();

            var cardLookup = cards.ToDictionary(c => c.Id, c => c.Title);

            var entries = filtered
                .Select(p => new PrayerTimeEntry(
                    PrayerId: p.Id,
                    CardTitle: cardLookup.TryGetValue(p.PrayerCardId, out var cardTitle) ? cardTitle : "Unknown",
                    PrayerTitle: p.Title,
                    Details: p.Details ?? string.Empty))
                .ToList();

            Entries = entries.AsReadOnly();
            CurrentIndex = 0;
            HasCompleted = entries.Count == 0;
        }
        catch (OperationCanceledException)
        {
            // Load was superseded by a newer call — ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load prayer time entries: {ex.Message}");
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Error", "Unable to load prayers for this session.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task NextAsync()
    {
        if (HasCompleted) return;

        if (CurrentEntry is not null)
        {
            try
            {
                await _interactionService.LogInteractionAsync(CurrentEntry.PrayerId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log interaction: {ex.Message}");
            }
        }

        if (HasNext)
            CurrentIndex++;
        else
            HasCompleted = true;
    }

    private void Previous()
    {
        if (HasPrevious)
            CurrentIndex--;
    }

    private async Task EndSessionAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
