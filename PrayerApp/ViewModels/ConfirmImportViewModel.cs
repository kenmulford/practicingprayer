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

/// <summary>
/// Controls whether the VM is operating as a standard import flow or as
/// the Manual (Quick Add) entry point. Import is the default; Manual is
/// set by the caller before the page appears via
/// <see cref="ConfirmImportViewModel.InitializeManualEntry"/>.
/// </summary>
public enum EntryMode { Import, Manual }

public class CardPickerItem : ObservableObject
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

/// <summary>
/// Group of <see cref="CardPickerItem"/>s under a single collection name on
/// the Confirm Import page. Drives the nested BindableLayout in
/// ConfirmImportPage.xaml — collection header, card rows, divider per group.
/// Keyed on <see cref="BoxId"/> (not <see cref="CollectionName"/>): two CardBox
/// rows can share a display name (e.g. user-renamed legacy "Family" alongside
/// a new "Family") and we must not silently merge their card lists.
/// </summary>
public class CardCollectionGroup
{
    public int BoxId { get; init; }
    public string CollectionName { get; init; } = string.Empty;
    public ObservableCollection<CardPickerItem> Cards { get; init; } = new();
}

public sealed class ConfirmImportViewModel : ObservableObject, IDisposable
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IMessenger _messenger;
    private readonly IImportPayloadService _payloadService;
    private readonly ITextSelectionParser _parser;
    private readonly IBoxService _boxService;

    // Fallback group header for cards whose BoxId doesn't resolve to a known
    // CardBox — only reachable if data integrity drifted (a card pointing at
    // a deleted box that wasn't re-parented). BoxId 0 is seeded separately so
    // legitimate Loose Cards never land here.
    private const string UnknownCollectionName = "Unknown";

    private bool _consumed;
    private bool _payloadConsumed;
    private bool _boxesLoaded;
    private bool _boxesLoading;
    private bool _hasNoAvailableCardsCached;
    private CancellationTokenSource? _loadCardGroupsCts = new();

    private EntryMode _entryMode;
    /// <summary>
    /// Import (default) preserves the existing import flow unchanged.
    /// Manual activates the Quick Add path: no payload consumed, Quick Add
    /// card preselected, prayers saved with <c>IsImported = false</c>.
    /// Set this before <see cref="InitializeManualEntry"/> is called.
    /// </summary>
    public EntryMode EntryMode
    {
        get => _entryMode;
        private set => SetProperty(ref _entryMode, value);
    }

    public ObservableCollection<EditablePrayer> Prayers { get; } = new();

    /// <summary>
    /// Picker source for the Collection field on the Confirm Import page.
    /// In NewCard mode: "Loose Cards" (BoxId=0) at index 0 (default selection),
    /// user-created collections follow in BoxService order. In ExistingCard
    /// mode the "All collections" sentinel is inserted at index 0 and selected
    /// by default — the picker becomes an optional filter rather than a
    /// destination. System / Archived boxes are excluded — same pattern as
    /// the card edit form.
    /// </summary>
    public ObservableCollection<BoxPickerItem> AvailableBoxes { get; } = new();

    private BoxPickerItem? _selectedBox;
    public BoxPickerItem? SelectedBox
    {
        get => _selectedBox;
        set
        {
            if (SetProperty(ref _selectedBox, value))
            {
                // NewCard mode: picker drives the new card's BoxId; nothing
                // to reload. ExistingCard mode: picker is a filter — refresh
                // the grouped card list.
                if (IsExistingCardMode)
                    LoadCardGroupsAsync().SafeFireAndForget();
            }
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
                ApplyImportModeSideEffects();
                NotifySaveCanExecute();
                OnPropertyChanged(nameof(IsNewCardMode));
                OnPropertyChanged(nameof(IsExistingCardMode));
                RaiseHasNoAvailableCardsIfChanged();
            }
        }
    }

    /// <summary>
    /// Maintains picker state when ImportMode flips. ExistingCard adds the
    /// All-collections sentinel at index 0 and selects it (so the user sees
    /// every card across all collections by default — fits the small-library
    /// path). NewCard removes the sentinel and restores Loose Cards as the
    /// default — a NewCard save needs ONE BoxId, and Loose Cards mirrors the
    /// pre-mode-toggle baseline. Skips if LoadBoxesAsync hasn't run yet
    /// (constructor-time mode set in tests, e.g.).
    /// In Manual mode the ImportMode is set to ExistingCard by
    /// InitializeManualEntry before boxes are loaded; at that point the
    /// sentinel cannot be inserted yet. LoadManualCardGroupsAsync (called from
    /// OnAppearing after LoadBoxesAsync) inserts the sentinel and triggers the
    /// card-group load. The early-return for Manual mode here prevents a
    /// premature (pre-boxes) LoadCardGroupsAsync fire-and-forget that would
    /// race against the authoritative load in LoadManualCardGroupsAsync.
    /// </summary>
    private void ApplyImportModeSideEffects()
    {
        if (!_boxesLoaded)
        {
            // No picker contents to mutate yet. In Import mode, kick off an
            // early card-group load so the empty-state binding is accurate.
            // In Manual mode, suppress the early load — boxes aren't ready,
            // and LoadManualCardGroupsAsync is the authoritative first loader.
            if (IsExistingCardMode && EntryMode != EntryMode.Manual)
                LoadCardGroupsAsync().SafeFireAndForget();
            else
                AvailableCardGroups.Clear();
            return;
        }

        if (IsExistingCardMode)
        {
            if (!AvailableBoxes.Contains(AllCollectionsPickerItem.Instance))
                AvailableBoxes.Insert(0, AllCollectionsPickerItem.Instance);
            SelectedBox = AllCollectionsPickerItem.Instance;
        }
        else
        {
            AvailableBoxes.Remove(AllCollectionsPickerItem.Instance);
            // Guard: only reset when the sentinel was selected. Real box
            // selections survive the toggle — SaveAsync's RealBoxPickerItem
            // guard assigns BoxId correctly for any real box.
            if (SelectedBox is not RealBoxPickerItem)
            {
                var looseCards = AvailableBoxes
                    .OfType<RealBoxPickerItem>()
                    .FirstOrDefault(b => b.BoxId == 0);
                if (looseCards is not null)
                    SelectedBox = looseCards;
            }
            AvailableCardGroups.Clear();
        }
    }

    public bool IsNewCardMode => ImportMode == ImportMode.NewCard;
    public bool IsExistingCardMode => ImportMode == ImportMode.ExistingCard;

    /// <summary>
    /// True when the user is in Existing-Card mode AND every group is empty
    /// (or no groups exist). Drives an empty-state Label in the XAML so the
    /// user sees "No cards in this collection" instead of an empty area
    /// that looks like a broken list. Recomputes whenever ImportMode changes
    /// or AvailableCardGroups' contents change.
    /// </summary>
    public bool HasNoAvailableCards =>
        IsExistingCardMode && AvailableCardGroups.All(g => g.Cards.Count == 0);

    /// <summary>
    /// Grouped card list for Existing-Card mode — collection name + the
    /// non-system cards under that BoxId, sorted alphabetically within and
    /// across groups. Always mutated in place via Clear()/Add() in
    /// LoadCardGroupsAsync — do not reassign. HasNoAvailableCards notification
    /// is wired to this instance via CollectionChanged in the constructor.
    /// </summary>
    public ObservableCollection<CardCollectionGroup> AvailableCardGroups { get; } = new();

    private CardPickerItem? _selectedCard;
    public CardPickerItem? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (SetProperty(ref _selectedCard, value))
                NotifySaveCanExecute();
        }
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

        Prayers.CollectionChanged += (_, e) =>
        {
            // Subscribe to Title changes on rows entering the collection so
            // CanSave is re-evaluated when the user types (not only on Add/Remove).
            if (e.NewItems is not null)
                foreach (EditablePrayer row in e.NewItems)
                    row.PropertyChanged += OnPrayerPropertyChanged;

            // Unsubscribe from rows leaving the collection to prevent leaks.
            if (e.OldItems is not null)
                foreach (EditablePrayer row in e.OldItems)
                    row.PropertyChanged -= OnPrayerPropertyChanged;

            NotifySaveCanExecute();
            OnPropertyChanged(nameof(PrayersHeader));
        };

        // HasNoAvailableCards drives the empty-state Label in the XAML; it
        // depends on AvailableCardGroups, which changes inside
        // LoadCardGroupsAsync via Clear()+Add() — both fire CollectionChanged.
        // Cache + change-detection: a Clear()+N×Add() sequence is N+1
        // CollectionChanged events but at most one logical transition on this
        // bool. Don't spam PropertyChanged on every intermediate state.
        _hasNoAvailableCardsCached = HasNoAvailableCards;
        AvailableCardGroups.CollectionChanged += (_, _) => RaiseHasNoAvailableCardsIfChanged();
    }

    /// <summary>
    /// Single chokepoint for <see cref="HasNoAvailableCards"/> notification —
    /// fires <see cref="ObservableObject.OnPropertyChanged(string?)"/> only on a real
    /// transition. Called from the AvailableCardGroups CollectionChanged
    /// handler and from <see cref="ImportMode"/>'s setter.
    /// </summary>
    private void RaiseHasNoAvailableCardsIfChanged()
    {
        var current = HasNoAvailableCards;
        if (current == _hasNoAvailableCardsCached) return;
        _hasNoAvailableCardsCached = current;
        OnPropertyChanged(nameof(HasNoAvailableCards));
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
    /// Prepares the VM for Manual (Quick Add) entry mode. Call once before
    /// the page appears; idempotent by design.
    /// Sets <see cref="EntryMode"/> to <see cref="EntryMode.Manual"/>,
    /// switches to ExistingCard mode (Quick Add card will be preselected by
    /// <see cref="LoadManualCardGroupsAsync"/>), and seeds exactly one empty
    /// prayer row. Does NOT consume any staged import payload.
    /// </summary>
    public void InitializeManualEntry()
    {
        EntryMode = EntryMode.Manual;
        // Mark both payload guards so ConsumePending() and DrainIfNotConsumed()
        // are no-ops — there is no staged import payload in manual mode.
        _consumed = true;
        _payloadConsumed = true;
        // ExistingCard mode so the Quick Add card picker is shown and the
        // page's OnAppearing collapse-to-summary logic fires when SelectedCard
        // is set by LoadManualCardGroupsAsync.
        ImportMode = ImportMode.ExistingCard;
        // Seed exactly one empty row for the zero-tap fast path.
        if (Prayers.Count == 0)
            Prayers.Add(new EditablePrayer());
    }

    /// <summary>
    /// For Manual mode: ensures the All-collections sentinel is present and
    /// selected (the window was missed during <see cref="InitializeManualEntry"/>
    /// because boxes weren't loaded yet), then delegates to
    /// <see cref="LoadCardGroupsAsync"/> which includes the Quick Add system
    /// card and preselects it when <see cref="EntryMode"/> is Manual.
    /// Call after <see cref="LoadBoxesAsync"/> so the box picker is ready.
    /// No-op if <see cref="EntryMode"/> is not Manual.
    /// </summary>
    public async Task LoadManualCardGroupsAsync()
    {
        if (EntryMode != EntryMode.Manual) return;

        // Apply the All-collections sentinel that ApplyImportModeSideEffects
        // would have set, but couldn't because _boxesLoaded was false when
        // InitializeManualEntry set ImportMode = ExistingCard.
        if (!AvailableBoxes.Contains(AllCollectionsPickerItem.Instance))
            AvailableBoxes.Insert(0, AllCollectionsPickerItem.Instance);
        // Set SelectedBox via the backing field directly so the setter's
        // LoadCardGroupsAsync fire-and-forget doesn't race the explicit await below.
        _selectedBox = AllCollectionsPickerItem.Instance;
        OnPropertyChanged(nameof(SelectedBox));

        // LoadCardGroupsAsync handles Manual mode: includes Quick Add and
        // preselects it when SelectedCard is null (first manual load).
        await LoadCardGroupsAsync();
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
        // _boxesLoaded only flips AFTER population — otherwise a future
        // refactor that adds an early `await` between flag-set and Add()
        // would silently leave AvailableBoxes empty for a re-entrant caller
        // who short-circuited on the flag. _boxesLoading covers the in-flight
        // window so a re-entrant OnAppearing (e.g. resume from background)
        // doesn't double-issue GetBoxesAsync.
        if (_boxesLoaded || _boxesLoading) return;
        _boxesLoading = true;
        try
        {
            var items = await _boxService.GetBoxPickerItemsAsync();

            foreach (var item in items)
                AvailableBoxes.Add(item);

            SelectedBox = items[0];
            _boxesLoaded = true;
        }
        finally
        {
            _boxesLoading = false;
        }
    }

    /// <summary>
    /// Builds the grouped card list for Existing-Card mode. Two filter modes
    /// driven by SelectedBox:
    ///   • All-collections sentinel (default in ExistingCard mode): every
    ///     non-system card (plus the Quick Add system card in Manual mode),
    ///     partitioned by collection — supports the small-library happy path
    ///     where the user picks any card without first knowing its collection.
    ///   • Specific BoxId: narrowed to that one collection — the 50+ card
    ///     filtered path, single group visible.
    /// Loose Cards (BoxId 0) gets a synthesized name so cards there don't
    /// fall through to "Unknown" when no CardBox row exists for it.
    /// In Manual mode: the Quick Add system card is always included (it is
    /// the default destination) and is preselected when SelectedCard is null.
    /// </summary>
    private async Task LoadCardGroupsAsync()
    {
        _loadCardGroupsCts?.Cancel();
        _loadCardGroupsCts?.Dispose();
        _loadCardGroupsCts = new CancellationTokenSource();
        var token = _loadCardGroupsCts.Token;

        // Manual mode: fetch the Quick Add system card so it can be included
        // in the card list and preselected as the default destination.
        PrayerCard? quickAddCard = null;
        if (EntryMode == EntryMode.Manual)
        {
            quickAddCard = await _cardService.GetOrCreateQuickAddCardAsync();
            if (token.IsCancellationRequested) return;
        }

        var allCards = await _cardService.GetCardsAsync();
        if (token.IsCancellationRequested) return;
        var allBoxes = await _boxService.GetBoxesAsync();
        if (token.IsCancellationRequested) return;

        // BoxId 0 is the "loose cards" sentinel — there is no CardBox row
        // for it, so seed the lookup so the group header shows the proper
        // label rather than "Unknown". For data-drift duplicates (two
        // CardBox rows with the same Id is impossible — DB PK; same NAME is
        // possible), ToDictionary is keyed by Id, which is what we want.
        var boxNames = allBoxes.ToDictionary(b => b.Id, b => b.Name);
        boxNames[0] = BoxStrings.Unorganized;

        // Import mode: exclude system cards entirely.
        // Manual mode: include the Quick Add system card alongside user cards.
        IEnumerable<PrayerCard> filtered = EntryMode == EntryMode.Manual
            ? allCards.Where(c => !c.IsSystem || c.Id == quickAddCard!.Id)
            : allCards.Where(c => !c.IsSystem);

        if (SelectedBox is RealBoxPickerItem real)
            filtered = filtered.Where(c => c.BoxId == real.BoxId);

        // GroupBy BoxId (not name): two CardBox rows with the same display
        // name would otherwise silently merge their card lists. The group's
        // BoxId field carries the partition key forward for any UI/test code
        // that needs to disambiguate same-named groups.
        var groups = filtered
            .GroupBy(c => c.BoxId)
            .Select(g => new CardCollectionGroup
            {
                BoxId = g.Key,
                CollectionName = boxNames.TryGetValue(g.Key, out var name) ? name : UnknownCollectionName,
                Cards = new ObservableCollection<CardPickerItem>(
                    g.OrderBy(c => c.Title)
                     .Select(c => new CardPickerItem { CardId = c.Id, Title = c.Title }))
            })
            .OrderBy(g => g.CollectionName)
            .ToList();

        AvailableCardGroups.Clear();
        foreach (var grp in groups)
            AvailableCardGroups.Add(grp);

        // Manual mode: preselect the Quick Add card when no card is already
        // selected (first load). Re-filter (box change) preserves the
        // existing selection if Quick Add is still in the filtered set, or
        // clears it if Quick Add was filtered out by a collection change.
        if (EntryMode == EntryMode.Manual && quickAddCard is not null)
        {
            var qaItem = groups
                .SelectMany(g => g.Cards)
                .FirstOrDefault(c => c.CardId == quickAddCard.Id);

            if (SelectedCard is null && qaItem is not null)
            {
                SelectedCard = qaItem;
                qaItem.IsSelected = true;
            }
            else if (SelectedCard is not null && groups.SelectMany(g => g.Cards)
                         .All(c => c.CardId != SelectedCard.CardId))
            {
                // Selected card no longer visible after filter change — clear.
                if (SelectedCard is not null) SelectedCard.IsSelected = false;
                SelectedCard = null;
            }
        }
        else if (EntryMode != EntryMode.Manual)
        {
            SelectedCard = null;
        }
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
        // ConsumePending already drained both channels by save time;
        // skip the OnDisappearing safety-net.
        _payloadConsumed = true;
        try
        {
            // Manual mode: prayers are user-authored, not imported from an
            // external source. IsImported = false preserves the prior QuickAdd
            // save semantics. Import mode keeps IsImported = true.
            var isImported = EntryMode != EntryMode.Manual;

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
                        IsImported = isImported,
                        CanNotify = false
                    };
                    await _prayerService.SavePrayerAsync(prayer, publishMessage: false);
                    existingSavedCount++;
                }
                _messenger.Send(new BulkChangedMessage());
                var existingAnnounce = isImported
                    ? $"Imported {existingSavedCount} prayers to {SelectedCard.Title}"
                    : $"Saved {existingSavedCount} {(existingSavedCount == 1 ? "prayer" : "prayers")} to {SelectedCard.Title}";
                _accessibilityService.Announce(existingAnnounce);
                await _navigationService.GoToAsync(Routes.PrayerCardsTabImportedToExisting(SelectedCard.CardId));
                return;
            }

            var card = new PrayerCard
            {
                Title = NormalizeQuotes(CardTitle)?.Trim() ?? string.Empty,
                // Only RealBoxPickerItem carries a BoxId; null and the
                // All-collections sentinel both fall through to BoxId 0
                // (Loose Cards) — the safe NewCard default.
                BoxId = SelectedBox is RealBoxPickerItem real ? real.BoxId : 0,
                IsImported = isImported
            };
            await _cardService.SaveCardAsync(card, publishMessage: false);

            var savedCount = 0;
            foreach (var row in Prayers.Where(r => !string.IsNullOrWhiteSpace(r.Title)))
            {
                var prayer = new Prayer
                {
                    PrayerCardId = card.Id,
                    Title = NormalizeQuotes(row.Title)?.Trim() ?? string.Empty,
                    Details = string.IsNullOrWhiteSpace(row.Details) ? null : NormalizeQuotes(row.Details)!.Trim(),
                    IsImported = isImported,
                    CanNotify = false
                };
                await _prayerService.SavePrayerAsync(prayer, publishMessage: false);
                savedCount++;
            }

            _messenger.Send(new BulkChangedMessage());
            var announce = isImported
                ? $"Imported {savedCount} prayers to {card.Title}"
                : $"Saved {savedCount} {(savedCount == 1 ? "prayer" : "prayers")} to {card.Title}";
            _accessibilityService.Announce(announce);
            await _navigationService.GoToAsync(Routes.PrayerCardsTabImported(card.Id));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        // Cancel before OnAppearing fired ConsumePending would otherwise
        // leave a staged payload to auto-re-import on the next launch.
        DrainIfNotConsumed();
        await _navigationService.PopModalAsync();
    }

    /// <summary>
    /// Idempotently drains both staged-payload channels so a payload cannot
    /// auto-re-import on the next app launch. Wired from
    /// <see cref="Views.ConfirmImportPage.OnDisappearing"/> as the swipe-dismiss
    /// safety net — iOS PageSheet modals can be swiped down without firing
    /// CancelCommand, so SaveAsync / CancelAsync alone don't cover the gap.
    /// First call drains; subsequent calls (and calls after Save/Cancel) are
    /// no-ops via the <see cref="_payloadConsumed"/> guard.
    /// </summary>
    public void DrainIfNotConsumed()
    {
        if (_payloadConsumed) return;
        _payloadConsumed = true;
        _payloadService.ConsumePayload();
        _payloadService.ConsumeStructured();
    }

    private void OnPrayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditablePrayer.Title))
            NotifySaveCanExecute();
    }

    private void NotifySaveCanExecute() => SaveCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Idempotent — disposes the in-flight CancellationTokenSource so the
    /// final instance doesn't leak when the modal closes. Wired from
    /// <see cref="Views.ConfirmImportPage.OnDisappearing"/>. Subsequent calls
    /// are no-ops; the field is nulled so a second Dispose() can't hit a
    /// disposed CTS.
    /// </summary>
    public void Dispose()
    {
        _loadCardGroupsCts?.Dispose();
        _loadCardGroupsCts = null;
    }
}
