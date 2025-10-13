using System;
using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;

namespace PrayerApp.ViewModels
{
    internal class PrayerCategoryViewModel : ObservableObject, IQueryAttributable
    {
        private PrayerCategory _prayerCategory;

        public ICommand SaveCommand { get; private set; }
        // public ICommand DeleteCommand { get; private set; }

        #region Properties

        public string Identifier => _prayerCategory.Id.ToString();

        public int Id => _prayerCategory.Id;

        public string Name
        {
            get => _prayerCategory.Name;
            set
            {
                if (_prayerCategory.Name != value)
                {
                    _prayerCategory.Name = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region Constructors

        public PrayerCategoryViewModel()
        {
            _prayerCategory = new PrayerCategory();
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            //DeleteCommand = new AsyncRelayCommand(Delete);
        }

        public PrayerCategoryViewModel(PrayerCategory _pc)
        {
            _prayerCategory = _pc;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            //DeleteCommand = new AsyncRelayCommand(Delete);
        }

        #endregion

        #region Private Methods

        private async Task SaveAsync()
        {
            _prayerCategory.UpdatedAt = DateTime.Now;
            await _prayerCategory.SaveAsync();
            await Shell.Current.GoToAsync($"..?saved={_prayerCategory.Name}");
        }

        //private async Task Delete()
        //{
        //    _note.Delete();
        //    await Shell.Current.GoToAsync($"..?deleted={_note.Filename}");
        //}

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("load"))
            {
                if(int.TryParse(query["load"].ToString(),out int _id))
                {
                    // fire task and forget
                    _ = LoadPrayerCategoryAsync(_id);
                }
                
                RefreshProperties();
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadPrayerCategoryAsync(int id)
        {
            try
            {
                _prayerCategory = await PrayerCategory.LoadAsync(id);
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load category: {e.Message}", "OK");
            }
            finally
            {
                RefreshProperties();
            }
        }


        public void Reload()
        {
            _ = LoadPrayerCategoryAsync(_prayerCategory.Id);
            RefreshProperties();
        }

        private void RefreshProperties()
        {
            OnPropertyChanged(nameof(Name));
        }

        #endregion
    }
}
