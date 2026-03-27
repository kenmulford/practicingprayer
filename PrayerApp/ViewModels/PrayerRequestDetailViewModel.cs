using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerRequestDetailViewModel : ObservableObject, IQueryAttributable, IEditGuard
    {
        private Prayer _prayer;
        private readonly IPrayerService _prayerService;
        private readonly ITagService _tagService;
        private readonly ICardService _cardService;
        private readonly IOnboardingService _onboardingService;
        private readonly INotificationService _notificationService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private readonly ISettings _settings;
        private List<PrayerTag> _allTags = new();

        public ICommand SaveCommand { get; private set; }
        public ICommand SaveAndNewCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }
        public ICommand EditPrayerCommand { get; private set; }
        public ICommand MarkAnsweredCommand { get; private set; }
        public ICommand ShareCommand { get; private set; }
        public ICommand AddSuggestedTagCommand { get; private set; }
        public ICommand SubmitTagEntryCommand { get; private set; }

        public ObservableCollection<TagChipViewModel> SelectedTags { get; } = new();
        public ObservableCollection<PrayerTag> SuggestedTags { get; } = new();

        private string _tagSearchText = string.Empty;
        public string TagSearchText
        {
            get => _tagSearchText;
            set
            {
                if (SetProperty(ref _tagSearchText, value))
                    UpdateSuggestions();
            }
        }

        public bool HasTags => SelectedTags.Count > 0;
        public bool HasSuggestions => SuggestedTags.Count > 0;

        private string _savedQueryKey = "saved";
        private string _deletedQueryKey = "deleted";

        // Dirty-tracking originals (set after load/save)
        private string _originalTitle = string.Empty;
        private string? _originalDetails;
        private int _originalPrayerCardId;
        private bool _originalCanNotify;
        private PrayerFrequency _originalFrequency;
        private bool _originalIsAnswered;
        private int _originalNotifyHour;
        private int _originalNotifyMinute;
        private int _originalDayOfWeek;
        private int _originalDayOfMonth;

        public bool ReturnToCards { get; set; }
        public bool IsNew => _prayer.Id == 0;
        public bool ShowSaveAndNew => IsNew && ReturnToCards;


        /// <summary>Raised after Save &amp; Add Another resets the form, so the view can focus the title entry.</summary>
        public event EventHandler? FormResetRequested;

        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEditable));
                }
            }
        }

        public bool IsEditable => !IsReadOnly;

        public bool IsNotAnswered => !IsAnswered;

        public bool IsDirty =>
            Title != _originalTitle ||
            Details != _originalDetails ||
            PrayerCardId != _originalPrayerCardId ||
            CanNotify != _originalCanNotify ||
            PrayerFrequency != _originalFrequency ||
            IsAnswered != _originalIsAnswered ||
            _prayer.NotifyHour != _originalNotifyHour ||
            _prayer.NotifyMinute != _originalNotifyMinute ||
            _prayer.NotifyDayOfWeek != _originalDayOfWeek ||
            _prayer.NotifyDayOfMonth != _originalDayOfMonth;

        public async Task<bool> CanLeaveAsync()
        {
            if (!IsDirty) return true;
            return await _navigationService.DisplayConfirmAsync(
                "Unsaved Changes", "Discard changes?", "Discard", "Cancel");
        }

        public string Identifier => _prayer.Id.ToString();

        public int Id
        {
            get => _prayer.Id;
            set
            {
                if (_prayer.Id != value)
                {
                    _prayer.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _prayer.Title;
            set
            {
                if (_prayer.Title != value)
                {
                    _prayer.Title = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccessibleSummary));
                }
            }
        }

        public string? Details
        {
            get => _prayer.Details;
            set
            {
                if (_prayer.Details != value)
                {
                    _prayer.Details = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PrayerCardId
        {
            get => _prayer.PrayerCardId;
            set
            {
                if (_prayer.PrayerCardId != value)
                {
                    _prayer.PrayerCardId = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanNotify
        {
            get => _prayer.CanNotify;
            set
            {
                if (_prayer.CanNotify != value)
                {
                    _prayer.CanNotify = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowNotifyTime));
                    OnPropertyChanged(nameof(ShowDayOfWeek));
                    OnPropertyChanged(nameof(ShowDayOfMonth));
                    // Request OS permission immediately when user enables notifications here
                    // (e.g. during tutorial) rather than waiting for Settings page visit.
                    if (value && _settings.AllowNotifications)
                        _notificationService.RequestPermissionAsync().SafeFireAndForget();
                }
            }
        }

        public PrayerFrequency PrayerFrequency
        {
            get => _prayer.PrayerFrequency;
            set
            {
                if (_prayer.PrayerFrequency != value)
                {
                    _prayer.PrayerFrequency = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PrayerFrequencyDisplay));
                    OnPropertyChanged(nameof(ShowDayOfWeek));
                    OnPropertyChanged(nameof(ShowDayOfMonth));

                    // Materialize defaults so the displayed Picker value matches
                    // what actually gets saved (avoids -1 sentinel persisting).
                    if (value == PrayerFrequency.Weekly && _prayer.NotifyDayOfWeek < 0)
                        SelectedDayOfWeek = DaysOfWeek[(int)DateTime.Now.DayOfWeek];
                    if (value == PrayerFrequency.Monthly && _prayer.NotifyDayOfMonth <= 0)
                        SelectedDayOfMonth = DateTime.Now.Day;
                }
            }
        }

        public string PrayerFrequencyDisplay
        {
            get
            {
                var time = new TimeSpan(_prayer.NotifyHour, _prayer.NotifyMinute, 0);
                var timeStr = DateTime.Today.Add(time).ToString("h:mm tt");
                return PrayerFrequency switch
                {
                    PrayerFrequency.Daily => $"Daily at {timeStr}",
                    PrayerFrequency.Weekly when _prayer.NotifyDayOfWeek >= 0 =>
                        $"Weekly on {DaysOfWeek[_prayer.NotifyDayOfWeek]} at {timeStr}",
                    PrayerFrequency.Weekly => $"Weekly at {timeStr}",
                    PrayerFrequency.Monthly when _prayer.NotifyDayOfMonth > 0 =>
                        $"Monthly on the {OrdinalSuffix(_prayer.NotifyDayOfMonth)} at {timeStr}",
                    PrayerFrequency.Monthly => $"Monthly at {timeStr}",
                    PrayerFrequency.Yearly => $"Yearly at {timeStr}",
                    PrayerFrequency.OneTime => $"Once at {timeStr}",
                    _ => PrayerFrequency.ToString()
                };
            }
        }

        public TimeSpan NotifyTime
        {
            get => new TimeSpan(_prayer.NotifyHour, _prayer.NotifyMinute, 0);
            set
            {
                _prayer.NotifyHour = value.Hours;
                _prayer.NotifyMinute = value.Minutes;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerFrequencyDisplay));
            }
        }

        public bool ShowNotifyTime => CanNotify;
        public bool ShowDayOfWeek => CanNotify && PrayerFrequency == PrayerFrequency.Weekly;
        public bool ShowDayOfMonth => CanNotify && PrayerFrequency == PrayerFrequency.Monthly;

        public List<string> DaysOfWeek { get; } =
            ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

        public string? SelectedDayOfWeek
        {
            get => _prayer.NotifyDayOfWeek >= 0 ? DaysOfWeek[_prayer.NotifyDayOfWeek] : null;
            set
            {
                _prayer.NotifyDayOfWeek = value != null ? DaysOfWeek.IndexOf(value) : -1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerFrequencyDisplay));
            }
        }

        public IReadOnlyList<int> DaysOfMonth { get; } = Enumerable.Range(1, 31).ToList();

        public int SelectedDayOfMonth
        {
            get => _prayer.NotifyDayOfMonth > 0 ? _prayer.NotifyDayOfMonth : DateTime.Now.Day;
            set
            {
                _prayer.NotifyDayOfMonth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerFrequencyDisplay));
            }
        }

        private static string OrdinalSuffix(int day) => day switch
        {
            1 or 21 or 31 => $"{day}st",
            2 or 22 => $"{day}nd",
            3 or 23 => $"{day}rd",
            _ => $"{day}th"
        };

        public bool IsAnswered
        {
            get => _prayer.IsAnswered;
            set
            {
                if (_prayer.IsAnswered != value)
                {
                    _prayer.IsAnswered = value;
                    _prayer.AnsweredAt = value ? DateTime.Now : null;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AnsweredAt));
                    OnPropertyChanged(nameof(AnsweredAtDisplay));
                    OnPropertyChanged(nameof(IsNotAnswered));
                    OnPropertyChanged(nameof(AccessibleSummary));
                }
            }
        }

        public DateTime? AnsweredAt => _prayer.AnsweredAt;

        public string AnsweredAtDisplay =>
            IsAnswered && AnsweredAt.HasValue
                ? $"✓ {AnsweredAt.Value:MMM d}"
                : string.Empty;

        /// <summary>
        /// Composed accessible label for screen readers. VoiceOver reads this as a single
        /// announcement for the prayer row: "Card Name, Prayer Title, Answered Mar 15".
        /// </summary>
        public string AccessibleSummary
        {
            get
            {
                var parts = new List<string>(3);
                if (!string.IsNullOrEmpty(CardTitle)) parts.Add(CardTitle);
                parts.Add(Title);
                if (IsAnswered && !string.IsNullOrEmpty(AnsweredAtDisplay))
                    parts.Add(AnsweredAtDisplay);
                return string.Join(", ", parts);
            }
        }

        public IReadOnlyList<PrayerFrequency> FrequencyOptions { get; } =
            new ReadOnlyCollection<PrayerFrequency>(Enum.GetValues<PrayerFrequency>().ToList());

        private string _cardTitle = string.Empty;
        public string CardTitle
        {
            get => _cardTitle;
            set
            {
                if (SetProperty(ref _cardTitle, value))
                    OnPropertyChanged(nameof(AccessibleSummary));
            }
        }

        public ObservableCollection<PrayerCard> AvailableCards { get; } = new();

        private PrayerCard? _selectedCard;
        public PrayerCard? SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (SetProperty(ref _selectedCard, value) && value is not null)
                {
                    PrayerCardId = value.Id;
                    CardTitle = value.Title ?? string.Empty;
                }
            }
        }

        public DateTime CreatedAt => _prayer.CreatedAt;
        public DateTime UpdatedAt => _prayer.UpdatedAt;

        public PrayerRequestDetailViewModel(IPrayerService prayerService, ITagService tagService,
            ICardService cardService, IOnboardingService onboardingService, INotificationService notificationService,
            INavigationService navigationService, IAccessibilityService accessibilityService, ISettings settings)
        {
            _prayer = new Prayer();
            _prayerService = prayerService;
            _tagService = tagService;
            _cardService = cardService;
            _onboardingService = onboardingService;
            _notificationService = notificationService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            SaveAndNewCommand = new AsyncRelayCommand(SaveAndNewAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);
            EditPrayerCommand = new AsyncRelayCommand(EditPrayerAsync);
            MarkAnsweredCommand = new AsyncRelayCommand(MarkAnsweredAsync);
            ShareCommand = new AsyncRelayCommand(ShareAsync);
            AddSuggestedTagCommand = new AsyncRelayCommand<int>(AddSuggestedTagAsync);
            SubmitTagEntryCommand = new AsyncRelayCommand(SubmitTagEntryAsync);

            SelectedTags.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTags));
            SuggestedTags.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSuggestions));
        }

        public PrayerRequestDetailViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INotificationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
        { }

        public PrayerRequestDetailViewModel(Prayer prayer) : this()
        {
            _prayer = prayer ?? new Prayer();
            IsReadOnly = false;
        }

        /// <summary>Core save logic shared by Save and Save &amp; Add Another.</summary>
        /// <returns>True if this was a new prayer (first save).</returns>
        private async Task<bool> CoreSaveAsync()
        {
            bool isNew = _prayer.Id == 0;
            await _prayerService.SavePrayerAsync(_prayer);
            CaptureOriginals();

            // New prayers: persist staged tags now that we have an ID.
            // Existing prayers persist tags immediately in AddSuggestedTagAsync.
            if (isNew)
            {
                foreach (var chip in SelectedTags)
                    await _tagService.AddTagToRequestAsync(_prayer.Id, chip.Id);
            }

            if (!string.IsNullOrWhiteSpace(_tagSearchText))
                await SubmitTagEntryAsync();

            if (_prayer.CanNotify)
                await _notificationService.ScheduleAsync(_prayer);
            else
                await _notificationService.CancelAsync(_prayer.Id, _prayer.PrayerFrequency);

            if (isNew)
                _onboardingService.Advance();

            _accessibilityService.Announce("Prayer saved");
            return isNew;
        }

        private async Task SaveAsync()
        {
            var origCardId = _originalPrayerCardId; // capture before CoreSaveAsync resets originals
            bool isNew = await CoreSaveAsync();

            if (ReturnToCards)
            {
                string route = isNew ? ".." : "../..";
                bool cardChanged = !isNew && origCardId != 0 && origCardId != PrayerCardId;
                var queryParts = $"prayerSaved={Identifier}&parentCardId={PrayerCardId}";
                if (cardChanged)
                    queryParts += $"&oldCardId={origCardId}";
                await _navigationService.GoToAsync($"{route}?{queryParts}");
            }
            else
            {
                await _navigationService.GoToAsync($"..?{_savedQueryKey}={Identifier}");
            }
        }

        private bool _saving;
        private async Task SaveAndNewAsync()
        {
            if (_saving) return;
            if (string.IsNullOrWhiteSpace(Title))
            {
                await _navigationService.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
                return;
            }

            _saving = true;
            try
            {
                var savedTitle = Title;
                var cardId = PrayerCardId;
                await CoreSaveAsync();

                await CommunityToolkit.Maui.Alerts.Toast.Make($"Saved \"{savedTitle}\"").Show();
                ResetForNewPrayer(cardId);
            }
            finally { _saving = false; }
        }

        private Prayer CreateDefaultPrayer(int? cardId = null) => new()
        {
            PrayerCardId = cardId ?? 0,
            NotifyHour = _settings.DefaultNotifyHour,
            NotifyMinute = _settings.DefaultNotifyMinute
        };

        private void ResetForNewPrayer(int cardId)
        {
            _prayer = CreateDefaultPrayer(cardId);
            SelectedTags.Clear();
            SuggestedTags.Clear();
            TagSearchText = string.Empty;
            RefreshProperties();
            CaptureOriginals();
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(ShowSaveAndNew));
            FormResetRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task DeleteAsync()
        {
            bool confirmed = await _navigationService.DisplayConfirmAsync(
                "Delete Prayer Request",
                $"Delete \"{Title}\"?",
                "Delete", "Cancel");

            if (!confirmed) return;

            await _prayerService.DeletePrayerAsync(_prayer);
            _accessibilityService.Announce("Prayer deleted");
            if (ReturnToCards)
            {
                await _navigationService.GoToAsync($"..?prayerDeleted={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await _navigationService.GoToAsync($"..?{_deletedQueryKey}={Identifier}");
            }
        }

        private async Task SelectPrayerAsync()
        {
            if (ReturnToCards)
            {
                await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?load={Identifier}&viewOnly=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?load={Identifier}&viewOnly=true");
            }
        }

        private async Task EditPrayerAsync()
        {
            if (ReturnToCards)
            {
                await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?load={Identifier}&edit=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?load={Identifier}&edit=true");
            }
        }

        private async Task MarkAnsweredAsync()
        {
            IsAnswered = true;
            await _notificationService.CancelAsync(_prayer.Id);
            await _prayerService.SavePrayerAsync(_prayer);
            CaptureOriginals(); // Reset dirty state before navigation

            // Navigate back so parent page (list or card accordion) picks up the change
            if (ReturnToCards)
            {
                await _navigationService.GoToAsync($"..?prayerSaved={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await _navigationService.GoToAsync($"..?{_savedQueryKey}={Identifier}");
            }
        }

        private async Task ShareAsync()
        {
            var text = string.IsNullOrWhiteSpace(Details)
                ? Title
                : $"{Title}\n\n{Details}";
            await Share.RequestAsync(new ShareTextRequest { Title = Title, Text = text });
        }

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("newForCard"))
            {
                if (int.TryParse(query["newForCard"].ToString(), out int cardId))
                {
                    _prayer = CreateDefaultPrayer(cardId);
                    ReturnToCards = true;
                    _savedQueryKey = "prayerSaved";
                    _deletedQueryKey = "prayerDeleted";
                    IsReadOnly = false;
                    RefreshProperties();
                    InitNewPrayerAsync().SafeFireAndForget();
                }
            }
            else if (query.ContainsKey("new"))
            {
                // New prayer from the prayer list (no card pre-selected)
                _prayer = CreateDefaultPrayer();
                IsReadOnly = false;
                RefreshProperties();
                InitNewPrayerAsync().SafeFireAndForget();
            }
            else if (query.ContainsKey("load"))
            {
                if (query.ContainsKey("returnToCards"))
                {
                    ReturnToCards = true;
                    _savedQueryKey = "prayerSaved";
                    _deletedQueryKey = "prayerDeleted";
                }

                if (query.ContainsKey("viewOnly"))
                {
                    IsReadOnly = true;
                }
                else if (query.ContainsKey("edit"))
                {
                    IsReadOnly = false;
                }

                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    LoadPrayerAsync(_id).SafeFireAndForget();
                }
            }
            else if (query.ContainsKey("prayerSaved"))
            {
                // Navigated back to the view-only page after saving from the edit page.
                // Reload unconditionally — this page can only receive prayerSaved for
                // the prayer it was already displaying (no ID guard needed, and an ID
                // guard would be unreliable if LoadPrayerAsync hasn't completed yet).
                Reload();
            }
            else if (query.ContainsKey("saved"))
            {
                // Same scenario via the PrayerListPage (ReturnToCards = false) code path.
                Reload();
            }
        }

        private async Task LoadPrayerAsync(int id)
        {
            try
            {
                var result = await Prayer.LoadAsync(id);
                if (result is null)
                {
                    await _navigationService.GoToAsync("..");
                    return;
                }
                _prayer = result;
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to load prayer: {e.Message}", "OK");
                return;
            }
            RefreshProperties();
            CaptureOriginals();
            await LoadCardsAsync();
            await LoadTagsAsync();
        }

        public void Reload()
        {
            LoadPrayerAsync(_prayer.Id).SafeFireAndForget();
        }

        private async Task InitNewPrayerAsync()
        {
            CaptureOriginals();
            await LoadCardsAsync();
            await LoadTagsAsync();
        }

        private async Task LoadCardsAsync()
        {
            var cards = await _cardService.GetCardsAsync();
            AvailableCards.Clear();
            foreach (var card in cards)
                AvailableCards.Add(card);

            // Set SelectedCard to the current prayer's card
            _selectedCard = AvailableCards.FirstOrDefault(c => c.Id == _prayer.PrayerCardId);
            OnPropertyChanged(nameof(SelectedCard));
        }

        private async Task LoadTagsAsync()
        {
            _allTags = (await _tagService.GetTagsAsync()).ToList();

            // For existing prayers, load their assigned tags from the DB.
            // For new prayers (Id <= 0), SelectedTags stays empty — user can still
            // add tags via the search entry; they'll be staged locally until save.
            if (_prayer.Id > 0)
            {
                var requestTags = await _tagService.GetTagsByRequestIdAsync(_prayer.Id);
                SelectedTags.Clear();
                foreach (var tag in requestTags.OrderBy(t => t.Name))
                    SelectedTags.Add(new TagChipViewModel(tag, RemoveTagAsync));
            }

            UpdateSuggestions();
        }

        private void UpdateSuggestions()
        {
            SuggestedTags.Clear();
            if (string.IsNullOrWhiteSpace(_tagSearchText)) return;

            var assignedIds = SelectedTags.Select(t => t.Id).ToHashSet();
            var filtered = _allTags
                .Where(t => !t.IsSystem && !assignedIds.Contains(t.Id) &&
                            (t.Name?.Contains(_tagSearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(t => t.Name)
                .Take(6);

            foreach (var tag in filtered)
                SuggestedTags.Add(tag);
        }

        private async Task AddSuggestedTagAsync(int tagId)
        {
            if (SelectedTags.Any(t => t.Id == tagId)) return;
            var tag = _allTags.FirstOrDefault(t => t.Id == tagId);
            if (tag is null) return;

            // Only persist immediately if the prayer has been saved (has a real ID).
            // For new prayers (Id == 0), just stage locally — SaveAsync persists them.
            if (_prayer.Id > 0)
                await _tagService.AddTagToRequestAsync(_prayer.Id, tagId);
            SelectedTags.Add(new TagChipViewModel(tag, RemoveTagAsync));
            TagSearchText = string.Empty;
        }

        private async Task RemoveTagAsync(int tagId)
        {
            // Only hit the DB if the prayer has been saved (has a real ID).
            if (_prayer.Id > 0)
                await _tagService.RemoveTagFromRequestAsync(_prayer.Id, tagId);
            var chip = SelectedTags.FirstOrDefault(t => t.Id == tagId);
            if (chip is not null)
                SelectedTags.Remove(chip);
        }

        private async Task SubmitTagEntryAsync()
        {
            var text = _tagSearchText.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            // If there's exactly one suggestion and it matches, just assign it
            var exactMatch = _allTags.FirstOrDefault(
                t => string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                await AddSuggestedTagAsync(exactMatch.Id);
                return;
            }

            // Create a new tag and assign it
            var newTag = new PrayerTag { Name = text };
            newTag = await _tagService.SaveTagAsync(newTag);
            _allTags.Add(newTag);

            // Only persist junction row if prayer has been saved (has a real ID).
            // For new prayers (Id == 0), just stage locally — SaveAsync persists them.
            if (_prayer.Id > 0)
                await _tagService.AddTagToRequestAsync(_prayer.Id, newTag.Id);
            SelectedTags.Add(new TagChipViewModel(newTag, RemoveTagAsync));
            TagSearchText = string.Empty;
        }

        private void CaptureOriginals()
        {
            _originalTitle = Title ?? string.Empty;
            _originalDetails = Details;
            _originalPrayerCardId = PrayerCardId;
            _originalCanNotify = CanNotify;
            _originalFrequency = PrayerFrequency;
            _originalIsAnswered = IsAnswered;
            _originalNotifyHour = _prayer.NotifyHour;
            _originalNotifyMinute = _prayer.NotifyMinute;
            _originalDayOfWeek = _prayer.NotifyDayOfWeek;
            _originalDayOfMonth = _prayer.NotifyDayOfMonth;
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(PrayerCardId));
            OnPropertyChanged(nameof(CanNotify));
            OnPropertyChanged(nameof(IsAnswered));
            OnPropertyChanged(nameof(AnsweredAt));
            OnPropertyChanged(nameof(AnsweredAtDisplay));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(Identifier));
            OnPropertyChanged(nameof(PrayerFrequency));
            OnPropertyChanged(nameof(PrayerFrequencyDisplay));
            OnPropertyChanged(nameof(NotifyTime));
            OnPropertyChanged(nameof(ShowNotifyTime));
            OnPropertyChanged(nameof(ShowDayOfWeek));
            OnPropertyChanged(nameof(ShowDayOfMonth));
            OnPropertyChanged(nameof(SelectedDayOfWeek));
            OnPropertyChanged(nameof(SelectedDayOfMonth));
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(IsNotAnswered));
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(ShowSaveAndNew));
            OnPropertyChanged(nameof(CardTitle));
            OnPropertyChanged(nameof(AccessibleSummary));
        }
    }
}
