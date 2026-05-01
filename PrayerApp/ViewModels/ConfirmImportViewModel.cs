using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using static PrayerApp.Helpers.TextNormalization;

namespace PrayerApp.ViewModels;

public class ConfirmImportViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IMessenger _messenger;
    private readonly IImportPayloadService _payloadService;
    private readonly ITextSelectionParser _parser;

    private bool _consumed;

    public ObservableCollection<EditablePrayer> Prayers { get; } = new();

    private string _cardTitle = string.Empty;
    public string CardTitle
    {
        get => _cardTitle;
        set
        {
            if (SetProperty(ref _cardTitle, value ?? string.Empty))
                NotifySaveCanExecute();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                NotifySaveCanExecute();
        }
    }

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }
    public ICommand AddPrayerCommand { get; }
    public ICommand RemovePrayerCommand { get; }

    public ConfirmImportViewModel(
        ICardService cardService,
        IPrayerService prayerService,
        INavigationService navigationService,
        IAccessibilityService accessibilityService,
        IMessenger messenger,
        IImportPayloadService payloadService,
        ITextSelectionParser parser)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _messenger = messenger;
        _payloadService = payloadService;
        _parser = parser;

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        AddPrayerCommand = new RelayCommand(() => Prayers.Add(new EditablePrayer()));
        RemovePrayerCommand = new RelayCommand<EditablePrayer>(row =>
        {
            if (row is null) return;
            Prayers.Remove(row);
        });

        Prayers.CollectionChanged += (_, _) => NotifySaveCanExecute();
    }

    public ConfirmImportViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IMessenger>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IImportPayloadService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ITextSelectionParser>())
    { }

    public void ConsumePending()
    {
        if (_consumed) return;
        _consumed = true;

        var raw = _payloadService.ConsumePayload();
        if (string.IsNullOrEmpty(raw)) return;

        var result = _parser.Parse(raw);
        CardTitle = result.SuggestedCardTitle;
        foreach (var p in result.Prayers)
            Prayers.Add(new EditablePrayer { Title = p.Title, Details = p.Details });
    }

    private bool CanSave()
        => !IsBusy
           && !string.IsNullOrWhiteSpace(CardTitle)
           && Prayers.Count > 0;

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var card = new PrayerCard
            {
                Title = NormalizeQuotes(CardTitle)?.Trim() ?? string.Empty,
                IsImported = true
            };
            await card.SaveAsync();

            var savedCount = 0;
            foreach (var row in Prayers.Where(r => !string.IsNullOrWhiteSpace(r.Title)))
            {
                var prayer = new Prayer
                {
                    PrayerCardId = card.Id,
                    Title = NormalizeQuotes(row.Title)?.Trim() ?? string.Empty,
                    Details = string.IsNullOrWhiteSpace(row.Details) ? null : NormalizeQuotes(row.Details)!.Trim(),
                    IsImported = true,
                    CanNotify = false
                };
                await prayer.SaveAsync();
                savedCount++;
            }

            _cardService.InvalidateCache();
            _prayerService.InvalidateCache();
            _messenger.Send(new BulkChangedMessage());
            _accessibilityService.Announce($"Imported {savedCount} prayers to {card.Title}");
            await _navigationService.GoToAsync(Routes.PrayerCardsTabImported(card.Id));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        // Drain in case the user dismissed before OnAppearing fired ConsumePending,
        // otherwise a stale payload could surface on the next launch.
        _payloadService.ConsumePayload();
        await _navigationService.PopModalAsync();
    }

    private void NotifySaveCanExecute() => SaveCommand.NotifyCanExecuteChanged();
}
