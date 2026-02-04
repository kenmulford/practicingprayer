using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.PrayerCard;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    internal class PrayerCardViewModel : ObservableObject, IQueryAttributable
    {
        private PrayerCard _prayerCard;

        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SelectCardCommand { get; }

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

        public string? Details
        {
            get => _prayerCard.Details;
            set
            {
                if (_prayerCard.Details != value)
                {
                    _prayerCard.Details = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructors

        public PrayerCardViewModel()
        {
            _prayerCard = new PrayerCard();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
        }

        public PrayerCardViewModel(PrayerCard _pc)
        {
            _prayerCard = _pc;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectCardCommand = new AsyncRelayCommand(SelectPrayerCardAsync);
        }

        #endregion

        #region Private Methods

        private async Task SaveAsync()
        {
            await _prayerCard.SaveAsync();
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            await _prayerCard.DeleteAsync();
            await Shell.Current.GoToAsync($"..?deleted={Identifier}");
        }

        private async Task SelectPrayerCardAsync()
        {
            await Shell.Current.GoToAsync($"{nameof(PrayerCardPage)}?load={Identifier}");
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
            OnPropertyChanged(nameof(Details));
        }

        #endregion
    }
}
