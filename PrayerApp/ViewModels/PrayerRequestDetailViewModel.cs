using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Prayer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerRequestDetailViewModel : ObservableObject, IQueryAttributable
    {
        private Prayer _prayer;
        private readonly IPrayerService _prayerService;
        private readonly ITagService _tagService;
        private readonly IOnboardingService _onboardingService;
        private readonly INotificationService _notificationService;
        private List<PrayerTag> _allTags = new();

        public ICommand SaveCommand { get; private set; }
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
        public bool ReturnToCards { get; set; }

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
                    // Request OS permission immediately when user enables notifications here
                    // (e.g. during tutorial) rather than waiting for Settings page visit.
                    if (value) Services.Settings.EnsureNotificationPermissionRequested();
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
                }
            }
        }

        public string PrayerFrequencyDisplay => PrayerFrequency.ToString();

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
                }
            }
        }

        public DateTime? AnsweredAt => _prayer.AnsweredAt;

        public string AnsweredAtDisplay =>
            IsAnswered && AnsweredAt.HasValue
                ? $"✓ {AnsweredAt.Value:MMM d}"
                : string.Empty;

        public IReadOnlyList<PrayerFrequency> FrequencyOptions { get; } =
            new ReadOnlyCollection<PrayerFrequency>(Enum.GetValues<PrayerFrequency>().ToList());

        private string _cardTitle = string.Empty;
        public string CardTitle
        {
            get => _cardTitle;
            set => SetProperty(ref _cardTitle, value);
        }

        public DateTime CreatedAt => _prayer.CreatedAt;
        public DateTime UpdatedAt => _prayer.UpdatedAt;

        public PrayerRequestDetailViewModel()
        {
            _prayer = new Prayer();
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            _tagService = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();
            _onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();
            _notificationService = IPlatformApplication.Current!.Services.GetRequiredService<INotificationService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
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

        public PrayerRequestDetailViewModel(Prayer prayer) : this()
        {
            _prayer = prayer ?? new Prayer();
            IsReadOnly = false;
        }

        private async Task SaveAsync()
        {
            bool isNew = _prayer.Id == 0;
            await _prayerService.SavePrayerAsync(_prayer);
            // Id is now assigned (even for new prayers).

            // For new prayers, persist any tags that were staged in SelectedTags
            // (they couldn't be persisted earlier because the prayer had no ID).
            if (isNew)
            {
                foreach (var chip in SelectedTags)
                    await _tagService.AddTagToRequestAsync(_prayer.Id, chip.Id);
            }

            // Auto-submit any tag text the user typed without pressing Return
            if (!string.IsNullOrWhiteSpace(_tagSearchText))
                await SubmitTagEntryAsync();

            // Schedule or cancel notifications accordingly.
            if (_prayer.CanNotify)
                await _notificationService.ScheduleAsync(_prayer);
            else
                await _notificationService.CancelAsync(_prayer.Id);
            if (isNew)
                _onboardingService.Advance(); // NameRequest → PrayerTime
            if (ReturnToCards)
            {
                // New prayers: stack is Cards→Edit (1 level back)
                // Existing prayers: stack is Cards→View→Edit (2 levels back)
                string route = isNew ? ".." : "../..";
                await Shell.Current.GoToAsync($"{route}?prayerSaved={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"..?{_savedQueryKey}={Identifier}");
            }
        }

        private async Task DeleteAsync()
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Delete Prayer Request",
                $"Delete \"{Title}\"?",
                "Delete", "Cancel");

            if (!confirmed) return;

            await _prayerService.DeletePrayerAsync(_prayer);
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"..?prayerDeleted={Identifier}&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"..?{_deletedQueryKey}={Identifier}");
            }
        }

        private async Task SelectPrayerAsync()
        {
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&viewOnly=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&viewOnly=true");
            }
        }

        private async Task EditPrayerAsync()
        {
            if (ReturnToCards)
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&edit=true&returnToCards=true&parentCardId={PrayerCardId}");
            }
            else
            {
                await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}&edit=true");
            }
        }

        private async Task MarkAnsweredAsync()
        {
            IsAnswered = true;
            await _notificationService.CancelAsync(_prayer.Id);
            await _prayerService.SavePrayerAsync(_prayer);
            RefreshProperties();
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
                    _prayer = new Prayer { PrayerCardId = cardId };
                    ReturnToCards = true;
                    _savedQueryKey = "prayerSaved";
                    _deletedQueryKey = "prayerDeleted";
                    IsReadOnly = false;
                    RefreshProperties();
                    LoadTagsAsync().SafeFireAndForget();
                }
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
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                _prayer = result;
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load prayer: {e.Message}", "OK");
                return;
            }
            RefreshProperties();
            await LoadTagsAsync();
        }

        public void Reload()
        {
            LoadPrayerAsync(_prayer.Id).SafeFireAndForget();
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
                .Where(t => !assignedIds.Contains(t.Id) &&
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
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(IsNotAnswered));
            OnPropertyChanged(nameof(CardTitle));
        }
    }
}
