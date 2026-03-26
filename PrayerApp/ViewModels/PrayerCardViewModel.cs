using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class PrayerCardViewModel : ObservableObject, IQueryAttributable, IEditGuard
    {
        private PrayerCard _prayerCard;
        private bool _isExpanded;
        private bool _prayersLoaded;
        private bool _addingPrayer;
        private string _originalTitle = string.Empty;
        private readonly ICardService _cardService;
        private readonly IPrayerService _prayerService;
        private readonly IOnboardingService _onboardingService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectCardCommand { get; }
        public ICommand ToggleExpandedCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand AddPrayerCommand { get; }

        #region Properties

        public string Identifier => _prayerCard.Id.ToString();

        public int Id
        {
            get => _prayerCard.Id;
            set
            {
                if (_prayerCard.Id != value)
                {
                    _prayerCard.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _prayerCard.Title;
            set
            {
                if (_prayerCard.Title != value)
                {
                    _prayerCard.Title = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsFavorite
        {
            get => _prayerCard.IsFavorite;
            set
            {
                if (_prayerCard.IsFavorite != value)
                {
                    _prayerCard.IsFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowBadge));
                }
            }
        }

        public bool IsSystem => _prayerCard.IsSystem;
        public bool IsNew => _prayerCard.Id == 0;
        public bool CanDelete => !IsSystem && !IsNew;

        public bool IsDirty => Title != _originalTitle;

        public async Task<bool> CanLeaveAsync()
        {
            if (!IsDirty) return true;
            return await _navigationService.DisplayConfirmAsync(
                "Unsaved Changes", "Discard changes?", "Discard", "Cancel");
        }

        public bool IsAnswered
        {
            get => _prayerCard.IsAnswered;
            set
            {
                if (_prayerCard.IsAnswered != value)
                {
                    _prayerCard.IsAnswered = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }

        private int _activePrayerCount;
        public int ActivePrayerCount
        {
            get => _activePrayerCount;
            set => SetProperty(ref _activePrayerCount, value);
        }

        /// <summary>Show the count badge when the card is collapsed.</summary>
        public bool ShowBadge => !IsExpanded;

        public ObservableCollection<PrayerRequestDetailViewModel> Prayers { get; }

        public bool HasPrayers => Prayers.Count > 0;

        #endregion

        #region Constructors

        public PrayerCardViewModel(ICardService cardService, IPrayerService prayerService,
            IOnboardingService onboardingService, INavigationService navigationService,
            IAccessibilityService accessibilityService)
        {
            _prayerCard = new PrayerCard();
            _cardService = cardService;
            _prayerService = prayerService;
            _onboardingService = onboardingService;
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
            ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
            AddPrayerCommand = new AsyncRelayCommand(AddPrayerAsync);
            Prayers = new ObservableCollection<PrayerRequestDetailViewModel>();
            Prayers.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasPrayers));
        }

        public PrayerCardViewModel() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>())
        { }

        public PrayerCardViewModel(PrayerCard pc) : this()
        {
            _prayerCard = pc;
            LoadActivePrayerCountAsync().SafeFireAndForget();
        }

        private async Task LoadActivePrayerCountAsync()
        {
            // When prayers are already loaded locally, derive count without a service call
            if (_prayersLoaded)
                ActivePrayerCount = Prayers.Count(p => !p.IsAnswered);
            else
                ActivePrayerCount = await _prayerService.GetActivePrayerCountByCardAsync(_prayerCard.Id);
        }

        /// <summary>Refresh the badge count from the service (lightweight — single query).</summary>
        public void RefreshActivePrayerCount()
            => LoadActivePrayerCountAsync().SafeFireAndForget();

        /// <summary>Force-reload prayers from the service (e.g. after Save+ added new prayers).</summary>
        public void ReloadPrayers()
        {
            _prayersLoaded = false;
            LoadPrayersAsync().SafeFireAndForget();
        }

        #endregion

        #region Private Methods

        private async Task SaveAsync()
        {
            bool isNew = _prayerCard.Id == 0;
            await _cardService.SaveCardAsync(_prayerCard);
            _originalTitle = Title; // Reset dirty state before navigation
            if (isNew)
                _onboardingService.Advance(); // NameCard → AddRequest
            _accessibilityService.Announce("Card saved");
            await _navigationService.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            if (_prayerCard.IsSystem) return;

            var count = ActivePrayerCount;
            string detail = count > 0
                ? $"Delete \"{Title}\" and its {count} prayer request{(count == 1 ? "" : "s")}?"
                : $"Delete \"{Title}\"?";

            bool confirmed = await _navigationService.DisplayConfirmAsync(
                "Delete Card", detail, "Delete", "Cancel");

            if (!confirmed) return;

            var prayers = await _prayerService.GetPrayersByCardAsync(_prayerCard.Id);
            foreach (var prayer in prayers)
                await _prayerService.DeletePrayerAsync(prayer);
            await _cardService.DeleteCardAsync(_prayerCard);
            _accessibilityService.Announce("Card deleted");
            await _navigationService.GoToAsync($"..?deleted={Identifier}");
        }

        private async Task SelectPrayerCardAsync()
        {
            if (_prayerCard.IsSystem) return;
            await _navigationService.GoToAsync($"{Routes.PrayerCardPage}?load={Identifier}");
        }

        private async Task ToggleExpandedAsync()
        {
            if (!IsExpanded && !_prayersLoaded)
            {
                // Load first, then reveal — avoids empty flash
                await LoadPrayersAsync();
                IsExpanded = true;
            }
            else
            {
                IsExpanded = !IsExpanded;
            }
        }

        private async Task ToggleFavoriteAsync()
        {
            IsFavorite = !IsFavorite;
            await _cardService.SaveCardAsync(_prayerCard);
        }

        private async Task AddPrayerAsync()
        {
            _onboardingService.Advance(); // AddRequest → NameRequest
            await _navigationService.GoToAsync($"{Routes.PrayerDetailPage}?newForCard={_prayerCard.Id}");
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("load"))
            {
                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    LoadPrayerCardAsync(_id).SafeFireAndForget();
                }
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadPrayerCardAsync(int id)
        {
            try
            {
                var loaded = await PrayerCard.LoadAsync(id);
                if (loaded is null)
                {
                    await _navigationService.GoToAsync("..");
                    return;
                }
                _prayerCard = loaded;
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to load card: {e.Message}", "OK");
                return;
            }

            _originalTitle = _prayerCard.Title ?? string.Empty;
            RefreshProperties();
        }

        public void Reload()
        {
            LoadPrayerCardAsync(_prayerCard.Id).SafeFireAndForget();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(IsSystem));
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(HasPrayers));
            OnPropertyChanged(nameof(IsAnswered));
        }

        private async Task LoadPrayersAsync()
        {
            try
            {
                var prayers = await _prayerService.GetPrayersByCardAsync(_prayerCard.Id);
                Prayers.Clear();
                foreach (var prayer in prayers
                                            .OrderBy(p => p.IsAnswered)
                                            .ThenByDescending(p => p.AnsweredAt ?? DateTime.MinValue)
                                            .ThenBy(p => p.Title)
                )
                {
                    var viewModel = new PrayerRequestDetailViewModel(prayer)
                    {
                        ReturnToCards = true
                    };
                    Prayers.Add(viewModel);
                }

                _prayersLoaded = true;
                OnPropertyChanged(nameof(HasPrayers));
            }
            catch (Exception e)
            {
                await _navigationService.DisplayAlertAsync("Error", $"Failed to load prayers: {e.Message}", "OK");
            }
        }

        public async Task AddOrUpdatePrayerAsync(int prayerId)
        {
            if (_addingPrayer) return;
            _addingPrayer = true;
            try
            {
            // If prayers haven't been loaded yet, load them first so we can
            // display the new/updated prayer in the expanded accordion.
            if (!_prayersLoaded)
                await LoadPrayersAsync();

            var existing = Prayers.FirstOrDefault(p => p.Id == prayerId);
            if (existing != null)
            {
                existing.Reload();
                return;
            }

            var prayer = await Prayer.LoadAsync(prayerId);
            if (prayer is null) return;
            if (prayer.PrayerCardId != _prayerCard.Id)
            {
                return;
            }

            var viewModel = new PrayerRequestDetailViewModel(prayer)
            {
                ReturnToCards = true
            };
            var insertIndex = Prayers
                .TakeWhile(p => string.Compare(p.Title, prayer.Title, StringComparison.OrdinalIgnoreCase) < 0)
                .Count();
            Prayers.Insert(insertIndex, viewModel);
            OnPropertyChanged(nameof(HasPrayers));
            LoadActivePrayerCountAsync().SafeFireAndForget();
            }
            finally { _addingPrayer = false; }
        }

        public void RemovePrayer(int prayerId)
        {
            if (_prayersLoaded)
            {
                var existing = Prayers.FirstOrDefault(p => p.Id == prayerId);
                if (existing != null)
                {
                    Prayers.Remove(existing);
                    OnPropertyChanged(nameof(HasPrayers));
                }
            }

            // Always refresh the badge count, even if prayers weren't loaded
            RefreshActivePrayerCount();
        }

        #endregion
    }
}
