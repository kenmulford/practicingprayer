using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Prayer;
using PrayerApp.Views.PrayerCard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    internal class PrayerCardViewModel : ObservableObject, IQueryAttributable, IEditGuard
    {
        private PrayerCard _prayerCard;
        private bool _isExpanded;
        private bool _prayersLoaded;
        private string _originalTitle = string.Empty;
        private readonly ICardService _cardService;
        private readonly IPrayerService _prayerService;
        private readonly IOnboardingService _onboardingService;

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
            return await Shell.Current.DisplayAlertAsync(
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

        public PrayerCardViewModel()
        {
            _prayerCard = new PrayerCard();
            _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            _onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
            ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
            AddPrayerCommand = new AsyncRelayCommand(AddPrayerAsync);
            Prayers = new ObservableCollection<PrayerRequestDetailViewModel>();
            Prayers.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasPrayers));
        }

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

        #endregion

        #region Private Methods

        private async Task SaveAsync()
        {
            bool isNew = _prayerCard.Id == 0;
            await _cardService.SaveCardAsync(_prayerCard);
            _originalTitle = Title; // Reset dirty state before navigation
            if (isNew)
                _onboardingService.Advance(); // NameCard → AddRequest
            SemanticScreenReader.Announce("Card saved");
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            if (_prayerCard.IsSystem) return;

            int prayerCount = Prayers.Count;
            string detail = prayerCount > 0
                ? $"Delete \"{Title}\"? This will also delete {prayerCount} prayer request{(prayerCount == 1 ? "" : "s")}."
                : $"Delete \"{Title}\"?";

            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Delete Card", detail, "Delete", "Cancel");

            if (!confirmed) return;

            var prayers = await _prayerService.GetPrayersByCardAsync(_prayerCard.Id);
            foreach (var prayer in prayers)
                await _prayerService.DeletePrayerAsync(prayer);
            await _cardService.DeleteCardAsync(_prayerCard);
            SemanticScreenReader.Announce("Card deleted");
            await Shell.Current.GoToAsync($"..?deleted={Identifier}");
        }

        private async Task SelectPrayerCardAsync()
        {
            if (_prayerCard.IsSystem) return;
            await Shell.Current.GoToAsync($"{nameof(PrayerCardPage)}?load={Identifier}");
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
            await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?newForCard={_prayerCard.Id}");
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
                _prayerCard = await PrayerCard.LoadAsync(id);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load card: {e.Message}", "OK");
            }
            finally
            {
                _originalTitle = _prayerCard.Title ?? string.Empty;
                RefreshProperties();
            }
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
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load prayers: {e.Message}", "OK");
            }
        }

        public async Task AddOrUpdatePrayerAsync(int prayerId)
        {
            if (!_prayersLoaded)
            {
                return;
            }

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

        public void RemovePrayer(int prayerId)
        {
            if (!_prayersLoaded)
            {
                return;
            }

            var existing = Prayers.FirstOrDefault(p => p.Id == prayerId);
            if (existing != null)
            {
                Prayers.Remove(existing);
                OnPropertyChanged(nameof(HasPrayers));
                LoadActivePrayerCountAsync().SafeFireAndForget();
            }
        }

        #endregion
    }
}
