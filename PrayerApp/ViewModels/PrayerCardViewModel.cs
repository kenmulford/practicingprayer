using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.PrayerCard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    internal class PrayerCardViewModel : ObservableObject, IQueryAttributable
    {
        private PrayerCard _prayerCard;
        private bool _isExpanded;
        private bool _prayersLoaded;
        private readonly ICardService _cardService;
        private readonly IPrayerService _prayerService;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectCardCommand { get; }
        public ICommand ToggleExpandedCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }

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
                }
            }
        }

        public ObservableCollection<PrayerRequestDetailViewModel> Prayers { get; }

        public bool HasPrayers => Prayers.Count > 0;

        #endregion

        #region Constructors

        public PrayerCardViewModel()
        {
            _prayerCard = new PrayerCard();
            _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
            ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
            Prayers = new ObservableCollection<PrayerRequestDetailViewModel>();
            Prayers.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasPrayers));
        }

        public PrayerCardViewModel(PrayerCard _pc)
        {
            _prayerCard = _pc;
            _cardService = IPlatformApplication.Current!.Services.GetRequiredService<ICardService>();
            _prayerService = IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
            ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
            Prayers = new ObservableCollection<PrayerRequestDetailViewModel>();
            Prayers.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasPrayers));
        }

        #endregion

        #region Private Methods

        private async Task SaveAsync()
        {
            await _cardService.SaveCardAsync(_prayerCard);
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            await _cardService.DeleteCardAsync(_prayerCard);
            await Shell.Current.GoToAsync($"..?deleted={Identifier}");
        }

        private async Task SelectPrayerCardAsync()
        {
            await Shell.Current.GoToAsync($"{nameof(PrayerCardPage)}?load={Identifier}");
        }

        private async Task ToggleExpandedAsync()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && !_prayersLoaded)
            {
                await LoadPrayersAsync();
            }
        }

        private async Task ToggleFavoriteAsync()
        {
            IsFavorite = !IsFavorite;
            await _cardService.SaveCardAsync(_prayerCard);
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("load"))
            {
                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    // fire task and forget
                    _ = LoadPrayerCardAsync(_id);
                }

                RefreshProperties();
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
                RefreshProperties();
            }
        }

        public void Reload()
        {
            _ = LoadPrayerCardAsync(_prayerCard.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(HasPrayers));
        }

        private async Task LoadPrayersAsync()
        {
            try
            {
                var prayers = await _prayerService.GetPrayersByCardAsync(_prayerCard.Id);
                Prayers.Clear();
                foreach (var prayer in prayers.OrderBy(p => p.Title))
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
            if (prayer.PrayerCardId != _prayerCard.Id)
            {
                return;
            }

            var viewModel = new PrayerRequestDetailViewModel(prayer)
            {
                ReturnToCards = true
            };
            Prayers.Add(viewModel);
            OnPropertyChanged(nameof(HasPrayers));
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
            }
        }

        #endregion
    }
}
