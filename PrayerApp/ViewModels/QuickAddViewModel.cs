using System.Collections.ObjectModel;
using System.Linq;
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

    private bool _initialized;
    private bool _boxesLoaded;
    private bool _boxesLoading;
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
        set
        {
            if (SetProperty(ref _cardTitle, value ?? string.Empty))
                SaveCommand.NotifyCanExecuteChanged();
        }
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
                ApplyDestinationModeSideEffects();
                OnPropertyChanged(nameof(IsNewCardMode));
                OnPropertyChanged(nameof(IsExistingCardMode));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNewCardMode => DestinationMode == ImportMode.NewCard;
    public bool IsExistingCardMode => DestinationMode == ImportMode.ExistingCard;

    public ObservableCollection<BoxPickerItem> AvailableBoxes { get; } = new();
    public ObservableCollection<CardCollectionGroup> AvailableCardGroups { get; } = new();

    private BoxPickerItem? _selectedBox;
    public BoxPickerItem? SelectedBox
    {
        get => _selectedBox;
        set => SetProperty(ref _selectedBox, value);
    }

    private CardPickerItem? _selectedCard;
    public CardPickerItem? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (SetProperty(ref _selectedCard, value))
            {
                if (value is not null)
                    _showCardPickerExpanded = false;
                OnPropertyChanged(nameof(ShowCardPickerList));
                OnPropertyChanged(nameof(ShowSelectedCardSummary));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _showCardPickerExpanded;
    public bool ShowCardPickerList => IsExistingCardMode && _showCardPickerExpanded;
    public bool ShowSelectedCardSummary => IsExistingCardMode && !_showCardPickerExpanded && SelectedCard is not null;

    public IAsyncRelayCommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DismissTipCommand { get; }
    public ICommand SetNewCardModeCommand { get; }
    public ICommand SetExistingCardModeCommand { get; }
    public ICommand SelectCardCommand { get; }
    public ICommand ShowCardPickerCommand { get; }

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

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        DismissTipCommand = new RelayCommand(DismissTip);
        SetNewCardModeCommand = new RelayCommand(() => DestinationMode = ImportMode.NewCard);
        SetExistingCardModeCommand = new RelayCommand(() => DestinationMode = ImportMode.ExistingCard);
        SelectCardCommand = new RelayCommand<CardPickerItem>(SelectCard);
        ShowCardPickerCommand = new RelayCommand(ShowCardPicker);
    }

    public QuickAddViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IBoxService>())
    { }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        var quickAdd = await _cardService.GetOrCreateQuickAddCardAsync();
        _quickAddCardId = quickAdd.Id;
        await LoadBoxesAsync();
        await LoadCardGroupsAsync();
        SelectQuickAddCardDefault();
        _initialized = true;
    }

    private void ApplyDestinationModeSideEffects()
    {
        _showCardPickerExpanded = false;
        OnPropertyChanged(nameof(ShowCardPickerList));
        OnPropertyChanged(nameof(ShowSelectedCardSummary));

        if (IsExistingCardMode)
        {
            LoadCardGroupsAsync().SafeFireAndForget();
            SelectQuickAddCardDefault();
        }
        else
        {
            AvailableCardGroups.Clear();
            if (SelectedCard is not null)
            {
                SelectedCard.IsSelected = false;
                SelectedCard = null;
            }
            if (_boxesLoaded && SelectedBox is not RealBoxPickerItem loose)
            {
                var looseCards = AvailableBoxes.OfType<RealBoxPickerItem>().FirstOrDefault(b => b.BoxId == 0);
                if (looseCards is not null)
                    SelectedBox = looseCards;
            }
        }
    }

    private void ShowCardPicker()
    {
        _showCardPickerExpanded = true;
        OnPropertyChanged(nameof(ShowCardPickerList));
        OnPropertyChanged(nameof(ShowSelectedCardSummary));
    }

    private void SelectCard(CardPickerItem? item)
    {
        if (item is null) return;
        if (SelectedCard is not null)
            SelectedCard.IsSelected = false;
        SelectedCard = item;
        item.IsSelected = true;
    }

    private void SelectQuickAddCardDefault()
    {
        if (!IsExistingCardMode || _quickAddCardId == 0) return;
        foreach (var group in AvailableCardGroups)
        {
            var match = group.Cards.FirstOrDefault(c => c.CardId == _quickAddCardId);
            if (match is not null)
            {
                SelectCard(match);
                return;
            }
        }
    }

    public async Task LoadBoxesAsync()
    {
        if (_boxesLoaded || _boxesLoading) return;
        _boxesLoading = true;
        try
        {
            var boxes = await _boxService.GetBoxesAsync();
            var looseCards = new RealBoxPickerItem(0, BoxStrings.Unorganized);
            AvailableBoxes.Add(looseCards);
            foreach (var box in boxes.Where(b => !b.IsSystem))
                AvailableBoxes.Add(new RealBoxPickerItem(box.Id, box.Name));

            SelectedBox = looseCards;
            _boxesLoaded = true;
        }
        finally
        {
            _boxesLoading = false;
        }
    }

    private async Task LoadCardGroupsAsync()
    {
        var allCards = await _cardService.GetCardsAsync();
        var allBoxes = await _boxService.GetBoxesAsync();
        var boxNames = allBoxes.ToDictionary(b => b.Id, b => b.Name);
        boxNames[0] = BoxStrings.Unorganized;

        var filtered = allCards.Where(c =>
            !c.IsSystem || c.SystemKey == PrayerCard.SystemKeyQuickAdd);

        var groups = filtered
            .GroupBy(c => c.BoxId)
            .Select(g => new CardCollectionGroup
            {
                BoxId = g.Key,
                CollectionName = boxNames.TryGetValue(g.Key, out var name) ? name : "Unknown",
                Cards = new ObservableCollection<CardPickerItem>(
                    g.OrderBy(c => c.Title)
                        .Select(c => new CardPickerItem { CardId = c.Id, Title = c.Title }))
            })
            .OrderBy(g => g.CollectionName)
            .ToList();

        AvailableCardGroups.Clear();
        foreach (var grp in groups)
            AvailableCardGroups.Add(grp);
    }

    private bool CanSave() =>
        IsNewCardMode
            ? !string.IsNullOrWhiteSpace(CardTitle)
            : SelectedCard is not null;

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await _navigationService.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
            return;
        }

        try
        {
            int cardId;
            if (IsExistingCardMode)
            {
                if (SelectedCard is null) return;
                cardId = SelectedCard.CardId;
            }
            else
            {
                var card = new PrayerCard
                {
                    Title = CardTitle.Trim(),
                    BoxId = SelectedBox is RealBoxPickerItem real ? real.BoxId : 0,
                };
                await _cardService.SaveCardAsync(card);
                cardId = card.Id;
            }

            var prayer = new Prayer
            {
                Title = Title.Trim(),
                PrayerCardId = cardId,
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
