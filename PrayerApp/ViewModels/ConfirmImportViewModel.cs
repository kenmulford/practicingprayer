using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrayerApp.Helpers;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using static PrayerApp.Helpers.TextNormalization;

namespace PrayerApp.ViewModels;

public enum ImportMode { NewCard, ExistingCard }

public class CardPickerItem : ObservableObject
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public class ConfirmImportViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IMessenger _messenger;
    private readonly IImportPayloadService _payloadService;
    private readonly ITextSelectionParser _parser;
    private readonly IBoxService _boxService;

    private bool _consumed;
    private bool _boxesLoaded;
    private CancellationTokenSource _loadCardsCts = new();

    public ObservableCollection<EditablePrayer> Prayers { get; } = new();

    /// <summary>
    /// Picker source for the Collection field on the Confirm Import page.
    /// "Loose Cards" (BoxId=0) is always first and is the default selection;
    /// user-created collections follow in BoxService order. System / Archived
    /// boxes are excluded — same pattern as the card edit form.
    /// </summary>
    public ObservableCollection<BoxPickerItem> AvailableBoxes { get; } = new();

    private BoxPickerItem? _selectedBox;
    public BoxPickerItem? SelectedBox
    {
        get => _selectedBox;
        set
        {
            if (SetProperty(ref _selectedBox, value))
                LoadCardsForBoxAsync().SafeFireAndForget();
        }
    }

    private ImportMode _importMode;
    public ImportMode ImportMode
    {
        get => _importMode;
        set
        {
            if (SetProperty(ref _importMode, value))
            {
                NotifySaveCanExecute();
                OnPropertyChanged(nameof(IsNewCardMode));
                OnPropertyChanged(nameof(IsExistingCardMode));
            }
        }
    }

    public bool IsNewCardMode => ImportMode == ImportMode.NewCard;
    public bool IsExistingCardMode => ImportMode == ImportMode.ExistingCard;

    public ObservableCollection<CardPickerItem> AvailableCards { get; } = new();

    private CardPickerItem? _selectedCard;
    public CardPickerItem? SelectedCard
    {
        get => _selectedCard;
        set { SetProperty(ref _selectedCard, value); NotifySaveCanExecute(); }
    }

    public string PrayersHeader => $"Prayers ({Prayers.Count})";

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
    public ICommand SetNewCardModeCommand { get; }
    public ICommand SetExistingCardModeCommand { get; }
    public IRelayCommand<CardPickerItem> SelectCardCommand { get; }

    public ConfirmImportViewModel(
        ICardService cardService,
        IPrayerService prayerService,
        INavigationService navigationService,
        IAccessibilityService accessibilityService,
        IMessenger messenger,
        IImportPayloadService payloadService,
        ITextSelectionParser parser,
        IBoxService boxService)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _messenger = messenger;
        _payloadService = payloadService;
        _parser = parser;
        _boxService = boxService;

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        AddPrayerCommand = new RelayCommand(() => Prayers.Add(new EditablePrayer()));
        RemovePrayerCommand = new RelayCommand<EditablePrayer>(row =>
        {
            if (row is null) return;
            Prayers.Remove(row);
        });
        SetNewCardModeCommand = new RelayCommand(() => ImportMode = ImportMode.NewCard);
        SetExistingCardModeCommand = new RelayCommand(() => ImportMode = ImportMode.ExistingCard);
        SelectCardCommand = new RelayCommand<CardPickerItem>(item =>
        {
            if (item is null) return;
            if (SelectedCard is not null) SelectedCard.IsSelected = false;
            SelectedCard = item;
            item.IsSelected = true;
        });

        Prayers.CollectionChanged += (_, _) =>
        {
            NotifySaveCanExecute();
            OnPropertyChanged(nameof(PrayersHeader));
        };
    }

    public ConfirmImportViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IMessenger>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IImportPayloadService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ITextSelectionParser>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>())
    { }

    public void ConsumePending()
    {
        if (_consumed) return;
        _consumed = true;

        // Structured channel (deep-link / .prayercard) wins: payload is
        // already authoritative and re-parsing as text would mangle clauses.
        var result = _payloadService.ConsumeStructured();
        if (result is null)
        {
            var raw = _payloadService.ConsumePayload();
            if (string.IsNullOrEmpty(raw)) return;
            result = _parser.Parse(raw);
        }

        CardTitle = result.SuggestedCardTitle;
        foreach (var p in result.Prayers)
            Prayers.Add(new EditablePrayer { Title = p.Title, Details = p.Details });
    }

    /// <summary>
    /// Loads the Collection picker. "Loose Cards" (BoxId=0) is always first
    /// and is the default selection — matches the prior hardcoded behavior
    /// (a freshly imported card with no BoxId set lands in Loose Cards).
    /// User-created collections follow in BoxService order; system / archived
    /// boxes are excluded (mirrors PrayerCardViewModel.LoadBoxPickerAsync).
    /// Idempotent — the modal's OnAppearing fires on initial show and on
    /// resume from background; reloading would clobber a user's mid-flow
    /// selection.
    /// </summary>
    public async Task LoadBoxesAsync()
    {
        if (_boxesLoaded) return;
        _boxesLoaded = true;

        var boxes = await _boxService.GetBoxesAsync();

        var looseCards = new BoxPickerItem(0, BoxStrings.Unorganized);
        AvailableBoxes.Add(looseCards);

        foreach (var box in boxes.Where(b => !b.IsSystem))
            AvailableBoxes.Add(new BoxPickerItem(box.Id, box.Name));

        SelectedBox = looseCards;
    }

    private async Task LoadCardsForBoxAsync()
    {
        _loadCardsCts.Cancel();
        _loadCardsCts.Dispose();
        _loadCardsCts = new CancellationTokenSource();
        var token = _loadCardsCts.Token;

        var all = await _cardService.GetCardsAsync();
        if (token.IsCancellationRequested) return;

        var boxId = SelectedBox?.BoxId ?? 0;
        var filtered = all
            .Where(c => c.BoxId == boxId && !c.IsSystem)
            .OrderBy(c => c.Title);

        AvailableCards.Clear();
        foreach (var c in filtered)
            AvailableCards.Add(new CardPickerItem { CardId = c.Id, Title = c.Title });
        SelectedCard = null;
    }

    private bool CanSave()
        => !IsBusy
           && Prayers.Any(p => !string.IsNullOrWhiteSpace(p.Title))
           && (ImportMode == ImportMode.NewCard
                   ? !string.IsNullOrWhiteSpace(CardTitle)
                   : SelectedCard is not null);

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (ImportMode == ImportMode.ExistingCard && SelectedCard is not null)
            {
                var existingSavedCount = 0;
                foreach (var row in Prayers.Where(r => !string.IsNullOrWhiteSpace(r.Title)))
                {
                    var prayer = new Prayer
                    {
                        PrayerCardId = SelectedCard.CardId,
                        Title = NormalizeQuotes(row.Title)?.Trim() ?? string.Empty,
                        Details = string.IsNullOrWhiteSpace(row.Details) ? null : NormalizeQuotes(row.Details)!.Trim(),
                        IsImported = true,
                        CanNotify = false
                    };
                    await prayer.SaveAsync();
                    existingSavedCount++;
                }
                _cardService.InvalidateCache();
                _prayerService.InvalidateCache();
                _messenger.Send(new BulkChangedMessage());
                _accessibilityService.Announce($"Imported {existingSavedCount} prayers to {SelectedCard.Title}");
                await _navigationService.GoToAsync(Routes.PrayerCardsTabImportedToExisting(SelectedCard.CardId));
                return;
            }

            var card = new PrayerCard
            {
                Title = NormalizeQuotes(CardTitle)?.Trim() ?? string.Empty,
                BoxId = SelectedBox?.BoxId ?? 0,
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
        // Drain both channels in case the user dismissed before OnAppearing
        // fired ConsumePending — a stale payload (raw or structured) could
        // otherwise surface on the next launch.
        _payloadService.ConsumePayload();
        _payloadService.ConsumeStructured();
        await _navigationService.PopModalAsync();
    }

    private void NotifySaveCanExecute() => SaveCommand.NotifyCanExecuteChanged();
}
