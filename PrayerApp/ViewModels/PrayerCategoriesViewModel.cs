using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Views.PrayerCategory;

namespace PrayerApp.ViewModels
{
    internal class PrayerCategoriesViewModel : IQueryAttributable
    {
        private List<PrayerCategory> _prayerCategories;
        public ObservableCollection<PrayerCategoryViewModel> AllPrayerCategories { get; }

        public ICommand NewCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        #region Constructors

        public PrayerCategoriesViewModel()
        {
            _prayerCategories = Task.Run(async () => await PrayerCategory.LoadAllAsync()).Result;
            // Convert PrayerCategory to PrayerCategoryViewModel
            AllPrayerCategories = new ObservableCollection<PrayerCategoryViewModel>(
                _prayerCategories.Select(pc => new PrayerCategoryViewModel(pc))
            );
            NewCommand = new AsyncRelayCommand(NewPrayerCategoryAsync);
            SelectCategoryCommand = new AsyncRelayCommand<PrayerCategoryViewModel>(SelectPrayerCategoryAsync);
        }

        #endregion

        #region Private Methods

        private async Task NewPrayerCategoryAsync()
        {
            await Shell.Current.GoToAsync(nameof(Views.PrayerCategory.PrayerCategoryPage));
        }

        private async Task SelectPrayerCategoryAsync(ViewModels.PrayerCategoryViewModel prayerCategory)
        {
            if (prayerCategory != null)
                await Shell.Current.GoToAsync($"{nameof(PrayerCategoryPage)}?load={prayerCategory.Identifier}");
        }

        #endregion

        #region Implemented Contract Methods

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("deleted"))
            {
                string? PrayerCategoryString = query["deleted"].ToString();
                PrayerCategoryViewModel matched = AllPrayerCategories.FirstOrDefault(pc => pc.Identifier == PrayerCategoryString);

                if(matched != null)
                {
                    AllPrayerCategories.Remove(matched);
                }
            }
            else if (query.ContainsKey("saved"))
            {
                string? PrayerCategoryString = query["saved"].ToString();
                PrayerCategoryViewModel matched = AllPrayerCategories.Where((c) => c.Identifier == PrayerCategoryString).FirstOrDefault();

                // If note is found, update it
                if (matched != null)
                {
                    matched.Reload();
                }
                // If note isn't found, it's new; add it.
                else
                {
                    var cat = PrayerCategory.LoadAsync(int.Parse(PrayerCategoryString ?? "0")).Result;
                    AllPrayerCategories.Add(new PrayerCategoryViewModel(cat));
                }
                    
            }
        }

        #endregion

        #region Helper Methods

        private async Task LoadPrayerCategoriesAsync()
        {
            try
            {
                _prayerCategories = await PrayerCategory.LoadAllAsync();
            }
            catch (Exception e)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load category: {e.Message}", "OK");
            }
            finally
            {
                
            }
        }


        public void Reload()
        {
            _ = LoadPrayerCategoriesAsync();
        }

        #endregion

    }
}
