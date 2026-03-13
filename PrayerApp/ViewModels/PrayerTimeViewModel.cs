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

    // Auto-mode
    private const int AutoIntervalSeconds = 30;
    private IDispatcherTimer? _autoTimer;

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

    private bool _isAutoMode;
    public bool IsAutoMode
    {
        get => _isAutoMode;
        private set
        {
            if (SetProperty(ref _isAutoMode, value))
            {
                OnPropertyChanged(nameof(AutoModeButtonText));
                OnPropertyChanged(nameof(CountdownDisplay));
            }
        }
    }

    private int _countdownSeconds;
    public int CountdownSeconds
    {
        get => _countdownSeconds;
        private set
        {
            if (SetProperty(ref _countdownSeconds, value))
                OnPropertyChanged(nameof(CountdownDisplay));
        }
    }

    /// <summary>Label text for the Auto toggle button.</summary>
    public string AutoModeButtonText => IsAutoMode ? "⏸ Auto" : "Auto ▷";

    /// <summary>Countdown shown while auto-mode is running; empty otherwise.</summary>
    public string CountdownDisplay => IsAutoMode ? $"{CountdownSeconds}s" : string.Empty;

    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand EndSessionCommand { get; }
    public ICommand ToggleAutoModeCommand { get; }

    public PrayerTimeViewModel()
    {
        _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
        _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
        _tagService = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();
        _interactionService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerInteractionService>();

        NextCommand = new AsyncRelayCommand(NextAsync);
        PreviousCommand = new RelayCommand(Previous);
        EndSessionCommand = new AsyncRelayCommand(EndSessionAsync);
        ToggleAutoModeCommand = new RelayCommand(ToggleAutoMode);
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
            // SetProperty(ref _currentIndex, 0) is a no-op when _currentIndex is already 0
            // (int default value). Bypass it and fire dependent notifications manually so
            // the XAML bindings on CurrentEntry update correctly after the first load.
            _currentIndex = 0;
            OnPropertyChanged(nameof(CurrentEntry));
            OnPropertyChanged(nameof(ProgressDisplay));
            OnPropertyChanged(nameof(HasPrevious));
            OnPropertyChanged(nameof(HasNext));
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
                await Shell.Current.DisplayAlertAsync("Error", "Unable to load prayers for this session.", "OK");
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
        {
            CurrentIndex++;
            // Reset countdown so user gets full interval on the new card
            if (IsAutoMode)
                CountdownSeconds = AutoIntervalSeconds;
        }
        else
        {
            HasCompleted = true;
            StopAutoMode();
        }
    }

    private void Previous()
    {
        if (HasPrevious)
            CurrentIndex--;
    }

    private async Task EndSessionAsync()
    {
        StopAutoMode();
        await Shell.Current.GoToAsync("..");
    }

    // ── Auto-mode ────────────────────────────────────────────────────────────

    private void ToggleAutoMode()
    {
        if (HasCompleted) return;

        if (IsAutoMode)
            StopAutoMode();
        else
            StartAutoMode();
    }

    private void StartAutoMode()
    {
        IsAutoMode = true;
        CountdownSeconds = AutoIntervalSeconds;

        _autoTimer = Application.Current!.Dispatcher.CreateTimer();
        _autoTimer.Interval = TimeSpan.FromSeconds(1);
        _autoTimer.Tick += OnAutoTimerTick;
        _autoTimer.Start();
    }

    /// <summary>
    /// Permanently stops auto-mode and disposes the timer.
    /// Called when the session ends, the user taps Stop, or all cards have been prayed.
    /// </summary>
    public void StopAutoMode()
    {
        if (_autoTimer is not null)
        {
            _autoTimer.Stop();
            _autoTimer.Tick -= OnAutoTimerTick;
            _autoTimer = null;
        }
        IsAutoMode = false;
        CountdownSeconds = 0;
    }

    /// <summary>
    /// Pauses the countdown without disabling auto-mode.
    /// Called when the app goes to background.
    /// </summary>
    public void PauseAutoMode()
    {
        _autoTimer?.Stop();
    }

    /// <summary>
    /// Resumes the countdown if auto-mode is still active.
    /// Called when the app returns to foreground.
    /// </summary>
    public void ResumeAutoMode()
    {
        if (IsAutoMode)
            _autoTimer?.Start();
    }

    private void OnAutoTimerTick(object? sender, EventArgs e)
    {
        CountdownSeconds--;
        if (CountdownSeconds <= 0)
            _ = NextAsync();
    }
}
