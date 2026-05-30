using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.ViewModels;

public class QuickAddViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly ISettings _settings;
    private readonly IBoxService _boxService;

    private bool _destinationLoaded;
    private int _quickAddCardId;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _cardTitle = string.Empty;
    public string CardTitle
    {
        get => _cardTitle;
        set => SetProperty(ref _cardTitle, value);
    }

    private bool _showTip;
    public bool ShowTip
    {
        get => _showTip;
        private set => SetProperty(ref _showTip, value);
    }

    private ImportMode _destinationMode = ImportMode.ExistingCard;
    public ImportMode DestinationMode
    {
        get => _destinationMode;
        set
        {
            if (SetProperty(ref _destinationMode, value))
            {
                OnPropertyChanged(nameof(IsNewCardMode));
                OnPropertyChanged(nameof(IsExistingCardMode));
            }
        }
    }

    public bool IsNewCardMode => DestinationMode == ImportMode.NewCard;
    public bool IsExistingCardMode => DestinationMode == ImportMode.ExistingCard;

    public ObservableCollection<CardCollectionGroup> AvailableCardGroups { get; } = new();

    private CardPickerItem? _selectedCard;
    public CardPickerItem? SelectedCard
    {
        get => _selectedCard;
        private set => SetProperty(ref _selectedCard, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DismissTipCommand { get; }
    public ICommand SetNewCardModeCommand { get; }
    public ICommand SetExistingCardModeCommand { get; }
    public IRelayCommand<CardPickerItem> SelectCardCommand { get; }

    public QuickAddViewModel(
        ICardService cardService,
        IPrayerService prayerService,
        INavigationService navigationService,
        IAccessibilityService accessibilityService,
        ISettings settings,
        IBoxService boxService)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _settings = settings;
        _boxService = boxService;
        _showTip = !settings.QuickAddTipDismissed;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        DismissTipCommand = new RelayCommand(DismissTip);
        SetNewCardModeCommand = new RelayCommand(() => DestinationMode = ImportMode.NewCard);
        SetExistingCardModeCommand = new RelayCommand(() => DestinationMode = ImportMode.ExistingCard);
        SelectCardCommand = new RelayCommand<CardPickerItem>(item =>
        {
            if (item is null) return;
            if (SelectedCard is not null) SelectedCard.IsSelected = false;
            SelectedCard = item;
            item.IsSelected = true;
        });
    }

    public QuickAddViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>())
    { }

    /// <summary>
    /// Loads destination cards for Existing mode. Defaults to the Quick Add
    /// system card so the zero-tap fast path is preserved.
    /// </summary>
    public async Task LoadDestinationAsync()
    {
        if (_destinationLoaded) return;

        var quickAdd = await _cardService.GetOrCreateQuickAddCardAsync();
        _quickAddCardId = quickAdd.Id;

        var allCards = await _cardService.GetCardsAsync();
        var allBoxes = await _boxService.GetBoxesAsync();
        var boxNames = allBoxes.ToDictionary(b => b.Id, b => b.Name);
        boxNames[0] = BoxStrings.Unorganized;

        var pickerCards = allCards
            .Where(c => !c.IsSystem || c.Id == quickAdd.Id)
            .OrderBy(c => c.Title)
            .ToList();

        AvailableCardGroups.Clear();
        foreach (var group in pickerCards.GroupBy(c => c.BoxId).OrderBy(g => boxNames.GetValueOrDefault(g.Key, string.Empty)))
        {
            AvailableCardGroups.Add(new CardCollectionGroup
            {
                BoxId = group.Key,
                CollectionName = boxNames.GetValueOrDefault(group.Key, "Unknown"),
                Cards = new ObservableCollection<CardPickerItem>(
                    group.Select(c => new CardPickerItem { CardId = c.Id, Title = c.Title })),
            });
        }

        var defaultItem = AvailableCardGroups
            .SelectMany(g => g.Cards)
            .FirstOrDefault(c => c.CardId == quickAdd.Id);
        if (defaultItem is not null)
            SelectCardCommand.Execute(defaultItem);

        _destinationLoaded = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await _navigationService.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
            return;
        }

        if (IsNewCardMode && string.IsNullOrWhiteSpace(CardTitle))
        {
            await _navigationService.DisplayAlertAsync("Required", "Please enter a card title.", "OK");
            return;
        }

        try
        {
            var targetCardId = await ResolveTargetCardIdAsync();
            var prayer = new Prayer
            {
                Title = Title.Trim(),
                PrayerCardId = targetCardId,
            };
            await _prayerService.SavePrayerAsync(prayer);
            _prayerService.InvalidateCache();
            _accessibilityService.Announce("Prayer saved");
            await _navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Quick add save failed: {ex.Message}");
            await _navigationService.DisplayAlertAsync("Error", "Unable to save prayer. Please try again.", "OK");
        }
    }

    private async Task<int> ResolveTargetCardIdAsync()
    {
        if (IsExistingCardMode)
        {
            if (SelectedCard is not null)
                return SelectedCard.CardId;

            if (_quickAddCardId != 0)
                return _quickAddCardId;

            return (await _cardService.GetOrCreateQuickAddCardAsync()).Id;
        }

        var card = new PrayerCard
        {
            Title = CardTitle.Trim(),
            BoxId = 0,
        };
        var saved = await _cardService.SaveCardAsync(card);
        return saved.Id;
    }

    private void DismissTip()
    {
        _settings.QuickAddTipDismissed = true;
        ShowTip = false;
    }

    private async Task CancelAsync()
    {
        await _navigationService.PopModalAsync();
    }
}
