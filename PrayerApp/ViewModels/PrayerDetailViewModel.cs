using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.Prayer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;

namespace PrayerApp.ViewModels
{
    internal class PrayerDetailViewModel : ObservableObject, IQueryAttributable
    {
        private Prayer _prayer;
        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand FavoriteCommand { get; private set; }
        public ICommand SelectPrayerCommand { get; private set; }

        #region Properties
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
        #endregion
        #region Constructors
        public PrayerDetailViewModel(Prayer prayer)
        {
            _prayer = prayer;

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            SelectPrayerCommand = new AsyncRelayCommand(SelectPrayerAsync);
        }
        #endregion

        #region Command Definitions

        private async Task SaveAsync()
        {
            _prayer.UpdatedAt = DateTime.Now;
            await _prayer.SaveAsync();
            await Shell.Current.GoToAsync($"..?saved={Identifier}");
        }

        private async Task DeleteAsync()
        {
            await _prayer.DeleteAsync();
            await Shell.Current.GoToAsync($"..?deleted={Identifier}");
        }
        private async Task SelectPrayerAsync()
        {
            await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?load={Identifier}");
        }

        #endregion


        #region IQueryAttributable Implementation
        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("load"))
            {
                if (int.TryParse(query["load"].ToString(), out int _id))
                {
                    // fire task and forget
                    _ = LoadPrayerAsync(_id);
                }

                RefreshProperties();
            }
        }
        #endregion

        private async Task LoadPrayerAsync(int id)
        {
            try
            {
                _prayer = await Prayer.LoadAsync(id);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load prayer: {e.Message}", "OK");
            }
            finally
            {
                RefreshProperties();
            }
        }

        public void Reload()
        {
            _ = LoadPrayerAsync(_prayer.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Details));
        }
    }
}
